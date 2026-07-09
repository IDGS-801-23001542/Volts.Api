using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using Volts.Api.Models;
using Volts.Api.Responses;
using Volts.Api.Services;

namespace Volts.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class AuditLogsController : ControllerBase
{
    private readonly MongoDbService _db;

    public AuditLogsController(MongoDbService db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var logs = await _db.AuditLogs
            .Find(x => !x.IsDeleted)
            .SortByDescending(x => x.EventDate)
            .Limit(500)
            .ToListAsync();

        return Ok(ApiResponse<List<AuditLog>>.Ok(logs));
    }
}