namespace Volts.Api.DTOs;

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
