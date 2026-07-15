namespace Volts.Api.Services;

public static class InventoryRoundingService
{
    /*
     * Cantidades continuas:
     * kg, g, L, ml, m, m².
     */
    public const int QuantityDecimalPlaces = 4;

    /*
     * Costos unitarios y costos promedio.
     *
     * Se mantiene mayor precisión porque puede haber
     * materiales con costo muy pequeño por ml, gramo
     * o metro.
     */
    public const int UnitCostDecimalPlaces = 6;

    /*
     * Costos estimados de receta y producción.
     */
    public const int EstimatedCostDecimalPlaces = 4;

    /*
     * Importes financieros finales:
     * subtotal, impuesto, envío y total.
     */
    public const int MoneyDecimalPlaces = 2;

    public static decimal RoundQuantity(
        decimal value,
        int decimalPlaces =
            QuantityDecimalPlaces)
    {
        return decimal.Round(
            value,
            decimalPlaces,
            MidpointRounding.AwayFromZero
        );
    }

    public static decimal RoundUnitCost(
        decimal value)
    {
        return decimal.Round(
            value,
            UnitCostDecimalPlaces,
            MidpointRounding.AwayFromZero
        );
    }

    public static decimal RoundEstimatedCost(
        decimal value)
    {
        return decimal.Round(
            value,
            EstimatedCostDecimalPlaces,
            MidpointRounding.AwayFromZero
        );
    }

    public static decimal RoundMoney(
        decimal value)
    {
        return decimal.Round(
            value,
            MoneyDecimalPlaces,
            MidpointRounding.AwayFromZero
        );
    }
}