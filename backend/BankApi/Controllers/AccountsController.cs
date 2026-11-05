using System.Globalization;
using System.Text;
using BankApi.Data;
using BankApi.DTOs;
using BankApi.Extensions;
using BankApi.Integrations.Caching;
using BankApi.Integrations.Notifications;
using BankApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BankApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AccountsController : ControllerBase
{
    private const decimal MaxDepositAmount = 10_000m;
    private const int MaxExportRows = 5_000;

    private readonly BankDbContext _db;
    private readonly RedisCacheService _cache;
    private readonly NotificationService _notifications;

    public AccountsController(BankDbContext db, RedisCacheService cache, NotificationService notifications)
    {
        _db = db;
        _cache = cache;
        _notifications = notifications;
    }

    public static string CacheKey(Guid customerId) => $"accounts:{customerId}";

   
    [HttpGet]
    public async Task<ActionResult<IEnumerable<AccountDto>>> GetAll()
    {
        var customerId = User.GetCustomerId();

     
        var accounts = await _cache.GetOrSetAsync(CacheKey(customerId), TimeSpan.FromSeconds(30), () =>
            _db.Accounts
                .Include(a => a.Customer)
                .Where(a => a.CustomerId == customerId)
                .Select(a => new AccountDto(a.Id, a.Iban, a.Balance, a.Currency, a.Customer!.FullName))
                .ToListAsync());

        return Ok(accounts);
    }


    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AccountDto>> GetById(Guid id)
    {
        var customerId = User.GetCustomerId();

        var account = await _db.Accounts
            .AsNoTracking()
            .Include(a => a.Customer)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (account is null) return NotFound();
        if (account.CustomerId != customerId) return Forbid();

        return Ok(new AccountDto(account.Id, account.Iban, account.Balance, account.Currency, account.Customer!.FullName));
    }

    
    [HttpPost]
    public async Task<ActionResult<AccountDto>> Create([FromBody] CreateAccountRequestDto request)
    {
        var customerId = User.GetCustomerId();
        var customer = await _db.Customers.FindAsync(customerId);
        if (customer is null) return Unauthorized();

        var account = new Account
        {
            Iban = GenerateFakeIban(),
            Balance = 0,
            Currency = string.IsNullOrWhiteSpace(request.Currency) ? "TND" : request.Currency,
            CustomerId = customerId
        };

        _db.Accounts.Add(account);
        await _db.SaveChangesAsync();
        await _cache.RemoveAsync(CacheKey(customerId));

        return CreatedAtAction(nameof(GetById), new { id = account.Id },
            new AccountDto(account.Id, account.Iban, account.Balance, account.Currency, customer.FullName));
    }

    
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var customerId = User.GetCustomerId();
        var account = await _db.Accounts.FirstOrDefaultAsync(a => a.Id == id);

        if (account is null) return NotFound();
        if (account.CustomerId != customerId) return Forbid();
        if (account.Balance != 0) return BadRequest("Impossible de fermer un compte dont le solde n'est pas nul.");

        _db.Accounts.Remove(account);
        await _db.SaveChangesAsync();
        await _cache.RemoveAsync(CacheKey(customerId));

        return NoContent();
    }

    
    [HttpPost("{id:guid}/deposit")]
    public async Task<IActionResult> Deposit(Guid id, [FromBody] DepositRequestDto request)
    {
        if (request.Amount <= 0 || request.Amount > MaxDepositAmount)
            return BadRequest($"Le montant doit être compris entre 0,01 et {MaxDepositAmount:N0}.");

        if (decimal.Round(request.Amount, 2) != request.Amount)
            return BadRequest("Le montant ne peut pas avoir plus de 2 décimales.");

        var (account, error) = await GetOwnedAccountAsync(id);
        if (error is not null) return error;

        await using var dbTransaction = await _db.Database.BeginTransactionAsync();

        await _db.Accounts
            .Where(a => a.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.Balance, a => a.Balance + request.Amount));

        _db.Transactions.Add(new Transaction
        {
            AccountId = id,
            Type = TransactionType.Deposit,
            Amount = request.Amount,
            Label = string.IsNullOrWhiteSpace(request.Label) ? "Dépôt (simulation)" : request.Label.Trim()
        });

        await _db.SaveChangesAsync();
        await dbTransaction.CommitAsync();

        await _cache.RemoveAsync(CacheKey(account!.CustomerId));

        var newBalance = await _db.Accounts.AsNoTracking()
            .Where(a => a.Id == id).Select(a => a.Balance).FirstAsync();

        await _notifications.NotifyAsync(
            account.CustomerId,
            "Dépôt effectué",
            $"{request.Amount} {account.Currency} ont été déposés sur le compte {account.Iban} (simulation).",
            NotificationType.Success);

        return Ok(new { newBalance });
    }

  
    [HttpGet("{id:guid}/transactions")]
    public async Task<IActionResult> GetTransactions(
        Guid id,
        [FromQuery] string? type,
        [FromQuery(Name = "from")] DateTime? dateFrom,
        [FromQuery(Name = "to")] DateTime? dateTo,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var (account, error) = await GetOwnedAccountAsync(id);
        if (error is not null) return error;

        var (query, filterError) = BuildTransactionQuery(account!.Id, type, dateFrom, dateTo);
        if (query is null) return BadRequest(filterError);

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var total = await query.CountAsync();

        var rows = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var items = rows
            .Select(t => new TransactionDto(t.Id, t.Type.ToString(), t.Amount, t.Label, t.CreatedAt))
            .ToList();

        return Ok(new PagedResult<TransactionDto>(items, total, page, pageSize));
    }

  
    [HttpGet("{id:guid}/transactions/export")]
    public async Task<IActionResult> ExportTransactions(
        Guid id,
        [FromQuery] string? type,
        [FromQuery(Name = "from")] DateTime? dateFrom,
        [FromQuery(Name = "to")] DateTime? dateTo)
    {
        var (account, error) = await GetOwnedAccountAsync(id);
        if (error is not null) return error;

        var (query, filterError) = BuildTransactionQuery(account!.Id, type, dateFrom, dateTo);
        if (query is null) return BadRequest(filterError);

        var rows = await query
            .OrderByDescending(t => t.CreatedAt)
            .Take(MaxExportRows)
            .ToListAsync();

        var fr = CultureInfo.GetCultureInfo("fr-FR");
        var csv = new StringBuilder();
        csv.AppendLine("Date (UTC);Libellé;Type;Montant;Devise");

        foreach (var t in rows)
        {
            var debit = t.Type is TransactionType.Withdrawal or TransactionType.TransferOut;
            var signedAmount = debit ? -t.Amount : t.Amount;

            csv.AppendLine(string.Join(';',
                t.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                CsvCell(t.Label),
                CsvCell(TypeLabel(t.Type)),
                signedAmount.ToString("F2", fr),
                account.Currency));
        }

       
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
        var fileName = $"releve-{account.Iban.Replace(" ", "")}-{DateTime.UtcNow:yyyyMMdd}.csv";

        return File(bytes, "text/csv; charset=utf-8", fileName);
    }

   

    private async Task<(Account? Account, IActionResult? Error)> GetOwnedAccountAsync(Guid id)
    {
        var account = await _db.Accounts.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id);

        if (account is null) return (null, NotFound());
        if (account.CustomerId != User.GetCustomerId()) return (null, Forbid());

        return (account, null);
    }

    private (IQueryable<Transaction>? Query, string? Error) BuildTransactionQuery(
        Guid accountId, string? type, DateTime? dateFrom, DateTime? dateTo)
    {
        var query = _db.Transactions.AsNoTracking().Where(t => t.AccountId == accountId);

        if (!string.IsNullOrWhiteSpace(type))
        {
            if (!Enum.TryParse<TransactionType>(type, ignoreCase: true, out var parsedType) || !Enum.IsDefined(parsedType))
                return (null, "Type d'opération invalide.");

            query = query.Where(t => t.Type == parsedType);
        }

        
        if (dateFrom.HasValue)
        {
            var start = DateTime.SpecifyKind(dateFrom.Value.Date, DateTimeKind.Utc);
            query = query.Where(t => t.CreatedAt >= start);
        }

        if (dateTo.HasValue)
        {
            var endExclusive = DateTime.SpecifyKind(dateTo.Value.Date.AddDays(1), DateTimeKind.Utc);
            query = query.Where(t => t.CreatedAt < endExclusive);
        }

        return (query, null);
    }

    private static string TypeLabel(TransactionType type) => type switch
    {
        TransactionType.Deposit => "Dépôt",
        TransactionType.Withdrawal => "Retrait",
        TransactionType.TransferOut => "Virement émis",
        TransactionType.TransferIn => "Virement reçu",
        _ => type.ToString()
    };


    private static string CsvCell(string? value)
    {
        var text = value ?? string.Empty;

        if (text.Length > 0 && "=+-@\t\r".Contains(text[0]))
            text = "'" + text;

        return $"\"{text.Replace("\"", "\"\"")}\"";
    }

    private static string GenerateFakeIban()
    {
        var random = new Random();
        var digits = string.Concat(Enumerable.Range(0, 20).Select(_ => random.Next(0, 10)));
        return $"TN{digits[..2]} {digits[2..6]} {digits[6..10]} {digits[10..14]} {digits[14..18]}";
    }
}
