using Volts.Api.Models.Enums;

namespace Volts.Api.DTOs;

public class WasteCreateDto
{
    public string RawMaterialId { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public WasteClassification Classification { get; set; }
    public WasteDestination Destination { get; set; } = WasteDestination.Pending;
    public decimal EstimatedRecoveryValue { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
}

public class WasteDispositionDto
{
    public WasteDestination Action { get; set; }
    public decimal Quantity { get; set; }
    public decimal RecoveredValue { get; set; }
    public string Notes { get; set; } = string.Empty;
}
