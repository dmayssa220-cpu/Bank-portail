using BankApi.Data;
using BankApi.DTOs;
using BankApi.Extensions;
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
    private readonly BankDbContext _db;

    public AccountsController(BankDbContext db)
    {
        _db = db;
    }

    // GET /api/accounts — uniquement les comptes du client authentifié
    [HttpGet]
    public async Task<ActionResult<IEnumerable<AccountDto>>> GetAll()
    {
        var customerId = User.GetCustomerId();

        var accounts = await _db.Accounts
            .Include(a => a.Customer)
            .Where(a => a.CustomerId == customerId)
            .Select(a => new AccountDto(a.Id, a.Iban, a.Balance, a.Currency, a.Customer!.FullName))
            .ToListAsync();

        return Ok(accounts);
    }

    // GET /api/accounts/{id}
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AccountDto>> GetById(Guid id)
    {
        var customerId = User.GetCustomerId();

        var account = await _db.Accounts
            .Include(a => a.Customer)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (account is null) return NotFound();
        if (account.CustomerId != customerId) return Forbid();

        return Ok(new AccountDto(account.Id, account.Iban, account.Balance, account.Currency, account.Customer!.FullName));
    }

    // POST /api/accounts — ouvrir un nouveau compte (ex: compte épargne) pour le client connecté
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

        return CreatedAtAction(nameof(GetById), new { id = account.Id },
            new AccountDto(account.Id, account.Iban, account.Balance, account.Currency, customer.FullName));
    }

    // DELETE /api/accounts/{id} — fermer un compte (uniquement si le solde est à zéro)
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

        return NoContent();
    }

    // GET /api/accounts/{id}/transactions
    [HttpGet("{id:guid}/transactions")]
    public async Task<IActionResult> GetTransactions(Guid id)
    {
        var customerId = User.GetCustomerId();
        var account = await _db.Accounts.FirstOrDefaultAsync(a => a.Id == id);

        if (account is null) return NotFound();
        if (account.CustomerId != customerId) return Forbid();

        var transactions = await _db.Transactions
            .Where(t => t.AccountId == id)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();

        return Ok(transactions);
    }

    private static string GenerateFakeIban()
    {
        var random = new Random();
        var digits = string.Concat(Enumerable.Range(0, 20).Select(_ => random.Next(0, 10)));
        return $"TN{digits[..2]} {digits[2..6]} {digits[6..10]} {digits[10..14]} {digits[14..18]}";
    }
}
