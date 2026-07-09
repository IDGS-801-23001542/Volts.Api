namespace Volts.Api.DTOs;

public class CommentCreateDto
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public int Rating { get; set; }
}

public class CommentApprovalDto
{
    public bool IsApproved { get; set; }
}