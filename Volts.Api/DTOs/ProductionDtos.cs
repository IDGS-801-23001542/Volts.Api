namespace Volts.Api.DTOs;

public class ProductionCreateDto
{
    public string ProductId { get; set; } =
        string.Empty;

    public int Quantity { get; set; }

    public string Notes { get; set; } =
        string.Empty;
}

public class ProductionCompleteDto
{
    public int QuantityCompleted { get; set; }

    public int QuantityDefective { get; set; }

    public string Notes { get; set; } =
        string.Empty;

    public List<ProductionWasteDto> Wastes { get; set; } =
        new();
}

public class ProductionWasteDto
{
    public string RawMaterialId { get; set; } =
        string.Empty;

    public decimal Quantity { get; set; }

    public string Classification { get; set; } =
        "Reusable";

    public string Destination { get; set; } =
        "Pending";

    public decimal EstimatedRecoveryValue { get; set; }

    public string Notes { get; set; } =
        string.Empty;
}

public class ProductionCancelDto
{
    public string Reason { get; set; } =
        string.Empty;
}