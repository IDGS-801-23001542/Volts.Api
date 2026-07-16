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
public class CommercialPlansController : ControllerBase
{
    private static readonly string[] AllowedSupportLevels =
    {
        "Basic", "Standard", "Priority", "Premium"
    };

    private readonly MongoDbService _db;

    public CommercialPlansController(MongoDbService db)
    {
        _db = db;
    }

    [HttpGet]
    [Authorize(Roles = "Admin,Employee")]
    public async Task<IActionResult> GetAll()
    {
        var plans = await _db.CommercialPlans
            .Find(x => !x.IsDeleted)
            .SortBy(x => x.DisplayOrder)
            .ThenBy(x => x.Name)
            .ToListAsync();

        return Ok(ApiResponse<List<CommercialPlan>>.Ok(
            plans,
            "Planes comerciales obtenidos correctamente"
        ));
    }

    [HttpGet("active")]
    [AllowAnonymous]
    public async Task<IActionResult> GetActive()
    {
        var plans = await _db.CommercialPlans
            .Find(x => !x.IsDeleted && x.IsActive)
            .SortBy(x => x.DisplayOrder)
            .ThenBy(x => x.Name)
            .ToListAsync();

        return Ok(ApiResponse<List<CommercialPlan>>.Ok(
            plans,
            "Planes comerciales activos obtenidos correctamente"
        ));
    }

