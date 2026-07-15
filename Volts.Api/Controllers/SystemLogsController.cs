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
public class SystemLogsController : ControllerBase
{
    private readonly MongoDbService _db;

    public SystemLogsController(MongoDbService db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] SystemLogQueryDto query)
    {
        query.Page = Math.Max(1, query.Page);
        query.PageSize =
            Math.Clamp(query.PageSize, 1, 100);

        var filter =
            Builders<SystemLog>.Filter.Eq(
                item => item.IsDeleted,
                false
            );

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var regex =
                new MongoDB.Bson.BsonRegularExpression(
                    System.Text.RegularExpressions
                        .Regex.Escape(
                            query.Search.Trim()
                        ),
                    "i"
                );

            filter &=
                Builders<SystemLog>.Filter.Or(
                    Builders<SystemLog>.Filter.Regex(
                        item => item.Message,
                        regex
                    ),
                    Builders<SystemLog>.Filter.Regex(
                        item => item.Source,
                        regex
                    ),
                    Builders<SystemLog>.Filter.Regex(
                        item => item.Path,
                        regex
                    ),
                    Builders<SystemLog>.Filter.Regex(
                        item => item.CorrelationId,
                        regex
                    )
                );
        }

        if (!string.IsNullOrWhiteSpace(query.Level))
            filter &= Builders<SystemLog>.Filter.Eq(
                item => item.Level,
                query.Level
            );

        if (!string.IsNullOrWhiteSpace(query.Source))
            filter &= Builders<SystemLog>.Filter.Eq(
                item => item.Source,
                query.Source
            );

        if (query.StatusCode.HasValue)
            filter &= Builders<SystemLog>.Filter.Eq(
                item => item.StatusCode,
                query.StatusCode.Value
            );

        if (query.From.HasValue)
            filter &= Builders<SystemLog>.Filter.Gte(
                item => item.CreatedAt,
                query.From.Value
            );

        if (query.To.HasValue)
            filter &= Builders<SystemLog>.Filter.Lt(
                item => item.CreatedAt,
                query.To.Value.Date.AddDays(1)
            );

        var total = await _db.SystemLogs
            .CountDocumentsAsync(filter);

        var items = await _db.SystemLogs
            .Find(filter)
            .SortByDescending(item => item.CreatedAt)
            .Skip(
                (query.Page - 1) *
                query.PageSize
            )
            .Limit(query.PageSize)
            .ToListAsync();

        return Ok(
            ApiResponse<
                PaginatedResultDto<SystemLog>
            >.Ok(
                new PaginatedResultDto<SystemLog>
                {
                    Items = items,
                    Total = total,
                    Page = query.Page,
                    PageSize = query.PageSize,
                    TotalPages = (int)Math.Ceiling(
                        total /
                        (double)query.PageSize
                    )
                },
                "Logs obtenidos correctamente"
            )
        );
    }

    [HttpDelete("old")]
    public async Task<IActionResult> DeleteOld(
        [FromBody] DeleteOldLogsDto dto)
    {
        if (dto.OlderThanDays < 30)
        {
            return BadRequest(
                ApiResponse<string>.Fail(
                    "Por seguridad, solo pueden eliminarse logs con más de 30 días"
                )
            );
        }

        var cutoff =
            DateTime.UtcNow.AddDays(
                -dto.OlderThanDays
            );

        var result =
            await _db.SystemLogs.DeleteManyAsync(
                item =>
                    item.CreatedAt < cutoff
            );

        return Ok(
            ApiResponse<string>.Ok(
                $"{result.DeletedCount} logs antiguos eliminados"
            )
        );
    }
}
