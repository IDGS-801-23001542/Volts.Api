using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using Volts.Api.DTOs;
using Volts.Api.Models;
using Volts.Api.Responses;
using Volts.Api.Services;

namespace Volts.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CommercialPackagesController : ControllerBase
{
    private readonly MongoDbService _db;

    public CommercialPackagesController(MongoDbService db)
    {
        _db = db;
    }

    [HttpGet]
    [Authorize(Roles = "Admin,Employee")]
    public async Task<IActionResult> GetAll()
    {
        var packages = await _db.CommercialPackages
            .Find(x => !x.IsDeleted)
            .SortBy(x => x.DisplayOrder)
            .ThenBy(x => x.Name)
            .ToListAsync();

        return Ok(ApiResponse<List<CommercialPackage>>.Ok(
            packages,
            "Paquetes comerciales obtenidos correctamente"
        ));
    }

    [HttpGet("active")]
    [AllowAnonymous]
    public async Task<IActionResult> GetActive()
    {
        var packages = await _db.CommercialPackages
            .Find(x => !x.IsDeleted && x.IsActive)
            .SortBy(x => x.DisplayOrder)
            .ThenBy(x => x.Name)
            .ToListAsync();

        return Ok(ApiResponse<List<CommercialPackage>>.Ok(
            packages,
            "Paquetes comerciales activos obtenidos correctamente"
        ));
    }

