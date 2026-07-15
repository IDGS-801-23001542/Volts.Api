using System.Text.Json;
using Volts.Api.Models;

namespace Volts.Api.Services;

public class AuditTrailService
{
    private readonly MongoDbService _db;
    private readonly SensitiveDataSanitizer _sanitizer;

    public AuditTrailService(MongoDbService db, SensitiveDataSanitizer sanitizer)
    {
        _db = db;
        _sanitizer = sanitizer;
    }

    public async Task WriteAsync(
        string? userId,
        string? userName,
        string? roleName,
        string actorType,
        string area,
        string module,
        string action,
        string entityType,
        string description,
        int statusCode,
        string httpMethod,
        string path,
        string correlationId,
        string? ipAddress = null,
        string? userAgent = null,
        string? entityId = null,
        string? entityFolio = null,
        long durationMs = 0,
        object? requestData = null,
        object? responseData = null)
    {
        var resolvedName = !string.IsNullOrWhiteSpace(userName)
            ? userName
            : actorType == "System" ? "Sistema VOLTS" : "Visitante público";

        var log = new AuditLog
        {
            UserId = userId,
            UserName = resolvedName,
            RoleName = string.IsNullOrWhiteSpace(roleName)
                ? actorType == "System" ? "System" : "Public"
                : roleName,
            ActorType = actorType,
            Area = area,
            Module = module,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            EntityFolio = entityFolio,
            Result = statusCode >= 400 ? "Failed" : "Successful",
            Description = description,
            HttpMethod = httpMethod,
            Path = path,
            StatusCode = statusCode,
            DurationMs = durationMs,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            CorrelationId = correlationId,
            RequestData = Serialize(requestData),
            ResponseData = Serialize(responseData),
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId
        };

        try { await _db.AuditLogs.InsertOneAsync(log); }
        catch { /* Audit must never break business operations. */ }
    }

    private string? Serialize(object? value)
    {
        if (value == null) return null;
        return _sanitizer.Sanitize(JsonSerializer.Serialize(value));
    }
}
