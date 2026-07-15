namespace Volts.Api.DTOs;

public class AuditLogQueryDto
{
    public string? Search { get; set; }
    public string? Module { get; set; }
    public string? Action { get; set; }
    public string? UserId { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}

public class SystemLogQueryDto
{
    public string? Search { get; set; }
    public string? Level { get; set; }
    public string? Source { get; set; }
    public int? StatusCode { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}

public class PaginatedResultDto<T>
{
    public List<T> Items { get; set; } = new();
    public long Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}

public class DeleteOldLogsDto
{
    public int OlderThanDays { get; set; } = 90;
}
