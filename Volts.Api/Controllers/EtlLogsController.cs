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
[Authorize(Roles = "Admin")]
public class EtlLogsController : ControllerBase
{
    private readonly MongoDbService _db;

    public EtlLogsController(MongoDbService db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var logs = await _db.EtlLogs
            .Find(x => !x.IsDeleted)
            .SortByDescending(x => x.StartedAt)
            .Limit(500)
            .ToListAsync();

        return Ok(ApiResponse<List<EtlLog>>.Ok(logs));
    }

    [HttpPost]
    public async Task<IActionResult> Create(EtlLogCreateDto dto)
    {
        var log = new EtlLog
        {
            ProcessName = dto.ProcessName,
            StartedAt = DateTime.UtcNow,
            Status = dto.Status,
            RecordsProcessed = dto.RecordsProcessed,
            ErrorMessage = dto.ErrorMessage
        };

        await _db.EtlLogs.InsertOneAsync(log);

        return Ok(ApiResponse<EtlLog>.Ok(log, "Log ETL creado correctamente"));
    }

    [HttpPut("{id}/finish")]
    public async Task<IActionResult> Finish(string id, EtlLogFinishDto dto)
    {
        var update = Builders<EtlLog>.Update
            .Set(x => x.FinishedAt, DateTime.UtcNow)
            .Set(x => x.Status, dto.Status)
            .Set(x => x.RecordsProcessed, dto.RecordsProcessed)
            .Set(x => x.ErrorMessage, dto.ErrorMessage)
            .Set(x => x.UpdatedAt, DateTime.UtcNow);

        var result = await _db.EtlLogs.UpdateOneAsync(x => x.Id == id && !x.IsDeleted, update);

        if (result.ModifiedCount == 0)
            return NotFound(ApiResponse<string>.Fail("Log ETL no encontrado"));

        return Ok(ApiResponse<string>.Ok("Log ETL finalizado correctamente"));
    }
}