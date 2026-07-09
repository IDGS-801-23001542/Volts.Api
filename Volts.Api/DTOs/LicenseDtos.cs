namespace Volts.Api.DTOs;

public class LicenseCreateDto
{
    public string CustomerId { get; set; } = string.Empty;
    public string? InstitutionId { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public DateTime? ExpirationDate { get; set; }
}

public class LicenseAssignDto
{
    public string AssignedToName { get; set; } = string.Empty;
    public string? AssignedToEmail { get; set; }
    public string? DeviceSerialNumber { get; set; }
}

public class LicenseStatusUpdateDto
{
    public string Status { get; set; } = string.Empty;
}