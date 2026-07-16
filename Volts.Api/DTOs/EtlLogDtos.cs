namespace Volts.Api.DTOs;

public class EtlLogCreateDto
{
    public string ProcessName { get; set; } = string.Empty;
    public string Source { get; set; } = "MongoDB";
    public string Destination { get; set; } = "Analytics";
}

public class EtlLogFinishDto
{
    public string Status { get; set; } = "Completed";
    public int RecordsRead { get; set; }
    public int RecordsProcessed { get; set; }
    public int RecordsRejected { get; set; }
    public List<string> Findings { get; set; } = new();
    public string? ErrorMessage { get; set; }
}
