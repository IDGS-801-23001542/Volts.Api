namespace Volts.Api.DTOs;

public class NotificationCreateDto
{
    public string? UserId { get; set; }
    public string? TargetRole { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Type { get; set; } = "Information";
    public string Priority { get; set; } = "Normal";
    public string Module { get; set; } = "General";
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
    public string? EntityFolio { get; set; }
    public string? Route { get; set; }
    public DateTime? ExpiresAt { get; set; }
}