    [HttpGet("{id}")]
    [Authorize(Roles = "Admin,Employee")]
    public async Task<IActionResult> GetById(string id)
    {
        var plan = await _db.CommercialPlans
            .Find(x => x.Id == id && !x.IsDeleted)
            .FirstOrDefaultAsync();

        return plan == null
            ? NotFound(ApiResponse<CommercialPlan>.Fail("Plan comercial no encontrado"))
            : Ok(ApiResponse<CommercialPlan>.Ok(plan, "Plan comercial obtenido correctamente"));
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([FromBody] CommercialPlanCreateDto dto)
    {
        var error = Validate(dto);
        if (error != null)
            return BadRequest(ApiResponse<CommercialPlan>.Fail(error));

        var code = NormalizeCode(dto.Code);
        if (await ExistsDuplicate(dto.Name, code))
            return BadRequest(ApiResponse<CommercialPlan>.Fail(
                "Ya existe un plan comercial con ese nombre o código"
            ));

        var plan = new CommercialPlan
        {
            Name = dto.Name.Trim(),
            Code = code,
            Description = dto.Description.Trim(),
            Audience = dto.Audience.Trim(),
            WarrantyMonths = dto.WarrantyMonths,
            SupportLevel = dto.SupportLevel.Trim(),
            IncludesTraining = dto.IncludesTraining,
            IncludesDocumentation = dto.IncludesDocumentation,
            IncludesUpdates = dto.IncludesUpdates,
            DisplayOrder = dto.DisplayOrder,
            IsActive = true,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = User.Identity?.Name
        };

        await _db.CommercialPlans.InsertOneAsync(plan);

        return CreatedAtAction(
            nameof(GetById),
            new { id = plan.Id },
            ApiResponse<CommercialPlan>.Ok(plan, "Plan comercial creado correctamente")
        );
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(string id, [FromBody] CommercialPlanUpdateDto dto)
    {
        var error = Validate(dto);
        if (error != null)
            return BadRequest(ApiResponse<CommercialPlan>.Fail(error));

        var plan = await _db.CommercialPlans
            .Find(x => x.Id == id && !x.IsDeleted)
            .FirstOrDefaultAsync();

        if (plan == null)
            return NotFound(ApiResponse<CommercialPlan>.Fail("Plan comercial no encontrado"));

        var code = NormalizeCode(dto.Code);
        if (await ExistsDuplicate(dto.Name, code, id))
            return BadRequest(ApiResponse<CommercialPlan>.Fail(
                "Ya existe otro plan comercial con ese nombre o código"
            ));

        plan.Name = dto.Name.Trim();
        plan.Code = code;
        plan.Description = dto.Description.Trim();
        plan.Audience = dto.Audience.Trim();
        plan.WarrantyMonths = dto.WarrantyMonths;
        plan.SupportLevel = dto.SupportLevel.Trim();
        plan.IncludesTraining = dto.IncludesTraining;
        plan.IncludesDocumentation = dto.IncludesDocumentation;
        plan.IncludesUpdates = dto.IncludesUpdates;
        plan.DisplayOrder = dto.DisplayOrder;
        plan.IsActive = dto.IsActive;
        plan.UpdatedAt = DateTime.UtcNow;
        plan.UpdatedBy = User.Identity?.Name;

        await _db.CommercialPlans.ReplaceOneAsync(x => x.Id == id, plan);

        return Ok(ApiResponse<CommercialPlan>.Ok(plan, "Plan comercial actualizado correctamente"));
    }

    [HttpPatch("{id}/status")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateStatus(string id, [FromQuery] bool isActive)
    {
        var result = await _db.CommercialPlans.UpdateOneAsync(
            x => x.Id == id && !x.IsDeleted,
            Builders<CommercialPlan>.Update
                .Set(x => x.IsActive, isActive)
                .Set(x => x.UpdatedAt, DateTime.UtcNow)
                .Set(x => x.UpdatedBy, User.Identity?.Name)
        );

        if (result.MatchedCount == 0)
            return NotFound(ApiResponse<string>.Fail("Plan comercial no encontrado"));

        return Ok(ApiResponse<string>.Ok(
            isActive ? "Plan comercial activado correctamente" : "Plan comercial desactivado correctamente"
        ));
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(string id)
    {
        if (await _db.CommercialPackages.Find(x => x.CommercialPlanId == id && !x.IsDeleted).AnyAsync())
            return BadRequest(ApiResponse<string>.Fail(
                "No se puede eliminar el plan porque tiene paquetes comerciales relacionados"
            ));

        var result = await _db.CommercialPlans.UpdateOneAsync(
            x => x.Id == id && !x.IsDeleted,
            Builders<CommercialPlan>.Update
                .Set(x => x.IsDeleted, true)
                .Set(x => x.IsActive, false)
                .Set(x => x.UpdatedAt, DateTime.UtcNow)
                .Set(x => x.UpdatedBy, User.Identity?.Name)
        );

        if (result.MatchedCount == 0)
            return NotFound(ApiResponse<string>.Fail("Plan comercial no encontrado"));

        return Ok(ApiResponse<string>.Ok("Plan comercial eliminado correctamente"));
    }

    private async Task<bool> ExistsDuplicate(string name, string code, string? excludedId = null)
    {
        var filter = Builders<CommercialPlan>.Filter.And(
            Builders<CommercialPlan>.Filter.Eq(x => x.IsDeleted, false),
            Builders<CommercialPlan>.Filter.Or(
                Builders<CommercialPlan>.Filter.Regex(x => x.Name, new MongoDB.Bson.BsonRegularExpression($"^{System.Text.RegularExpressions.Regex.Escape(name.Trim())}$", "i")),
                Builders<CommercialPlan>.Filter.Eq(x => x.Code, code)
            )
        );

        if (!string.IsNullOrWhiteSpace(excludedId))
            filter &= Builders<CommercialPlan>.Filter.Ne(x => x.Id, excludedId);

        return await _db.CommercialPlans.Find(filter).AnyAsync();
    }

    private static string? Validate(CommercialPlanCreateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name) || dto.Name.Trim().Length < 3)
            return "El nombre del plan debe tener al menos 3 caracteres";
        if (string.IsNullOrWhiteSpace(dto.Code))
            return "El código del plan es obligatorio";
        if (string.IsNullOrWhiteSpace(dto.Description))
            return "La descripción del plan es obligatoria";
        if (string.IsNullOrWhiteSpace(dto.Audience))
            return "El público objetivo es obligatorio";
        if (dto.WarrantyMonths < 0 || dto.WarrantyMonths > 120)
            return "La garantía debe estar entre 0 y 120 meses";
        if (!AllowedSupportLevels.Contains(dto.SupportLevel))
            return "El nivel de soporte no es válido";
        if (dto.DisplayOrder < 0)
            return "El orden de visualización no puede ser negativo";
        return null;
    }

    private static string NormalizeCode(string value) =>
        value.Trim().ToUpperInvariant().Replace(" ", "-");
}
