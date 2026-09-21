using BankApi.Data;
using BankApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BankApi.Controllers;

public record NotificationDto(Guid Id, string Title, string Message, string Type, bool IsRead, DateTime CreatedAt);

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly BankDbContext _db;

    public NotificationsController(BankDbContext db)
    {
        _db = db;
    }

    // GET /api/notifications — les 50 dernières notifications du client connecté
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var customerId = User.GetCustomerId();

        var notifications = await _db.Notifications
            .Where(n => n.CustomerId == customerId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(50)
            .Select(n => new NotificationDto(n.Id, n.Title, n.Message, n.Type.ToString(), n.IsRead, n.CreatedAt))
            .ToListAsync();

        return Ok(notifications);
    }

    // GET /api/notifications/unread-count
    [HttpGet("unread-count")]
    public async Task<IActionResult> UnreadCount()
    {
        var customerId = User.GetCustomerId();
        var count = await _db.Notifications.CountAsync(n => n.CustomerId == customerId && !n.IsRead);
        return Ok(new { count });
    }

    // PATCH /api/notifications/{id}/read
    [HttpPatch("{id:guid}/read")]
    public async Task<IActionResult> MarkAsRead(Guid id)
    {
        var customerId = User.GetCustomerId();
        var notification = await _db.Notifications.FirstOrDefaultAsync(n => n.Id == id);

        if (notification is null) return NotFound();
        if (notification.CustomerId != customerId) return Forbid();

        notification.IsRead = true;
        await _db.SaveChangesAsync();

        return NoContent();
    }

    // PATCH /api/notifications/read-all
    [HttpPatch("read-all")]
    public async Task<IActionResult> MarkAllAsRead()
    {
        var customerId = User.GetCustomerId();

        await _db.Notifications
            .Where(n => n.CustomerId == customerId && !n.IsRead)
            .ExecuteUpdateAsync(setters => setters.SetProperty(n => n.IsRead, true));

        return NoContent();
    }
}
