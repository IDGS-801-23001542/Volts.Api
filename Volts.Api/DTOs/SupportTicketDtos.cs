namespace Volts.Api.DTOs;

public class SupportTicketCreateDto
{
    public string CustomerId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Priority { get; set; } = "Medium";
}

public class SupportTicketStatusDto
{
    public string Status { get; set; } = string.Empty;
}