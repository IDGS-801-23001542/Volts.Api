using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using Volts.Api.DTOs;
using Volts.Api.Models;
using Volts.Api.Responses;
using Volts.Api.Services;

namespace Volts.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly MongoDbService _db;

    public NotificationsController(MongoDbService db)
    {
        _db = db;
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetMine()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        var notifications = await _db.Notifications
            .Find(x => x.UserId == userId && !x.IsDeleted)
            .SortByDescending(x => x.CreatedAt)
            .ToListAsync();

        return Ok(ApiResponse<List<Notification>>.Ok(notifications));
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create(NotificationCreateDto dto)
    {
        var userExists = await _db.Users.Find(x => x.Id == dto.UserId && !x.IsDeleted).AnyAsync();

        if (!userExists)
            return BadRequest(ApiResponse<Notification>.Fail("Usuario no encontrado"));

        var notification = new Notification
        {
            UserId = dto.UserId,
            Title = dto.Title,
            Message = dto.Message,
            IsRead = false
        };

        await _db.Notifications.InsertOneAsync(notification);

        return Ok(ApiResponse<Notification>.Ok(notification, "Notificación creada correctamente"));
    }

    [HttpPut("{id}/read")]
    public async Task<IActionResult> MarkAsRead(string id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        var update = Builders<Notification>.Update
            .Set(x => x.IsRead, true)
            .Set(x => x.UpdatedAt, DateTime.UtcNow);

        var result = await _db.Notifications.UpdateOneAsync(
            x => x.Id == id && x.UserId == userId && !x.IsDeleted,
            update
        );

        if (result.ModifiedCount == 0)
            return NotFound(ApiResponse<string>.Fail("Notificación no encontrada"));

        return Ok(ApiResponse<string>.Ok("Notificación marcada como leída"));
    }
}