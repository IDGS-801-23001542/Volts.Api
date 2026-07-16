namespace Volts.Api.DTOs;

public class ContactCreateDto
{
    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public string Subject { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;
}

public class ContactStatusDto
{
    public string Status { get; set; } = string.Empty;
}