using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using Volts.Api.DTOs;
using Volts.Api.Models;
using Volts.Api.Responses;
using Volts.Api.Services;

namespace Volts.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class AuditController : ControllerBase
{
    private readonly MongoDbService _db;

    public AuditController(MongoDbService db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] AuditLogQueryDto query)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var filter = Builders<AuditLog>.Filter.Eq(
            item => item.IsDeleted,
            false
        );

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var regex = new BsonRegularExpression(
                System.Text.RegularExpressions.Regex.Escape(
                    query.Search.Trim()
                ),
                "i"
            );

            filter &= Builders<AuditLog>.Filter.Or(
                Builders<AuditLog>.Filter.Regex(
                    nameof(AuditLog.UserName),
                    regex
                ),
                Builders<AuditLog>.Filter.Regex(
                    nameof(AuditLog.Description),
                    regex
                ),
                Builders<AuditLog>.Filter.Regex(
                    nameof(AuditLog.EntityType),
                    regex
                ),
                Builders<AuditLog>.Filter.Regex(
                    nameof(AuditLog.EntityFolio),
                    regex
                ),
                Builders<AuditLog>.Filter.Regex(
                    nameof(AuditLog.EntityId),
                    regex
                ),
                Builders<AuditLog>.Filter.Regex(
                    nameof(AuditLog.Path),
                    regex
                )
            );
        }

        if (!string.IsNullOrWhiteSpace(query.Module))
        {
            filter &= Builders<AuditLog>.Filter.Eq(
                item => item.Module,
                query.Module.Trim()
            );
        }

        if (!string.IsNullOrWhiteSpace(query.Action))
        {
            filter &= Builders<AuditLog>.Filter.Eq(
                item => item.Action,
                query.Action.Trim()
            );
        }

        if (!string.IsNullOrWhiteSpace(query.UserId))
        {
            filter &= Builders<AuditLog>.Filter.Eq(
                item => item.UserId,
                query.UserId.Trim()
            );
        }

        if (query.From.HasValue)
        {
            filter &= Builders<AuditLog>.Filter.Gte(
                item => item.CreatedAt,
                query.From.Value
            );
        }

        if (query.To.HasValue)
        {
            filter &= Builders<AuditLog>.Filter.Lt(
                item => item.CreatedAt,
                query.To.Value.Date.AddDays(1)
            );
        }

        var total = await _db.AuditLogs
            .CountDocumentsAsync(filter);

        var items = await _db.AuditLogs
            .Find(filter)
            .SortByDescending(item => item.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync();

        return Ok(
            ApiResponse<PaginatedResultDto<AuditLog>>.Ok(
                new PaginatedResultDto<AuditLog>
                {
                    Items = items,
                    Total = total,
                    Page = page,
                    PageSize = pageSize,
                    TotalPages = total == 0
                        ? 0
                        : (int)Math.Ceiling(
                            total / (double)pageSize
                        )
                },
                "Auditoría obtenida correctamente"
            )
        );
    }

    [HttpGet("modules")]
    public async Task<IActionResult> GetModules()
    {
        var modules = await _db.AuditLogs
            .Distinct<string>(
                nameof(AuditLog.Module),
                Builders<AuditLog>.Filter.Eq(
                    item => item.IsDeleted,
                    false
                )
            )
            .ToListAsync();

        return Ok(
            ApiResponse<List<string>>.Ok(
                modules
                    .Where(item =>
                        !string.IsNullOrWhiteSpace(item))
                    .Distinct(
                        StringComparer.OrdinalIgnoreCase)
                    .OrderBy(item => item)
                    .ToList(),
                "Módulos obtenidos correctamente"
            )
        );
    }
}