    [HttpGet("{id}")]
    [Authorize(Roles = "Admin,Employee")]
    public async Task<IActionResult> GetById(string id)
    {
        var package = await _db.CommercialPackages
            .Find(x => x.Id == id && !x.IsDeleted)
            .FirstOrDefaultAsync();

        return package == null
            ? NotFound(ApiResponse<CommercialPackage>.Fail("Paquete comercial no encontrado"))
            : Ok(ApiResponse<CommercialPackage>.Ok(package, "Paquete comercial obtenido correctamente"));
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([FromBody] CommercialPackageCreateDto dto)
    {
        var build = await BuildPackageAsync(dto);
        if (!build.Success)
            return BadRequest(ApiResponse<CommercialPackage>.Fail(build.Error!));

        if (await ExistsDuplicate(dto.Name, build.Package!.Code))
            return BadRequest(ApiResponse<CommercialPackage>.Fail(
                "Ya existe un paquete comercial con ese nombre o código"
            ));

        build.Package.IsActive = true;
        build.Package.IsDeleted = false;
        build.Package.CreatedAt = DateTime.UtcNow;
        build.Package.CreatedBy = User.Identity?.Name;

        await _db.CommercialPackages.InsertOneAsync(build.Package);

        return CreatedAtAction(
            nameof(GetById),
            new { id = build.Package.Id },
            ApiResponse<CommercialPackage>.Ok(build.Package, "Paquete comercial creado correctamente")
        );
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(string id, [FromBody] CommercialPackageUpdateDto dto)
    {
        var existing = await _db.CommercialPackages
            .Find(x => x.Id == id && !x.IsDeleted)
            .FirstOrDefaultAsync();

        if (existing == null)
            return NotFound(ApiResponse<CommercialPackage>.Fail("Paquete comercial no encontrado"));

        var build = await BuildPackageAsync(dto);
        if (!build.Success)
            return BadRequest(ApiResponse<CommercialPackage>.Fail(build.Error!));

        if (await ExistsDuplicate(dto.Name, build.Package!.Code, id))
            return BadRequest(ApiResponse<CommercialPackage>.Fail(
                "Ya existe otro paquete comercial con ese nombre o código"
            ));

        var updated = build.Package;
        updated.Id = existing.Id;
        updated.IsActive = dto.IsActive;
        updated.IsDeleted = false;
        updated.CreatedAt = existing.CreatedAt;
        updated.CreatedBy = existing.CreatedBy;
        updated.UpdatedAt = DateTime.UtcNow;
        updated.UpdatedBy = User.Identity?.Name;

        await _db.CommercialPackages.ReplaceOneAsync(x => x.Id == id, updated);

        return Ok(ApiResponse<CommercialPackage>.Ok(updated, "Paquete comercial actualizado correctamente"));
    }

    [HttpPatch("{id}/status")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateStatus(string id, [FromQuery] bool isActive)
    {
        var result = await _db.CommercialPackages.UpdateOneAsync(
            x => x.Id == id && !x.IsDeleted,
            Builders<CommercialPackage>.Update
                .Set(x => x.IsActive, isActive)
                .Set(x => x.UpdatedAt, DateTime.UtcNow)
                .Set(x => x.UpdatedBy, User.Identity?.Name)
        );

        if (result.MatchedCount == 0)
            return NotFound(ApiResponse<string>.Fail("Paquete comercial no encontrado"));

        return Ok(ApiResponse<string>.Ok(
            isActive ? "Paquete comercial activado correctamente" : "Paquete comercial desactivado correctamente"
        ));
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(string id)
    {
        var result = await _db.CommercialPackages.UpdateOneAsync(
            x => x.Id == id && !x.IsDeleted,
            Builders<CommercialPackage>.Update
                .Set(x => x.IsDeleted, true)
                .Set(x => x.IsActive, false)
                .Set(x => x.UpdatedAt, DateTime.UtcNow)
                .Set(x => x.UpdatedBy, User.Identity?.Name)
        );

        if (result.MatchedCount == 0)
            return NotFound(ApiResponse<string>.Fail("Paquete comercial no encontrado"));

        return Ok(ApiResponse<string>.Ok("Paquete comercial eliminado correctamente"));
    }

    private async Task<(bool Success, string? Error, CommercialPackage? Package)> BuildPackageAsync(CommercialPackageCreateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.CommercialPlanId))
            return (false, "Debes seleccionar un plan comercial", null);
        if (string.IsNullOrWhiteSpace(dto.Name) || dto.Name.Trim().Length < 3)
            return (false, "El nombre del paquete debe tener al menos 3 caracteres", null);
        if (string.IsNullOrWhiteSpace(dto.Code))
            return (false, "El código del paquete es obligatorio", null);
        if (string.IsNullOrWhiteSpace(dto.Description))
            return (false, "La descripción del paquete es obligatoria", null);
        if (dto.Price < 0)
            return (false, "El precio del paquete no puede ser negativo", null);
        if (dto.DisplayOrder < 0)
            return (false, "El orden de visualización no puede ser negativo", null);
        if (dto.Items == null || dto.Items.Count == 0)
            return (false, "El paquete debe incluir al menos un producto", null);
        if (dto.Items.Any(x => string.IsNullOrWhiteSpace(x.ProductId) || x.Quantity <= 0))
            return (false, "Todos los productos deben ser válidos y tener una cantidad mayor a cero", null);
        if (dto.Items.GroupBy(x => x.ProductId).Any(g => g.Count() > 1))
            return (false, "No puedes repetir un producto dentro del mismo paquete", null);

        var plan = await _db.CommercialPlans
            .Find(x => x.Id == dto.CommercialPlanId && !x.IsDeleted && x.IsActive)
            .FirstOrDefaultAsync();
        if (plan == null)
            return (false, "El plan comercial seleccionado no existe o está inactivo", null);

        var ids = dto.Items.Select(x => x.ProductId).Distinct().ToList();
        var products = await _db.Products
            .Find(x => ids.Contains(x.Id) && !x.IsDeleted && x.IsActive && x.CanBePurchased && x.CommercialStatus == "Available")
            .ToListAsync();

        if (products.Count != ids.Count)
            return (false, "Uno o más productos no existen, están inactivos o no están disponibles para compra", null);

        var items = dto.Items.Select(input =>
        {
            var product = products.First(x => x.Id == input.ProductId);
            var subtotal = decimal.Round(product.Price * input.Quantity, 2, MidpointRounding.AwayFromZero);
            return new CommercialPackageItem
            {
                ProductId = product.Id,
                ProductName = product.Name,
                Quantity = input.Quantity,
                UnitPrice = product.Price,
                Subtotal = subtotal
            };
        }).ToList();

        var referencePrice = decimal.Round(items.Sum(x => x.Subtotal), 2, MidpointRounding.AwayFromZero);
        var price = decimal.Round(dto.Price, 2, MidpointRounding.AwayFromZero);

        return (true, null, new CommercialPackage
        {
            CommercialPlanId = plan.Id,
            CommercialPlanName = plan.Name,
            Name = dto.Name.Trim(),
            Code = NormalizeCode(dto.Code),
            Description = dto.Description.Trim(),
            Price = price,
            ReferencePrice = referencePrice,
            Savings = Math.Max(0, referencePrice - price),
            Items = items,
            DisplayOrder = dto.DisplayOrder
        });
    }

    private async Task<bool> ExistsDuplicate(string name, string code, string? excludedId = null)
    {
        var filter = Builders<CommercialPackage>.Filter.And(
            Builders<CommercialPackage>.Filter.Eq(x => x.IsDeleted, false),
            Builders<CommercialPackage>.Filter.Or(
                Builders<CommercialPackage>.Filter.Regex(x => x.Name, new BsonRegularExpression($"^{System.Text.RegularExpressions.Regex.Escape(name.Trim())}$", "i")),
                Builders<CommercialPackage>.Filter.Eq(x => x.Code, code)
            )
        );
        if (!string.IsNullOrWhiteSpace(excludedId))
            filter &= Builders<CommercialPackage>.Filter.Ne(x => x.Id, excludedId);
        return await _db.CommercialPackages.Find(filter).AnyAsync();
    }

    private static string NormalizeCode(string value) =>
        value.Trim().ToUpperInvariant().Replace(" ", "-");
}
