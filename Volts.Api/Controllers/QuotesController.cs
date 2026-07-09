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
public class QuotesController : ControllerBase
{
    private readonly MongoDbService _db;

    public QuotesController(MongoDbService db)
    {
        _db = db;
    }

    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> Create(QuoteCreateDto dto)
    {
        if (dto.Quantity <= 0)
            return BadRequest(ApiResponse<Quote>.Fail("La cantidad debe ser mayor a 0"));

        var total = dto.Quantity * dto.UnitPrice + dto.Shipping;

        var quote = new Quote
        {
            FullName = dto.FullName,
            Email = dto.Email.ToLower(),
            Phone = dto.Phone,
            InstitutionName = dto.InstitutionName,
            PlanName = dto.PlanName,
            Quantity = dto.Quantity,
            UnitPrice = dto.UnitPrice,
            Shipping = dto.Shipping,
            Total = total,
            Notes = dto.Notes,
            Status = "Pending"
        };

        await _db.Quotes.InsertOneAsync(quote);

        return Ok(ApiResponse<Quote>.Ok(quote, "Cotización enviada correctamente"));
    }

    [HttpGet]
    [Authorize(Roles = "Admin,Employee")]
    public async Task<IActionResult> GetAll()
    {
        var quotes = await _db.Quotes
            .Find(x => !x.IsDeleted)
            .SortByDescending(x => x.CreatedAt)
            .ToListAsync();

        return Ok(ApiResponse<List<Quote>>.Ok(quotes));
    }

    [HttpPut("{id}/status")]
    [Authorize(Roles = "Admin,Employee")]
    public async Task<IActionResult> UpdateStatus(string id, [FromQuery] string status)
    {
        var update = Builders<Quote>.Update
            .Set(x => x.Status, status)
            .Set(x => x.UpdatedAt, DateTime.UtcNow);

        var result = await _db.Quotes.UpdateOneAsync(x => x.Id == id && !x.IsDeleted, update);

        if (result.ModifiedCount == 0)
            return NotFound(ApiResponse<string>.Fail("Cotización no encontrada"));

        return Ok(ApiResponse<string>.Ok("Estado actualizado correctamente"));
    }
}