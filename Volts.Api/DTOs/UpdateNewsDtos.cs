namespace Volts.Api.DTOs;

public class UpdateNewsCreateDto
{
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Platform { get; set; } = "Android";
    public bool IsPublished { get; set; } = true;
}

public class UpdateNewsUpdateDto : UpdateNewsCreateDto
{
}