namespace Volts.Api.DTOs;

public class UnitOfMeasureCreateDto
{
    public string Code { get; set; } =
        string.Empty;

    public string SingularName { get; set; } =
        string.Empty;

    public string PluralName { get; set; } =
        string.Empty;

    public string Symbol { get; set; } =
        string.Empty;

    public bool AllowsDecimals { get; set; }

    public int DecimalPlaces { get; set; }
}

public class UnitOfMeasureUpdateDto :
    UnitOfMeasureCreateDto
{
    public bool IsActive { get; set; } = true;
}