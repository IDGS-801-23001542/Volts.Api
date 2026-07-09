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
public class LicensesController : ControllerBase
{
    private readonly MongoDbService _db;

    public LicensesController(MongoDbService db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var licenses = await _db.Licenses
            .Find(x => !x.IsDeleted)
            .SortByDescending(x => x.CreatedAt)
            .ToListAsync();

        return Ok(ApiResponse<List<License>>.Ok(licenses));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        var license = await _db.Licenses
            .Find(x => x.Id == id && !x.IsDeleted)
            .FirstOrDefaultAsync();

        if (license == null)
            return NotFound(ApiResponse<License>.Fail("Licencia no encontrada"));

        return Ok(ApiResponse<License>.Ok(license));
    }

    [HttpGet("customer/{customerId}")]
    public async Task<IActionResult> GetByCustomer(string customerId)
    {
        var licenses = await _db.Licenses
            .Find(x => x.CustomerId == customerId && !x.IsDeleted)
            .SortByDescending(x => x.CreatedAt)
            .ToListAsync();

        return Ok(ApiResponse<List<License>>.Ok(licenses));
    }

    [HttpPost]
    public async Task<IActionResult> Create(LicenseCreateDto dto)
    {
        var customerExists = await _db.Customers
            .Find(x => x.Id == dto.CustomerId && !x.IsDeleted)
            .AnyAsync();

        if (!customerExists)
            return BadRequest(ApiResponse<License>.Fail("El cliente no existe"));

        if (!string.IsNullOrWhiteSpace(dto.InstitutionId))
        {
            var institutionExists = await _db.Institutions
                .Find(x => x.Id == dto.InstitutionId && !x.IsDeleted)
                .AnyAsync();

            if (!institutionExists)
                return BadRequest(ApiResponse<License>.Fail("La institución no existe"));
        }

        var license = new License
        {
            LicenseCode = GenerateLicenseCode(),
            CustomerId = dto.CustomerId,
            InstitutionId = dto.InstitutionId,
            PlanName = dto.PlanName,
            Status = "Available",
            ExpirationDate = dto.ExpirationDate
        };

        await _db.Licenses.InsertOneAsync(license);

        return Ok(ApiResponse<License>.Ok(license, "Licencia creada correctamente"));
    }

    [HttpPut("{id}/assign")]
    public async Task<IActionResult> Assign(string id, LicenseAssignDto dto)
    {
        var license = await _db.Licenses
            .Find(x => x.Id == id && !x.IsDeleted)
            .FirstOrDefaultAsync();

        if (license == null)
            return NotFound(ApiResponse<License>.Fail("Licencia no encontrada"));

        if (license.Status == "Revoked")
            return BadRequest(ApiResponse<License>.Fail("No se puede asignar una licencia revocada"));

        license.AssignedToName = dto.AssignedToName;
        license.AssignedToEmail = dto.AssignedToEmail;
        license.DeviceSerialNumber = dto.DeviceSerialNumber;
        license.Status = "Active";
        license.ActivationDate ??= DateTime.UtcNow;
        license.UpdatedAt = DateTime.UtcNow;

        await _db.Licenses.ReplaceOneAsync(x => x.Id == id, license);

        return Ok(ApiResponse<License>.Ok(license, "Licencia asignada correctamente"));
    }

    [HttpPut("{id}/status")]
    public async Task<IActionResult> UpdateStatus(string id, LicenseStatusUpdateDto dto)
    {
        var allowedStatuses = new[] { "Available", "Active", "Expired", "Revoked" };

        if (!allowedStatuses.Contains(dto.Status))
            return BadRequest(ApiResponse<string>.Fail("Estado de licencia inválido"));

        var update = Builders<License>.Update
            .Set(x => x.Status, dto.Status)
            .Set(x => x.UpdatedAt, DateTime.UtcNow);

        var result = await _db.Licenses.UpdateOneAsync(x => x.Id == id && !x.IsDeleted, update);

        if (result.ModifiedCount == 0)
            return NotFound(ApiResponse<string>.Fail("Licencia no encontrada"));

        return Ok(ApiResponse<string>.Ok("Estado de licencia actualizado correctamente"));
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(string id)
    {
        var update = Builders<License>.Update
            .Set(x => x.IsDeleted, true)
            .Set(x => x.UpdatedAt, DateTime.UtcNow);

        var result = await _db.Licenses.UpdateOneAsync(x => x.Id == id && !x.IsDeleted, update);

        if (result.ModifiedCount == 0)
            return NotFound(ApiResponse<string>.Fail("Licencia no encontrada"));

        return Ok(ApiResponse<string>.Ok("Licencia eliminada correctamente"));
    }

    private static string GenerateLicenseCode()
    {
        return $"VOLTS-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpper()}";
    }
}