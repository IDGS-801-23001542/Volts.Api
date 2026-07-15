using System.Net.Mail;
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
public class LicensesController : ControllerBase
{
    private static readonly string[] AllowedStatuses =
    {
        "Available",
        "Active",
        "Expired",
        "Revoked"
    };

    private readonly MongoDbService _db;

    public LicensesController(MongoDbService db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var licenses = await _db.Licenses
            .Find(item => !item.IsDeleted)
            .SortByDescending(item => item.CreatedAt)
            .ToListAsync();

        return Ok(
            ApiResponse<List<License>>.Ok(
                licenses,
                "Licencias obtenidas correctamente"
            )
        );
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        var license = await _db.Licenses
            .Find(item =>
                item.Id == id &&
                !item.IsDeleted)
            .FirstOrDefaultAsync();

        if (license == null)
        {
            return NotFound(
                ApiResponse<License>.Fail(
                    "Licencia no encontrada"
                )
            );
        }

        return Ok(
            ApiResponse<License>.Ok(
                license,
                "Licencia obtenida correctamente"
            )
        );
    }

    [HttpGet("customer/{customerId}")]
    public async Task<IActionResult> GetByCustomer(
        string customerId)
    {
        var licenses = await _db.Licenses
            .Find(item =>
                item.CustomerId == customerId &&
                !item.IsDeleted)
            .SortByDescending(item => item.CreatedAt)
            .ToListAsync();

        return Ok(
            ApiResponse<List<License>>.Ok(
                licenses,
                "Licencias del cliente obtenidas correctamente"
            )
        );
    }

    [HttpGet("institution/{institutionId}")]
    public async Task<IActionResult> GetByInstitution(
        string institutionId)
    {
        var licenses = await _db.Licenses
            .Find(item =>
                item.InstitutionId == institutionId &&
                !item.IsDeleted)
            .SortByDescending(item => item.CreatedAt)
            .ToListAsync();

        return Ok(
            ApiResponse<List<License>>.Ok(
                licenses,
                "Licencias de la institución obtenidas correctamente"
            )
        );
    }

    [HttpPut("{id}/assign")]
    public async Task<IActionResult> Assign(
        string id,
        [FromBody] LicenseAssignDto dto)
    {
        if (string.IsNullOrWhiteSpace(
                dto.AssignedToName))
        {
            return BadRequest(
                ApiResponse<License>.Fail(
                    "Debes indicar a quién se asigna la licencia"
                )
            );
        }

        if (!string.IsNullOrWhiteSpace(
                dto.AssignedToEmail) &&
            !IsValidEmail(dto.AssignedToEmail))
        {
            return BadRequest(
                ApiResponse<License>.Fail(
                    "El correo de asignación no es válido"
                )
            );
        }

        var license = await _db.Licenses
            .Find(item =>
                item.Id == id &&
                !item.IsDeleted)
            .FirstOrDefaultAsync();

        if (license == null)
        {
            return NotFound(
                ApiResponse<License>.Fail(
                    "Licencia no encontrada"
                )
            );
        }

        if (license.Status == "Revoked" ||
            license.Status == "Expired")
        {
            return BadRequest(
                ApiResponse<License>.Fail(
                    "No puede asignarse una licencia revocada o expirada"
                )
            );
        }

        license.AssignedToName =
            dto.AssignedToName.Trim();

        license.AssignedToEmail =
            NormalizeOptional(
                dto.AssignedToEmail
            )?.ToLowerInvariant();

        license.DeviceSerialNumber =
            NormalizeOptional(
                dto.DeviceSerialNumber
            );

        license.Status = "Active";
        license.ActivationDate ??=
            DateTime.UtcNow;
        license.UpdatedAt =
            DateTime.UtcNow;
        license.UpdatedBy =
            GetCurrentUserId();

        await _db.Licenses.ReplaceOneAsync(
            item => item.Id == license.Id,
            license
        );

        return Ok(
            ApiResponse<License>.Ok(
                license,
                "Licencia asignada y activada correctamente"
            )
        );
    }

    [HttpPut("{id}/status")]
    public async Task<IActionResult> UpdateStatus(
        string id,
        [FromBody] LicenseStatusUpdateDto dto)
    {
        var status = dto.Status?.Trim();

        if (string.IsNullOrWhiteSpace(status) ||
            !AllowedStatuses.Contains(status))
        {
            return BadRequest(
                ApiResponse<License>.Fail(
                    "Estado de licencia inválido"
                )
            );
        }

        var license = await _db.Licenses
            .Find(item =>
                item.Id == id &&
                !item.IsDeleted)
            .FirstOrDefaultAsync();

        if (license == null)
        {
            return NotFound(
                ApiResponse<License>.Fail(
                    "Licencia no encontrada"
                )
            );
        }

        license.Status = status;
        license.UpdatedAt = DateTime.UtcNow;
        license.UpdatedBy = GetCurrentUserId();

        if (status == "Active")
        {
            license.ActivationDate ??=
                DateTime.UtcNow;
        }

        await _db.Licenses.ReplaceOneAsync(
            item => item.Id == license.Id,
            license
        );

        return Ok(
            ApiResponse<License>.Ok(
                license,
                "Estado de licencia actualizado correctamente"
            )
        );
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(string id)
    {
        var result = await _db.Licenses
            .UpdateOneAsync(
                item =>
                    item.Id == id &&
                    !item.IsDeleted,
                Builders<License>.Update
                    .Set(
                        item => item.IsDeleted,
                        true
                    )
                    .Set(
                        item => item.UpdatedAt,
                        DateTime.UtcNow
                    )
                    .Set(
                        item => item.UpdatedBy,
                        GetCurrentUserId()
                    )
            );

        if (result.ModifiedCount != 1)
        {
            return NotFound(
                ApiResponse<string>.Fail(
                    "Licencia no encontrada"
                )
            );
        }

        return Ok(
            ApiResponse<string>.Ok(
                "Licencia eliminada correctamente"
            )
        );
    }

    private static bool IsValidEmail(string value)
    {
        try
        {
            _ = new MailAddress(value.Trim());
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string? NormalizeOptional(
        string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private string? GetCurrentUserId()
    {
        return User.FindFirstValue(
            ClaimTypes.NameIdentifier
        );
    }
}
