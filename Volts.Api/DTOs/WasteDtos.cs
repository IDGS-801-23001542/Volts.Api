namespace Volts.Api.DTOs;

public class WasteCreateDto
{
    public string RawMaterialId { get; set; } =
        string.Empty;

    public decimal Quantity { get; set; }

    public string Classification { get; set; } =
        "FinalWaste";

    public string Destination { get; set; } =
        "Pending";

    public string Reason { get; set; } =
        string.Empty;

    public string Notes { get; set; } =
        string.Empty;

    public decimal EstimatedRecoveryValue { get; set; }
}

public class WasteDispositionDto
{
    public decimal Quantity { get; set; }

    public string Action { get; set; } =
        string.Empty;

    public decimal RecoveredValue { get; set; }

    public string Notes { get; set; } =
        string.Empty;
}