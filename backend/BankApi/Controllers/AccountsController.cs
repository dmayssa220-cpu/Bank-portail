using BankApi.Data;
using BankApi.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BankApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AccountsController : ControllerBase
{
    private readonly BankDbContext _db;

    public AccountsController(BankDbContext db)
    {
        _db = db;
    }

    // GET /api/accounts
    // NOTE: en production, filtrer par l'utilisateur authentifié (claim CustomerId issu du JWT)
    [HttpGet]
    public async Task<ActionResult<IEnumerable<AccountDto>>> GetAll()
    {
        var accounts = await _db.Accounts
            .Include(a => a.Customer)
            .Select(a => new AccountDto(a.Id, a.Iban, a.Balance, a.Currency, a.Customer!.FullName))
            .ToListAsync();

        return Ok(accounts);
    }

    // GET /api/accounts/{id}
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AccountDto>> GetById(Guid id)
    {
        var account = await _db.Accounts
            .Include(a => a.Customer)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (account is null) return NotFound();

        return Ok(new AccountDto(account.Id, account.Iban, account.Balance, account.Currency, account.Customer!.FullName));
    }

    // GET /api/accounts/{id}/transactions
    [HttpGet("{id:guid}/transactions")]
    public async Task<IActionResult> GetTransactions(Guid id)
    {
        var transactions = await _db.Transactions
            .Where(t => t.AccountId == id)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();

        return Ok(transactions);
    }
}
