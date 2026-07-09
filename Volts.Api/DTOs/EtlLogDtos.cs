namespace Volts.Api.DTOs;

public class EtlLogCreateDto
{
    public string ProcessName { get; set; } = string.Empty;
    public int RecordsProcessed { get; set; }
    public string Status { get; set; } = "Running";
    public string? ErrorMessage { get; set; }
}

public class EtlLogFinishDto
{
    public string Status { get; set; } = "Completed";
    public int RecordsProcessed { get; set; }
    public string? ErrorMessage { get; set; }
}