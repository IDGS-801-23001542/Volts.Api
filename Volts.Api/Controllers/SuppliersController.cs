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
public class SuppliersController : ControllerBase
{
    private static readonly string[] AllowedSupplierTypes =
    {
        "Electronics",
        "Cardboard",
        "Textiles",
        "Adhesives",
        "Mechanical",
        "Soldering",
        "Packaging",
        "General"
    };

    private static readonly string[] AllowedMaterialCategories =
    {
        "Cardboard",
        "Electronics",
        "Mechanical",
        "Textiles",
        "Adhesives",
        "Consumables",
        "Soldering",
        "Packaging",
        "Other"
    };

    private readonly MongoDbService _db;

    public SuppliersController(MongoDbService db)
    {
        _db = db;
    }

    // =========================================================
    // GET: api/Suppliers
    // Devuelve proveedores activos e inactivos no eliminados.
    // =========================================================
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var suppliers = await _db.Suppliers
            .Find(supplier => !supplier.IsDeleted)
            .SortBy(supplier => supplier.Name)
            .ToListAsync();

        return Ok(
            ApiResponse<List<Supplier>>.Ok(
                suppliers,
                "Proveedores obtenidos correctamente"
            )
        );
    }

    // =========================================================
    // GET: api/Suppliers/active
    // Se utilizará en Compras y selectores.
    // =========================================================
    [HttpGet("active")]
    public async Task<IActionResult> GetActive()
    {
        var suppliers = await _db.Suppliers
            .Find(supplier =>
                !supplier.IsDeleted &&
                supplier.IsActive)
            .SortBy(supplier => supplier.Name)
            .ToListAsync();

        return Ok(
            ApiResponse<List<Supplier>>.Ok(
                suppliers,
                "Proveedores activos obtenidos correctamente"
            )
        );
    }

    // =========================================================
    // GET: api/Suppliers/{id}
    // =========================================================
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return BadRequest(
                ApiResponse<Supplier>.Fail(
                    "El identificador del proveedor es obligatorio"
                )
            );
        }

        var supplier = await _db.Suppliers
            .Find(item =>
                item.Id == id &&
                !item.IsDeleted)
            .FirstOrDefaultAsync();

        if (supplier == null)
        {
            return NotFound(
                ApiResponse<Supplier>.Fail(
                    "Proveedor no encontrado"
                )
            );
        }

        return Ok(
            ApiResponse<Supplier>.Ok(
                supplier,
                "Proveedor obtenido correctamente"
            )
        );
    }

    // =========================================================
    // POST: api/Suppliers
    // Admin y Employee.
    // =========================================================
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] SupplierCreateDto dto)
    {
        var validationError = ValidateSupplier(dto);

        if (validationError != null)
        {
            return BadRequest(
                ApiResponse<Supplier>.Fail(
                    validationError
                )
            );
        }

        var normalizedCode = NormalizeCode(dto.Code);
        var normalizedEmail = dto.Email
            .Trim()
            .ToLowerInvariant();

        var codeExists = await _db.Suppliers
            .Find(supplier =>
                supplier.Code.ToUpper() ==
                normalizedCode.ToUpper() &&
                !supplier.IsDeleted)
            .AnyAsync();

        if (codeExists)
        {
            return BadRequest(
                ApiResponse<Supplier>.Fail(
                    "Ya existe un proveedor con ese código"
                )
            );
        }

        var emailExists = await _db.Suppliers
            .Find(supplier =>
                supplier.Email.ToLower() ==
                normalizedEmail &&
                !supplier.IsDeleted)
            .AnyAsync();

        if (emailExists)
        {
            return BadRequest(
                ApiResponse<Supplier>.Fail(
                    "Ya existe un proveedor con ese correo"
                )
            );
        }

        if (!string.IsNullOrWhiteSpace(dto.TaxId))
        {
            var normalizedTaxId = dto.TaxId
                .Trim()
                .ToUpperInvariant();

            var taxIdExists = await _db.Suppliers
                .Find(supplier =>
                    supplier.TaxId.ToUpper() ==
                    normalizedTaxId &&
                    !supplier.IsDeleted)
                .AnyAsync();

            if (taxIdExists)
            {
                return BadRequest(
                    ApiResponse<Supplier>.Fail(
                        "Ya existe un proveedor con ese RFC"
                    )
                );
            }
        }

        var supplier = new Supplier
        {
            Code = normalizedCode,
            Name = dto.Name.Trim(),
            LegalName = dto.LegalName.Trim(),
            TaxId = dto.TaxId
                .Trim()
                .ToUpperInvariant(),
            ContactName = dto.ContactName.Trim(),
            Email = normalizedEmail,
            Phone = NormalizeOptional(dto.Phone),
            Address = NormalizeOptional(dto.Address),
            City = NormalizeOptional(dto.City),
            State = NormalizeOptional(dto.State),
            PostalCode =
                NormalizeOptional(dto.PostalCode),
            SupplierType = dto.SupplierType.Trim(),
            MaterialCategories =
                NormalizeCategories(
                    dto.MaterialCategories
                ),
            LeadTimeDays = dto.LeadTimeDays,
            PaymentTerms = dto.PaymentTerms.Trim(),
            Notes = dto.Notes.Trim(),
            IsActive = true,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = GetCurrentUserId()
        };

        await _db.Suppliers.InsertOneAsync(supplier);

        return CreatedAtAction(
            nameof(GetById),
            new { id = supplier.Id },
            ApiResponse<Supplier>.Ok(
                supplier,
                "Proveedor creado correctamente"
            )
        );
    }

    // =========================================================
    // PUT: api/Suppliers/{id}
    // Admin y Employee.
    // =========================================================
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(
        string id,
        [FromBody] SupplierUpdateDto dto)
    {
        var validationError = ValidateSupplier(dto);

        if (validationError != null)
        {
            return BadRequest(
                ApiResponse<Supplier>.Fail(
                    validationError
                )
            );
        }

        var supplier = await _db.Suppliers
            .Find(item =>
                item.Id == id &&
                !item.IsDeleted)
            .FirstOrDefaultAsync();

        if (supplier == null)
        {
            return NotFound(
                ApiResponse<Supplier>.Fail(
                    "Proveedor no encontrado"
                )
            );
        }

        var normalizedCode = NormalizeCode(dto.Code);
        var normalizedEmail = dto.Email
            .Trim()
            .ToLowerInvariant();

        var duplicateCode = await _db.Suppliers
            .Find(item =>
                item.Id != id &&
                item.Code.ToUpper() ==
                normalizedCode.ToUpper() &&
                !item.IsDeleted)
            .AnyAsync();

        if (duplicateCode)
        {
            return BadRequest(
                ApiResponse<Supplier>.Fail(
                    "Ya existe otro proveedor con ese código"
                )
            );
        }

        var duplicateEmail = await _db.Suppliers
            .Find(item =>
                item.Id != id &&
                item.Email.ToLower() ==
                normalizedEmail &&
                !item.IsDeleted)
            .AnyAsync();

        if (duplicateEmail)
        {
            return BadRequest(
                ApiResponse<Supplier>.Fail(
                    "Ya existe otro proveedor con ese correo"
                )
            );
        }

        if (!string.IsNullOrWhiteSpace(dto.TaxId))
        {
            var normalizedTaxId = dto.TaxId
                .Trim()
                .ToUpperInvariant();

            var duplicateTaxId = await _db.Suppliers
                .Find(item =>
                    item.Id != id &&
                    item.TaxId.ToUpper() ==
                    normalizedTaxId &&
                    !item.IsDeleted)
                .AnyAsync();

            if (duplicateTaxId)
            {
                return BadRequest(
                    ApiResponse<Supplier>.Fail(
                        "Ya existe otro proveedor con ese RFC"
                    )
                );
            }
        }

        supplier.Code = normalizedCode;
        supplier.Name = dto.Name.Trim();
        supplier.LegalName = dto.LegalName.Trim();
        supplier.TaxId = dto.TaxId
            .Trim()
            .ToUpperInvariant();
        supplier.ContactName =
            dto.ContactName.Trim();
        supplier.Email = normalizedEmail;
        supplier.Phone =
            NormalizeOptional(dto.Phone);
        supplier.Address =
            NormalizeOptional(dto.Address);
        supplier.City =
            NormalizeOptional(dto.City);
        supplier.State =
            NormalizeOptional(dto.State);
        supplier.PostalCode =
            NormalizeOptional(dto.PostalCode);
        supplier.SupplierType =
            dto.SupplierType.Trim();
        supplier.MaterialCategories =
            NormalizeCategories(
                dto.MaterialCategories
            );
        supplier.LeadTimeDays =
            dto.LeadTimeDays;
        supplier.PaymentTerms =
            dto.PaymentTerms.Trim();
        supplier.Notes = dto.Notes.Trim();
        supplier.IsActive = dto.IsActive;
        supplier.UpdatedAt = DateTime.UtcNow;
        supplier.UpdatedBy = GetCurrentUserId();

        await _db.Suppliers.ReplaceOneAsync(
            item =>
                item.Id == id &&
                !item.IsDeleted,
            supplier
        );

        return Ok(
            ApiResponse<Supplier>.Ok(
                supplier,
                "Proveedor actualizado correctamente"
            )
        );
    }

    // =========================================================
    // PATCH: api/Suppliers/{id}/status
    // Admin y Employee.
    // =========================================================
    [HttpPatch("{id}/status")]
    public async Task<IActionResult> UpdateStatus(
        string id,
        [FromQuery] bool isActive)
    {
        var update = Builders<Supplier>.Update
            .Set(
                supplier => supplier.IsActive,
                isActive
            )
            .Set(
                supplier => supplier.UpdatedAt,
                DateTime.UtcNow
            )
            .Set(
                supplier => supplier.UpdatedBy,
                GetCurrentUserId()
            );

        var result = await _db.Suppliers.UpdateOneAsync(
            supplier =>
                supplier.Id == id &&
                !supplier.IsDeleted,
            update
        );

        if (result.MatchedCount == 0)
        {
            return NotFound(
                ApiResponse<string>.Fail(
                    "Proveedor no encontrado"
                )
            );
        }

        return Ok(
            ApiResponse<string>.Ok(
                isActive
                    ? "Proveedor activado correctamente"
                    : "Proveedor desactivado correctamente"
            )
        );
    }

    // =========================================================
    // DELETE: api/Suppliers/{id}
    // Solo Admin.
    // =========================================================
    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(string id)
    {
        var supplier = await _db.Suppliers
            .Find(item =>
                item.Id == id &&
                !item.IsDeleted)
            .FirstOrDefaultAsync();

        if (supplier == null)
        {
            return NotFound(
                ApiResponse<string>.Fail(
                    "Proveedor no encontrado"
                )
            );
        }

        var hasPurchases = await _db.Purchases
            .Find(purchase =>
                purchase.SupplierId == id &&
                !purchase.IsDeleted)
            .AnyAsync();

        if (hasPurchases)
        {
            return BadRequest(
                ApiResponse<string>.Fail(
                    "No se puede eliminar un proveedor con compras registradas. Puedes desactivarlo."
                )
            );
        }

        var update = Builders<Supplier>.Update
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

        await _db.Suppliers.UpdateOneAsync(
            item =>
                item.Id == id &&
                !item.IsDeleted,
            update
        );

        /*
         * Quitamos la referencia preferente en materias
         * primas que apuntaban al proveedor eliminado.
         */
        var rawMaterialUpdate =
            Builders<RawMaterial>.Update
                .Set(
                    material =>
                        material.PreferredSupplierId,
                    null
                )
                .Set(
                    material =>
                        material.PreferredSupplierName,
                    null
                )
                .Set(
                    material =>
                        material.UpdatedAt,
                    DateTime.UtcNow
                );

        await _db.RawMaterials.UpdateManyAsync(
            material =>
                material.PreferredSupplierId == id &&
                !material.IsDeleted,
            rawMaterialUpdate
        );

        return Ok(
            ApiResponse<string>.Ok(
                "Proveedor eliminado correctamente"
            )
        );
    }

    private static string? ValidateSupplier(
        SupplierCreateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Code))
            return "El código del proveedor es obligatorio";

        if (dto.Code.Trim().Length > 30)
            return "El código no puede superar los 30 caracteres";

        if (string.IsNullOrWhiteSpace(dto.Name))
            return "El nombre comercial es obligatorio";

        if (dto.Name.Trim().Length < 2)
            return "El nombre comercial debe tener al menos 2 caracteres";

        if (dto.Name.Trim().Length > 150)
            return "El nombre comercial no puede superar los 150 caracteres";

        if (dto.LegalName.Trim().Length > 200)
            return "La razón social no puede superar los 200 caracteres";

        if (dto.TaxId.Trim().Length > 20)
            return "El RFC no puede superar los 20 caracteres";

        if (string.IsNullOrWhiteSpace(dto.ContactName))
            return "El nombre de contacto es obligatorio";

        if (dto.ContactName.Trim().Length > 150)
            return "El contacto no puede superar los 150 caracteres";

        if (string.IsNullOrWhiteSpace(dto.Email))
            return "El correo electrónico es obligatorio";

        if (!IsValidEmail(dto.Email))
            return "El correo electrónico no tiene un formato válido";

        if (!AllowedSupplierTypes.Contains(
            dto.SupplierType))
        {
            return "El tipo de proveedor no es válido";
        }

        if (dto.LeadTimeDays < 0)
            return "Los días de entrega no pueden ser negativos";

        if (dto.LeadTimeDays > 365)
            return "Los días de entrega no pueden superar 365";

        if (dto.MaterialCategories.Any(
            category =>
                !AllowedMaterialCategories.Contains(
                    category)))
        {
            return "Una de las categorías de materiales no es válida";
        }

        if (dto.Notes.Trim().Length > 1000)
            return "Las observaciones no pueden superar los 1000 caracteres";

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
        return value
            .Trim()
            .ToUpperInvariant()
            .Replace(" ", "-");
    }

    private static string? NormalizeOptional(
        string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private static List<string> NormalizeCategories(
        IEnumerable<string> categories)
    {
        return categories
            .Where(category =>
                !string.IsNullOrWhiteSpace(category))
            .Select(category => category.Trim())
            .Distinct()
            .ToList();
    }

    private static bool IsValidEmail(
        string email)
    {
        try
        {
            var address =
                new System.Net.Mail.MailAddress(
                    email.Trim()
                );

            return address.Address.Equals(
                email.Trim(),
                StringComparison.OrdinalIgnoreCase
            );
        }
        catch
        {
            return false;
        }
    }
}