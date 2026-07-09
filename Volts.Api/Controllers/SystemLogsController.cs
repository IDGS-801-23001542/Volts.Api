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
public class SystemLogsController : ControllerBase
{
    private readonly MongoDbService _db;

    public SystemLogsController(MongoDbService db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var logs = await _db.SystemLogs
            .Find(x => !x.IsDeleted)
            .SortByDescending(x => x.EventDate)
            .Limit(500)
            .ToListAsync();

        return Ok(ApiResponse<List<SystemLog>>.Ok(logs));
    }
}