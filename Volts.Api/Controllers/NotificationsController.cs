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
    private readonly NotificationDispatchService _dispatch;

    public NotificationsController(MongoDbService db, NotificationDispatchService dispatch)
    {
        _db = db;
        _dispatch = dispatch;
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetMine()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var now = DateTime.UtcNow;
        var notifications = await _db.Notifications.Find(item =>
            item.UserId == userId && !item.IsDeleted &&
            (!item.ExpiresAt.HasValue || item.ExpiresAt > now))
            .SortByDescending(item => item.CreatedAt).Limit(100).ToListAsync();
        return Ok(ApiResponse<List<Notification>>.Ok(notifications));
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetAll()
    {
        var notifications = await _db.Notifications.Find(item => !item.IsDeleted)
            .SortByDescending(item => item.CreatedAt).Limit(500).ToListAsync();
        return Ok(ApiResponse<List<Notification>>.Ok(notifications, "Notificaciones obtenidas correctamente"));
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create(NotificationCreateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Title) || string.IsNullOrWhiteSpace(dto.Message))
            return BadRequest(ApiResponse<string>.Fail("El título y el mensaje son obligatorios."));
        if (string.IsNullOrWhiteSpace(dto.UserId) && string.IsNullOrWhiteSpace(dto.TargetRole))
            return BadRequest(ApiResponse<string>.Fail("Selecciona un usuario o un rol destinatario."));

        var actorId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var count = 0;
        if (!string.IsNullOrWhiteSpace(dto.UserId))
        {
            await _dispatch.NotifyUserAsync(dto.UserId, dto.Title.Trim(), dto.Message.Trim(), dto.Type,
                dto.Priority, dto.Module, dto.Route, dto.EntityType, dto.EntityId, dto.EntityFolio, actorId);
            count = 1;
        }
        else
        {
            count = await _dispatch.NotifyRolesAsync(new[]{dto.TargetRole!}, dto.Title.Trim(), dto.Message.Trim(),
                dto.Type, dto.Priority, dto.Module, dto.Route, dto.EntityType, dto.EntityId, dto.EntityFolio, actorId);
        }
        return Ok(ApiResponse<int>.Ok(count, $"Notificación enviada a {count} destinatario(s)."));
    }

    [HttpPut("{id}/read")]
    public async Task<IActionResult> MarkAsRead(string id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var result = await _db.Notifications.UpdateOneAsync(
            item => item.Id == id && item.UserId == userId && !item.IsDeleted,
            Builders<Notification>.Update.Set(item => item.IsRead, true)
                .Set(item => item.ReadAt, DateTime.UtcNow).Set(item => item.UpdatedAt, DateTime.UtcNow));
        if (result.MatchedCount == 0) return NotFound(ApiResponse<string>.Fail("Notificación no encontrada."));
        return Ok(ApiResponse<string>.Ok(id, "Notificación marcada como leída."));
    }

    [HttpPut("read-all")]
    public async Task<IActionResult> MarkAllAsRead()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var result = await _db.Notifications.UpdateManyAsync(
            item => item.UserId == userId && !item.IsDeleted && !item.IsRead,
            Builders<Notification>.Update.Set(item => item.IsRead, true)
                .Set(item => item.ReadAt, DateTime.UtcNow).Set(item => item.UpdatedAt, DateTime.UtcNow));
        return Ok(ApiResponse<long>.Ok(result.ModifiedCount, "Notificaciones marcadas como leídas."));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var result = await _db.Notifications.UpdateOneAsync(
            item => item.Id == id && item.UserId == userId && !item.IsDeleted,
            Builders<Notification>.Update.Set(item => item.IsDeleted, true)
                .Set(item => item.UpdatedAt, DateTime.UtcNow));
        if (result.MatchedCount == 0) return NotFound(ApiResponse<string>.Fail("Notificación no encontrada."));
        return Ok(ApiResponse<string>.Ok(id, "Notificación eliminada."));
    }
}
