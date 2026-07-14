using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using Volts.Api.DTOs;
using Volts.Api.Models;
using Volts.Api.Models.Common;
using Volts.Api.Responses;
using Volts.Api.Services;

namespace Volts.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin,Employee")]
public class SuppliersController : ControllerBase
{
    private static readonly string[]
        AllowedSupplierTypes =
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

    private static readonly string[]
        AllowedMaterialCategories =
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

    public SuppliersController(
        MongoDbService db)
    {
        _db = db;
    }

    // =========================================================
    // GET: api/Suppliers
    // =========================================================
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var suppliers =
            await _db.Suppliers
                .Find(item =>
                    !item.IsDeleted)
                .SortBy(item => item.Name)
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
    // =========================================================
    [HttpGet("active")]
    public async Task<IActionResult> GetActive()
    {
        var suppliers =
            await _db.Suppliers
                .Find(item =>
                    !item.IsDeleted &&
                    item.IsActive)
                .SortBy(item => item.Name)
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
    public async Task<IActionResult> GetById(
        string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return BadRequest(
                ApiResponse<Supplier>.Fail(
                    "El identificador del proveedor es obligatorio"
                )
            );
        }

        var supplier =
            await _db.Suppliers
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
        var validationError =
            ValidateSupplier(dto);

        if (validationError != null)
        {
            return BadRequest(
                ApiResponse<Supplier>.Fail(
                    validationError
                )
            );
        }

        var normalizedCode =
            NormalizeCode(dto.Code);

        var normalizedEmail =
            dto.Email
                .Trim()
                .ToLowerInvariant();

        var normalizedTaxId =
            NormalizeTaxId(dto.TaxId);

        var duplicateError =
            await ValidateDuplicatesAsync(
                normalizedCode,
                normalizedEmail,
                normalizedTaxId
            );

        if (duplicateError != null)
        {
            return BadRequest(
                ApiResponse<Supplier>.Fail(
                    duplicateError
                )
            );
        }

        var supplier = new Supplier
        {
            Code =
                normalizedCode,

            Name =
                dto.Name.Trim(),

            LegalName =
                dto.LegalName.Trim(),

            TaxId =
                normalizedTaxId,

            ContactName =
                dto.ContactName.Trim(),

            Email =
                normalizedEmail,

            Phone =
                NormalizeOptional(dto.Phone),

            Address =
                BuildAddress(dto.Address),

            SupplierType =
                dto.SupplierType.Trim(),

            MaterialCategories =
                NormalizeCategories(
                    dto.MaterialCategories
                ),

            LeadTimeDays =
                dto.LeadTimeDays,

            PaymentTerms =
                dto.PaymentTerms.Trim(),

            Notes =
                dto.Notes.Trim(),

            IsActive =
                true,

            IsDeleted =
                false,

            CreatedAt =
                DateTime.UtcNow,

            CreatedBy =
                GetCurrentUserId()
        };

        await _db.Suppliers
            .InsertOneAsync(supplier);

        return CreatedAtAction(
            nameof(GetById),
            new
            {
                id = supplier.Id
            },
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
        if (string.IsNullOrWhiteSpace(id))
        {
            return BadRequest(
                ApiResponse<Supplier>.Fail(
                    "El identificador del proveedor es obligatorio"
                )
            );
        }

        var validationError =
            ValidateSupplier(dto);

        if (validationError != null)
        {
            return BadRequest(
                ApiResponse<Supplier>.Fail(
                    validationError
                )
            );
        }

        var supplier =
            await _db.Suppliers
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

        var normalizedCode =
            NormalizeCode(dto.Code);

        var normalizedEmail =
            dto.Email
                .Trim()
                .ToLowerInvariant();

        var normalizedTaxId =
            NormalizeTaxId(dto.TaxId);

        var duplicateError =
            await ValidateDuplicatesAsync(
                normalizedCode,
                normalizedEmail,
                normalizedTaxId,
                id
            );

        if (duplicateError != null)
        {
            return BadRequest(
                ApiResponse<Supplier>.Fail(
                    duplicateError
                )
            );
        }

        supplier.Code =
            normalizedCode;

        supplier.Name =
            dto.Name.Trim();

        supplier.LegalName =
            dto.LegalName.Trim();

        supplier.TaxId =
            normalizedTaxId;

        supplier.ContactName =
            dto.ContactName.Trim();

        supplier.Email =
            normalizedEmail;

        supplier.Phone =
            NormalizeOptional(dto.Phone);

        supplier.Address =
            BuildAddress(dto.Address);

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

        supplier.Notes =
            dto.Notes.Trim();

        supplier.IsActive =
            dto.IsActive;

        supplier.UpdatedAt =
            DateTime.UtcNow;

        supplier.UpdatedBy =
            GetCurrentUserId();

        var result =
            await _db.Suppliers
                .ReplaceOneAsync(
                    item =>
                        item.Id == id &&
                        !item.IsDeleted,
                    supplier
                );

        if (result.MatchedCount == 0)
        {
            return NotFound(
                ApiResponse<Supplier>.Fail(
                    "Proveedor no encontrado"
                )
            );
        }

        /*
         * Si cambia el nombre comercial, actualizamos
         * la fotografía operativa en materias primas.
         */
        var materialUpdate =
            Builders<RawMaterial>.Update
                .Set(
                    material =>
                        material.PreferredSupplierName,
                    supplier.Name
                )
                .Set(
                    material =>
                        material.UpdatedAt,
                    DateTime.UtcNow
                )
                .Set(
                    material =>
                        material.UpdatedBy,
                    GetCurrentUserId()
                );

        await _db.RawMaterials
            .UpdateManyAsync(
                material =>
                    material.PreferredSupplierId ==
                        supplier.Id &&
                    !material.IsDeleted,
                materialUpdate
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
        var supplier =
            await _db.Suppliers
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

        var update =
            Builders<Supplier>.Update
                .Set(
                    item =>
                        item.IsActive,
                    isActive
                )
                .Set(
                    item =>
                        item.UpdatedAt,
                    DateTime.UtcNow
                )
                .Set(
                    item =>
                        item.UpdatedBy,
                    GetCurrentUserId()
                );

        var result =
            await _db.Suppliers
                .UpdateOneAsync(
                    item =>
                        item.Id == id &&
                        !item.IsDeleted,
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
    public async Task<IActionResult> Delete(
        string id)
    {
        var supplier =
            await _db.Suppliers
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

        var hasPurchases =
            await _db.Purchases
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

        using var session =
            await _db.StartSessionAsync();

        try
        {
            session.StartTransaction();

            var supplierUpdate =
                Builders<Supplier>.Update
                    .Set(
                        item =>
                            item.IsDeleted,
                        true
                    )
                    .Set(
                        item =>
                            item.IsActive,
                        false
                    )
                    .Set(
                        item =>
                            item.UpdatedAt,
                        DateTime.UtcNow
                    )
                    .Set(
                        item =>
                            item.UpdatedBy,
                        GetCurrentUserId()
                    );

            var deleteResult =
                await _db.Suppliers
                    .UpdateOneAsync(
                        session,
                        item =>
                            item.Id == id &&
                            !item.IsDeleted,
                        supplierUpdate
                    );

            if (deleteResult.MatchedCount == 0)
            {
                await session
                    .AbortTransactionAsync();

                return NotFound(
                    ApiResponse<string>.Fail(
                        "Proveedor no encontrado"
                    )
                );
            }

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
                    )
                    .Set(
                        material =>
                            material.UpdatedBy,
                        GetCurrentUserId()
                    );

            await _db.RawMaterials
                .UpdateManyAsync(
                    session,
                    material =>
                        material.PreferredSupplierId ==
                            id &&
                        !material.IsDeleted,
                    rawMaterialUpdate
                );

            await session
                .CommitTransactionAsync();
        }
        catch
        {
            await session
                .AbortTransactionAsync();

            throw;
        }

        return Ok(
            ApiResponse<string>.Ok(
                "Proveedor eliminado correctamente"
            )
        );
    }

    private async Task<string?>
        ValidateDuplicatesAsync(
            string code,
            string email,
            string taxId,
            string? excludedId = null)
    {
        var duplicateCode =
            await _db.Suppliers
                .Find(item =>
                    item.Id != excludedId &&
                    item.Code.ToUpper() ==
                        code.ToUpper() &&
                    !item.IsDeleted)
                .AnyAsync();

        if (duplicateCode)
        {
            return
                "Ya existe un proveedor con ese código";
        }

        var duplicateEmail =
            await _db.Suppliers
                .Find(item =>
                    item.Id != excludedId &&
                    item.Email.ToLower() ==
                        email.ToLower() &&
                    !item.IsDeleted)
                .AnyAsync();

        if (duplicateEmail)
        {
            return
                "Ya existe un proveedor con ese correo";
        }

        if (!string.IsNullOrWhiteSpace(taxId))
        {
            var duplicateTaxId =
                await _db.Suppliers
                    .Find(item =>
                        item.Id != excludedId &&
                        item.TaxId.ToUpper() ==
                            taxId.ToUpper() &&
                        !item.IsDeleted)
                    .AnyAsync();

            if (duplicateTaxId)
            {
                return
                    "Ya existe un proveedor con ese RFC";
            }
        }

        return null;
    }

    private static string? ValidateSupplier(
        SupplierCreateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Code))
        {
            return
                "El código del proveedor es obligatorio";
        }

        if (dto.Code.Trim().Length > 30)
        {
            return
                "El código no puede superar los 30 caracteres";
        }

        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            return
                "El nombre comercial es obligatorio";
        }

        if (dto.Name.Trim().Length < 2 ||
            dto.Name.Trim().Length > 150)
        {
            return
                "El nombre comercial debe tener entre 2 y 150 caracteres";
        }

        if (dto.LegalName?.Trim().Length > 200)
        {
            return
                "La razón social no puede superar los 200 caracteres";
        }

        if (dto.TaxId?.Trim().Length > 20)
        {
            return
                "El RFC no puede superar los 20 caracteres";
        }

        if (string.IsNullOrWhiteSpace(
                dto.ContactName))
        {
            return
                "El nombre de contacto es obligatorio";
        }

        if (dto.ContactName.Trim().Length > 150)
        {
            return
                "El contacto no puede superar los 150 caracteres";
        }

        if (string.IsNullOrWhiteSpace(dto.Email))
        {
            return
                "El correo electrónico es obligatorio";
        }

        if (!IsValidEmail(dto.Email))
        {
            return
                "El correo electrónico no tiene un formato válido";
        }

        if (!string.IsNullOrWhiteSpace(dto.Phone) &&
            !Regex.IsMatch(
                dto.Phone.Trim(),
                @"^[0-9+\-\s()]{7,25}$"))
        {
            return
                "El teléfono contiene caracteres no permitidos";
        }

        if (!AllowedSupplierTypes.Contains(
                dto.SupplierType))
        {
            return
                "El tipo de proveedor no es válido";
        }

        if (dto.LeadTimeDays < 0 ||
            dto.LeadTimeDays > 365)
        {
            return
                "Los días de entrega deben estar entre 0 y 365";
        }

        if (dto.MaterialCategories == null)
        {
            return
                "Las categorías de materiales son obligatorias";
        }

        if (dto.MaterialCategories.Any(
                category =>
                    !AllowedMaterialCategories.Contains(
                        category)))
        {
            return
                "Una de las categorías de materiales no es válida";
        }

        var addressError =
            ValidateAddress(dto.Address);

        if (addressError != null)
        {
            return addressError;
        }

        if (dto.PaymentTerms?.Trim().Length > 300)
        {
            return
                "Las condiciones de pago no pueden superar los 300 caracteres";
        }

        if (dto.Notes?.Trim().Length > 1000)
        {
            return
                "Las observaciones no pueden superar los 1000 caracteres";
        }

        return null;
    }

