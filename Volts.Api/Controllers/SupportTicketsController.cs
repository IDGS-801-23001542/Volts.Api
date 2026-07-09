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
public class SupportTicketsController : ControllerBase
{
    private readonly MongoDbService _db;

    public SupportTicketsController(MongoDbService db)
    {
        _db = db;
    }

    [HttpGet]
    [Authorize(Roles = "Admin,Employee")]
    public async Task<IActionResult> GetAll()
    {
        var tickets = await _db.SupportTickets
            .Find(x => !x.IsDeleted)
            .SortByDescending(x => x.CreatedAt)
            .ToListAsync();

        return Ok(ApiResponse<List<SupportTicket>>.Ok(tickets));
    }

    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> Create(SupportTicketCreateDto dto)
    {
        var customer = await _db.Customers
            .Find(x => x.Id == dto.CustomerId && !x.IsDeleted)
            .FirstOrDefaultAsync();

        var ticket = new SupportTicket
        {
            CustomerId = dto.CustomerId,
            CustomerName = customer?.FullName ?? "Cliente no registrado",
            Email = dto.Email.ToLower(),
            Subject = dto.Subject,
            Description = dto.Description,
            Priority = dto.Priority,
            Status = "Open"
        };

        await _db.SupportTickets.InsertOneAsync(ticket);

        return Ok(ApiResponse<SupportTicket>.Ok(ticket, "Ticket creado correctamente"));
    }

    [HttpPut("{id}/status")]
    [Authorize(Roles = "Admin,Employee")]
    public async Task<IActionResult> UpdateStatus(string id, SupportTicketStatusDto dto)
    {
        var allowed = new[] { "Open", "InProgress", "Resolved", "Closed" };

        if (!allowed.Contains(dto.Status))
            return BadRequest(ApiResponse<string>.Fail("Estado inválido"));

        var update = Builders<SupportTicket>.Update
            .Set(x => x.Status, dto.Status)
            .Set(x => x.UpdatedAt, DateTime.UtcNow);

        var result = await _db.SupportTickets.UpdateOneAsync(x => x.Id == id && !x.IsDeleted, update);

        if (result.ModifiedCount == 0)
            return NotFound(ApiResponse<string>.Fail("Ticket no encontrado"));

        return Ok(ApiResponse<string>.Ok("Estado del ticket actualizado correctamente"));
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(string id)
    {
        var update = Builders<SupportTicket>.Update
            .Set(x => x.IsDeleted, true)
            .Set(x => x.UpdatedAt, DateTime.UtcNow);

        var result = await _db.SupportTickets.UpdateOneAsync(x => x.Id == id && !x.IsDeleted, update);

        if (result.ModifiedCount == 0)
            return NotFound(ApiResponse<string>.Fail("Ticket no encontrado"));

        return Ok(ApiResponse<string>.Ok("Ticket eliminado correctamente"));
    }
}