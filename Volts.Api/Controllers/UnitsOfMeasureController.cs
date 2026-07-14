using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using Volts.Api.DTOs;
using Volts.Api.Models;
using Volts.Api.Responses;
using Volts.Api.Services;

namespace Volts.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin,Employee")]
public class UnitsOfMeasureController :
    ControllerBase
{
    private readonly MongoDbService _db;

    public UnitsOfMeasureController(
        MongoDbService db)
    {
        _db = db;
    }

    // =========================================================
    // GET: api/UnitsOfMeasure
    // Devuelve todas las unidades no eliminadas.
    // =========================================================
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var units =
            await _db.UnitsOfMeasure
                .Find(item => !item.IsDeleted)
                .SortBy(item => item.SingularName)
                .ToListAsync();

        return Ok(
            ApiResponse<List<UnitOfMeasure>>.Ok(
                units,
                "Unidades de medida obtenidas correctamente"
            )
        );
    }

    // =========================================================
    // GET: api/UnitsOfMeasure/active
    // Para selectores de materia prima, compras,
    // recetas, producción y merma.
    // =========================================================
    [HttpGet("active")]
    public async Task<IActionResult> GetActive()
    {
        var units =
            await _db.UnitsOfMeasure
                .Find(item =>
                    !item.IsDeleted &&
                    item.IsActive)
                .SortBy(item => item.SingularName)
                .ToListAsync();

        return Ok(
            ApiResponse<List<UnitOfMeasure>>.Ok(
                units,
                "Unidades activas obtenidas correctamente"
            )
        );
    }

    // =========================================================
    // GET: api/UnitsOfMeasure/{id}
    // =========================================================
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(
        string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return BadRequest(
                ApiResponse<UnitOfMeasure>.Fail(
                    "El identificador de la unidad es obligatorio"
                )
            );
        }

        var unit =
            await _db.UnitsOfMeasure
                .Find(item =>
                    item.Id == id &&
                    !item.IsDeleted)
                .FirstOrDefaultAsync();

        if (unit == null)
        {
            return NotFound(
                ApiResponse<UnitOfMeasure>.Fail(
                    "Unidad de medida no encontrada"
                )
            );
        }

        return Ok(
            ApiResponse<UnitOfMeasure>.Ok(
                unit,
                "Unidad de medida obtenida correctamente"
            )
        );
    }

    // =========================================================
    // POST: api/UnitsOfMeasure
    // Solo Admin.
    // =========================================================
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create(
        [FromBody] UnitOfMeasureCreateDto dto)
    {
        var validationError =
            ValidateUnit(dto);

        if (validationError != null)
        {
            return BadRequest(
                ApiResponse<UnitOfMeasure>.Fail(
                    validationError
                )
            );
        }

        var normalizedCode =
            NormalizeCode(dto.Code);

        var codeExists =
            await _db.UnitsOfMeasure
                .Find(item =>
                    item.Code.ToUpper() ==
                    normalizedCode.ToUpper() &&
                    !item.IsDeleted)
                .AnyAsync();

        if (codeExists)
        {
            return BadRequest(
                ApiResponse<UnitOfMeasure>.Fail(
                    "Ya existe una unidad con ese código"
                )
            );
        }

        var normalizedSymbol =
            dto.Symbol.Trim();

        var symbolExists =
            await _db.UnitsOfMeasure
                .Find(item =>
                    item.Symbol.ToLower() ==
                    normalizedSymbol.ToLower() &&
                    !item.IsDeleted)
                .AnyAsync();

        if (symbolExists)
        {
            return BadRequest(
                ApiResponse<UnitOfMeasure>.Fail(
                    "Ya existe una unidad con ese símbolo"
                )
            );
        }

        var unit = new UnitOfMeasure
        {
            Code = normalizedCode,

            SingularName =
                dto.SingularName.Trim(),

            PluralName =
                dto.PluralName.Trim(),

            Symbol =
                normalizedSymbol,

            AllowsDecimals =
                dto.AllowsDecimals,

            DecimalPlaces =
                dto.AllowsDecimals
                    ? dto.DecimalPlaces
                    : 0,

            IsActive = true,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = GetCurrentUserId()
        };

        await _db.UnitsOfMeasure
            .InsertOneAsync(unit);

        return CreatedAtAction(
            nameof(GetById),
            new
            {
                id = unit.Id
            },
            ApiResponse<UnitOfMeasure>.Ok(
                unit,
                "Unidad de medida creada correctamente"
            )
        );
    }

    // =========================================================
    // PUT: api/UnitsOfMeasure/{id}
    // Solo Admin.
    // =========================================================
    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(
        string id,
        [FromBody] UnitOfMeasureUpdateDto dto)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return BadRequest(
                ApiResponse<UnitOfMeasure>.Fail(
                    "El identificador de la unidad es obligatorio"
                )
            );
        }

        var validationError =
            ValidateUnit(dto);

        if (validationError != null)
        {
            return BadRequest(
                ApiResponse<UnitOfMeasure>.Fail(
                    validationError
                )
            );
        }

        var unit =
            await _db.UnitsOfMeasure
                .Find(item =>
                    item.Id == id &&
                    !item.IsDeleted)
                .FirstOrDefaultAsync();

        if (unit == null)
        {
            return NotFound(
                ApiResponse<UnitOfMeasure>.Fail(
                    "Unidad de medida no encontrada"
                )
            );
        }

        var normalizedCode =
            NormalizeCode(dto.Code);

        /*
         * Cuando la unidad ya está relacionada con
         * materias primas, no se puede modificar su
         * código técnico ni su naturaleza decimal.
         */
        var isUsed =
            await _db.RawMaterials
                .Find(material =>
                    !material.IsDeleted &&
                    (
                        material.Unit ==
                        unit.Code ||

                        material.Unit ==
                        unit.SingularName ||

                        material.Unit ==
                        unit.Symbol
                    ))
                .AnyAsync();

        if (isUsed)
        {
            if (!string.Equals(
                    unit.Code,
                    normalizedCode,
                    StringComparison.Ordinal))
            {
                return BadRequest(
                    ApiResponse<UnitOfMeasure>.Fail(
                        "No se puede cambiar el código de una unidad que ya está siendo utilizada"
                    )
                );
            }

            if (unit.AllowsDecimals !=
                dto.AllowsDecimals)
            {
                return BadRequest(
                    ApiResponse<UnitOfMeasure>.Fail(
                        "No se puede cambiar la regla de decimales de una unidad que ya está siendo utilizada"
                    )
                );
            }

            if (unit.DecimalPlaces !=
                (
                    dto.AllowsDecimals
                        ? dto.DecimalPlaces
                        : 0
                ))
            {
                return BadRequest(
                    ApiResponse<UnitOfMeasure>.Fail(
                        "No se puede cambiar la precisión de una unidad que ya está siendo utilizada"
                    )
                );
            }
        }

        var duplicateCode =
            await _db.UnitsOfMeasure
                .Find(item =>
                    item.Id != id &&
                    item.Code.ToUpper() ==
                    normalizedCode.ToUpper() &&
                    !item.IsDeleted)
                .AnyAsync();

        if (duplicateCode)
        {
            return BadRequest(
                ApiResponse<UnitOfMeasure>.Fail(
                    "Ya existe otra unidad con ese código"
                )
            );
        }

        var normalizedSymbol =
            dto.Symbol.Trim();

        var duplicateSymbol =
            await _db.UnitsOfMeasure
                .Find(item =>
                    item.Id != id &&
                    item.Symbol.ToLower() ==
                    normalizedSymbol.ToLower() &&
                    !item.IsDeleted)
                .AnyAsync();

        if (duplicateSymbol)
        {
            return BadRequest(
                ApiResponse<UnitOfMeasure>.Fail(
                    "Ya existe otra unidad con ese símbolo"
                )
            );
        }

        unit.Code = normalizedCode;

        unit.SingularName =
            dto.SingularName.Trim();

        unit.PluralName =
            dto.PluralName.Trim();

        unit.Symbol =
            normalizedSymbol;

        unit.AllowsDecimals =
            dto.AllowsDecimals;

        unit.DecimalPlaces =
            dto.AllowsDecimals
                ? dto.DecimalPlaces
                : 0;

        unit.IsActive =
            dto.IsActive;

        unit.UpdatedAt =
            DateTime.UtcNow;

        unit.UpdatedBy =
            GetCurrentUserId();

        await _db.UnitsOfMeasure
            .ReplaceOneAsync(
                item =>
                    item.Id == id &&
                    !item.IsDeleted,
                unit
            );

        return Ok(
            ApiResponse<UnitOfMeasure>.Ok(
                unit,
                "Unidad de medida actualizada correctamente"
            )
        );
    }

    // =========================================================
    // PATCH: api/UnitsOfMeasure/{id}/status
    // Solo Admin.
    // =========================================================
    [HttpPatch("{id}/status")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateStatus(
        string id,
        [FromQuery] bool isActive)
    {
        var unit =
            await _db.UnitsOfMeasure
                .Find(item =>
                    item.Id == id &&
                    !item.IsDeleted)
                .FirstOrDefaultAsync();

        if (unit == null)
        {
            return NotFound(
                ApiResponse<string>.Fail(
                    "Unidad de medida no encontrada"
                )
            );
        }

        if (!isActive)
        {
            var isUsedByActiveMaterial =
                await _db.RawMaterials
                    .Find(material =>
                        !material.IsDeleted &&
                        material.IsActive &&
                        (
                            material.Unit ==
                            unit.Code ||

                            material.Unit ==
                            unit.SingularName ||

                            material.Unit ==
                            unit.Symbol
                        ))
                    .AnyAsync();

            if (isUsedByActiveMaterial)
            {
                return BadRequest(
                    ApiResponse<string>.Fail(
                        "No se puede desactivar una unidad utilizada por materias primas activas"
                    )
                );
            }
        }

        var update =
            Builders<UnitOfMeasure>.Update
                .Set(
                    item => item.IsActive,
                    isActive
                )
                .Set(
                    item => item.UpdatedAt,
                    DateTime.UtcNow
                )
                .Set(
                    item => item.UpdatedBy,
                    GetCurrentUserId()
                );

        await _db.UnitsOfMeasure
            .UpdateOneAsync(
                item =>
                    item.Id == id &&
                    !item.IsDeleted,
                update
            );

        return Ok(
            ApiResponse<string>.Ok(
                isActive
                    ? "Unidad de medida activada correctamente"
                    : "Unidad de medida desactivada correctamente"
            )
        );
    }

    // =========================================================
    // DELETE: api/UnitsOfMeasure/{id}
    // Eliminación lógica. Solo Admin.
    // =========================================================
    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(
        string id)
    {
        var unit =
            await _db.UnitsOfMeasure
                .Find(item =>
                    item.Id == id &&
                    !item.IsDeleted)
                .FirstOrDefaultAsync();

        if (unit == null)
        {
            return NotFound(
                ApiResponse<string>.Fail(
                    "Unidad de medida no encontrada"
                )
            );
        }

        var isUsed =
            await _db.RawMaterials
                .Find(material =>
                    !material.IsDeleted &&
                    (
                        material.Unit ==
                        unit.Code ||

                        material.Unit ==
                        unit.SingularName ||

                        material.Unit ==
                        unit.Symbol
                    ))
                .AnyAsync();

        if (isUsed)
        {
            return BadRequest(
                ApiResponse<string>.Fail(
                    "No se puede eliminar una unidad con materias primas relacionadas. Puedes desactivarla."
                )
            );
        }

        var update =
            Builders<UnitOfMeasure>.Update
                .Set(
                    item => item.IsDeleted,
                    true
                )
                .Set(
                    item => item.IsActive,
                    false
                )
                .Set(
                    item => item.UpdatedAt,
                    DateTime.UtcNow
                )
                .Set(
                    item => item.UpdatedBy,
                    GetCurrentUserId()
                );

        await _db.UnitsOfMeasure
            .UpdateOneAsync(
                item =>
                    item.Id == id &&
                    !item.IsDeleted,
                update
            );

        return Ok(
            ApiResponse<string>.Ok(
                "Unidad de medida eliminada correctamente"
            )
        );
    }

    private static string? ValidateUnit(
        UnitOfMeasureCreateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Code))
        {
            return
                "El código de la unidad es obligatorio";
        }

        if (dto.Code.Trim().Length < 2)
        {
            return
                "El código debe tener al menos 2 caracteres";
        }

        if (dto.Code.Trim().Length > 40)
        {
            return
                "El código no puede superar los 40 caracteres";
        }

        if (dto.Code.Trim().Any(
                character =>
                    !char.IsLetterOrDigit(character)))
        {
            return
                "El código solamente puede contener letras y números";
        }

        if (string.IsNullOrWhiteSpace(
                dto.SingularName))
        {
            return
                "El nombre singular es obligatorio";
        }

        if (dto.SingularName.Trim().Length > 80)
        {
            return
                "El nombre singular no puede superar los 80 caracteres";
        }

        if (string.IsNullOrWhiteSpace(
                dto.PluralName))
        {
            return
                "El nombre plural es obligatorio";
        }

        if (dto.PluralName.Trim().Length > 80)
        {
            return
                "El nombre plural no puede superar los 80 caracteres";
        }

        if (string.IsNullOrWhiteSpace(dto.Symbol))
        {
            return
                "El símbolo es obligatorio";
        }

        if (dto.Symbol.Trim().Length > 12)
        {
            return
                "El símbolo no puede superar los 12 caracteres";
        }

        if (!dto.AllowsDecimals &&
            dto.DecimalPlaces != 0)
        {
            return
                "Una unidad discreta debe tener cero decimales";
        }

        if (dto.AllowsDecimals &&
            (
                dto.DecimalPlaces < 1 ||
                dto.DecimalPlaces >
                InventoryRoundingService
                    .QuantityDecimalPlaces
            ))
        {
            return
                $"Una unidad continua debe permitir entre 1 y " +
                $"{InventoryRoundingService.QuantityDecimalPlaces} decimales";
        }

        return null;
    }

    private string? GetCurrentUserId()
    {
        return User.FindFirstValue(
            ClaimTypes.NameIdentifier
        );
    }

    private static string NormalizeCode(
        string value)
    {
        var trimmed =
            value.Trim();

        return char.ToUpperInvariant(
                   trimmed[0]
               ) +
               trimmed[1..];
    }
}