    private static string? ValidateAddress(
        SupplierAddressDto? address)
    {
        if (address == null)
        {
            return
                "La dirección del proveedor es obligatoria";
        }

        if (string.IsNullOrWhiteSpace(
                address.Street))
        {
            return
                "La calle es obligatoria";
        }

        if (string.IsNullOrWhiteSpace(
                address.ExteriorNumber))
        {
            return
                "El número exterior es obligatorio";
        }

        if (string.IsNullOrWhiteSpace(
                address.Neighborhood))
        {
            return
                "La colonia es obligatoria";
        }

        if (string.IsNullOrWhiteSpace(
                address.PostalCode))
        {
            return
                "El código postal es obligatorio";
        }

        if (string.IsNullOrWhiteSpace(
                address.City))
        {
            return
                "La ciudad es obligatoria";
        }

        if (string.IsNullOrWhiteSpace(
                address.State))
        {
            return
                "El estado es obligatorio";
        }

        if (string.IsNullOrWhiteSpace(
                address.Country))
        {
            return
                "El país es obligatorio";
        }

        if (address.Country
                .Trim()
                .Equals(
                    "México",
                    StringComparison.OrdinalIgnoreCase) &&
            !Regex.IsMatch(
                address.PostalCode.Trim(),
                @"^\d{5}$"))
        {
            return
                "El código postal de México debe contener 5 dígitos";
        }

        if (address.Street.Trim().Length > 150 ||
            address.ExteriorNumber.Trim().Length > 20 ||
            address.InteriorNumber?.Trim().Length > 20 ||
            address.Neighborhood.Trim().Length > 120 ||
            address.PostalCode.Trim().Length > 15 ||
            address.City.Trim().Length > 100 ||
            address.State.Trim().Length > 100 ||
            address.Country.Trim().Length > 100 ||
            address.References?.Trim().Length > 500)
        {
            return
                "Uno de los campos de dirección supera la longitud permitida";
        }

        return null;
    }

    private static Address BuildAddress(
        SupplierAddressDto dto)
    {
        return new Address
        {
            Street =
                dto.Street.Trim(),

            ExteriorNumber =
                dto.ExteriorNumber.Trim(),

            InteriorNumber =
                NormalizeOptional(
                    dto.InteriorNumber
                ),

            Neighborhood =
                dto.Neighborhood.Trim(),

            PostalCode =
                dto.PostalCode.Trim(),

            City =
                dto.City.Trim(),

            State =
                dto.State.Trim(),

            Country =
                dto.Country.Trim(),

            References =
                NormalizeOptional(
                    dto.References
                )
        };
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

    private static string NormalizeTaxId(
        string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToUpperInvariant();
    }

    private static string? NormalizeOptional(
        string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private static List<string>
        NormalizeCategories(
            IEnumerable<string> categories)
    {
        return categories
            .Where(category =>
                !string.IsNullOrWhiteSpace(
                    category))
            .Select(category =>
                category.Trim())
            .Distinct(
                StringComparer.OrdinalIgnoreCase)
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