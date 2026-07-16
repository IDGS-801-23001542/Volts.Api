using System.Security.Claims;
using System.Text.Json;
using Volts.Api.Models;
using Volts.Api.Services;

namespace Volts.Api.Middleware;

public class ExceptionLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionLoggingMiddleware>
        _logger;

    public ExceptionLoggingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(
        HttpContext context,
        MongoDbService db,
        SensitiveDataSanitizer sanitizer)
    {
        try
        {
            await _next(context);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Unhandled exception {Method} {Path}",
                context.Request.Method,
                context.Request.Path
            );

            var log = new SystemLog
            {
                Level = "Error",
                Source =
                    exception.TargetSite
                        ?.DeclaringType?.FullName ??
                    "UnhandledException",
                Message = exception.Message,
                ExceptionType =
                    exception.GetType().FullName,
                StackTrace = exception.StackTrace,
                HttpMethod =
                    context.Request.Method,
                Path = context.Request.Path,
                StatusCode =
                    StatusCodes
                        .Status500InternalServerError,
                UserId =
                    context.User.FindFirstValue(
                        ClaimTypes.NameIdentifier
                    ),
                UserName =
                    context.User.FindFirstValue(
                        ClaimTypes.Name
                    ) ??
                    context.User.FindFirstValue(
                        ClaimTypes.Email
                    ),
                RoleName =
                    context.User.FindFirstValue(
                        ClaimTypes.Role
                    ),
                IpAddress =
                    context.Connection
                        .RemoteIpAddress?.ToString(),
                UserAgent =
                    context.Request.Headers
                        .UserAgent.ToString(),
                CorrelationId =
                    context.TraceIdentifier,
                AdditionalData =
                    sanitizer.Sanitize(
                        JsonSerializer.Serialize(
                            new
                            {
                                context.Request.QueryString,
                                RouteValues =
                                    context.Request
                                        .RouteValues
                            }
                        )
                    ),
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow,
                CreatedBy =
                    context.User.FindFirstValue(
                        ClaimTypes.NameIdentifier
                    )
            };

            try
            {
                await db.SystemLogs.InsertOneAsync(log);
            }
            catch (Exception loggingException)
            {
                _logger.LogError(
                    loggingException,
                    "Could not persist SystemLog"
                );
            }

            throw;
        }
    }
}
