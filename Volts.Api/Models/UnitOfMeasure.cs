using MongoDB.Bson.Serialization.Attributes;

namespace Volts.Api.Models;

[BsonIgnoreExtraElements]
public class UnitOfMeasure : BaseEntity
{
    /*
     * Código técnico estable utilizado por backend,
     * frontend, materia prima, compras, recetas,
     * producción y merma.
     *
     * Ejemplos:
     * Piece
     * Kilogram
     * Meter
     * Milliliter
     */
    public string Code { get; set; } = string.Empty;

    /*
     * Nombre mostrado cuando la cantidad representa
     * exactamente una unidad.
     *
     * Ejemplo:
     * 1 Pieza
     * 1 Kilogramo
     */
    public string SingularName { get; set; } =
        string.Empty;

    /*
     * Nombre mostrado cuando la cantidad es distinta
     * de uno.
     *
     * Ejemplo:
     * 4 Piezas
     * 2.5 Kilogramos
     */
    public string PluralName { get; set; } =
        string.Empty;

    /*
     * Símbolo corto para interfaces, movimientos,
     * recetas y reportes.
     *
     * Ejemplos:
     * pza
     * kg
     * m
     * ml
     */
    public string Symbol { get; set; } =
        string.Empty;

    /*
     * Define si la unidad acepta cantidades
     * fraccionarias.
     *
     * false:
     * Piece, Unit, Kit, Sheet
     *
     * true:
     * Kilogram, Gram, Liter, Milliliter,
     * Meter, SquareMeter
     */
    public bool AllowsDecimals { get; set; }

    /*
     * Cantidad máxima de decimales permitidos.
     *
     * Para unidades discretas siempre debe ser 0.
     * Para unidades continuas se utilizarán hasta 4.
     */
    public int DecimalPlaces { get; set; }

    /*
     * Permite desactivar una unidad sin eliminar su
     * historial operativo.
     */
    public bool IsActive { get; set; } = true;
}