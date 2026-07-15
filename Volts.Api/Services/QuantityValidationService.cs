using Volts.Api.Models;

namespace Volts.Api.Services;

public static class QuantityValidationService
{
    /*
     * Valida una cantidad utilizando la configuración
     * de su unidad de medida.
     *
     * positiveRequired:
     * true  -> debe ser mayor a cero.
     * false -> puede ser cero, pero nunca negativa.
     */
    public static string? ValidateQuantity(
        decimal quantity,
        UnitOfMeasure unit,
        string fieldName,
        bool positiveRequired = true)
    {
        if (positiveRequired)
        {
            if (quantity <= 0)
            {
                return
                    $"{fieldName} debe ser mayor a cero";
            }
        }
        else if (quantity < 0)
        {
            return
                $"{fieldName} no puede ser negativa";
        }

        if (!unit.AllowsDecimals &&
            decimal.Truncate(quantity) != quantity)
        {
            return
                $"{fieldName} debe ser un número entero " +
                $"porque la unidad {unit.Symbol} no permite decimales";
        }

        var actualDecimalPlaces =
            GetDecimalPlaces(quantity);

        if (actualDecimalPlaces >
            unit.DecimalPlaces)
        {
            return
                $"{fieldName} permite como máximo " +
                $"{unit.DecimalPlaces} decimales para la unidad " +
                $"{unit.Symbol}";
        }

        return null;
    }

    /*
     * Valida cantidades enteras de productos
     * terminados.
     *
     * Se utiliza para:
     * - Producción planeada.
     * - Unidades buenas.
     * - Unidades defectuosas.
     * - Stock físico.
     * - Stock reservado.
     * - Pedidos.
     * - Ventas.
     * - Licencias.
     */
    public static string? ValidateWholeQuantity(
        int quantity,
        string fieldName,
        bool positiveRequired = true)
    {
        if (positiveRequired)
        {
            if (quantity <= 0)
            {
                return
                    $"{fieldName} debe ser mayor a cero";
            }
        }
        else if (quantity < 0)
        {
            return
                $"{fieldName} no puede ser negativa";
        }

        return null;
    }

    public static string? ValidateCost(
        decimal cost,
        string fieldName,
        bool positiveRequired = false)
    {
        if (positiveRequired)
        {
            if (cost <= 0)
            {
                return
                    $"{fieldName} debe ser mayor a cero";
            }
        }
        else if (cost < 0)
        {
            return
                $"{fieldName} no puede ser negativo";
        }

        if (GetDecimalPlaces(cost) >
            InventoryRoundingService
                .UnitCostDecimalPlaces)
        {
            return
                $"{fieldName} permite como máximo " +
                $"{InventoryRoundingService.UnitCostDecimalPlaces} " +
                "decimales";
        }

        return null;
    }

    public static string? ValidatePercentage(
        decimal percentage,
        string fieldName,
        decimal minimum = 0,
        decimal maximum = 100)
    {
        if (percentage < minimum ||
            percentage > maximum)
        {
            return
                $"{fieldName} debe estar entre " +
                $"{minimum} y {maximum}";
        }

        if (GetDecimalPlaces(percentage) > 4)
        {
            return
                $"{fieldName} permite como máximo 4 decimales";
        }

        return null;
    }

    public static int GetDecimalPlaces(
        decimal value)
    {
        value = Math.Abs(value);

        var bits = decimal.GetBits(value);

        return (bits[3] >> 16) & 0x7F;
    }
}