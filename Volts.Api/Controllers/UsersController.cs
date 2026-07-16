using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using Volts.Api.DTOs;
using Volts.Api.Models;
using Volts.Api.Models.Enums;
using Volts.Api.Responses;
using Volts.Api.Services;

namespace Volts.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly MongoDbService _db;

    public UsersController(MongoDbService db)
    {
        _db = db;
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetAll()
    {
        var users = await _db.Users
            .Find(item => !item.IsDeleted)
            .SortByDescending(item => item.CreatedAt)
            .ToListAsync();

        var customerIds = users
            .Where(item => item.UserType == UserType.Customer && !string.IsNullOrWhiteSpace(item.ProfileId))
            .Select(item => item.ProfileId!)
            .Distinct()
            .ToList();

        var institutionIds = users
            .Where(item => item.UserType == UserType.Institution && !string.IsNullOrWhiteSpace(item.ProfileId))
            .Select(item => item.ProfileId!)
            .Distinct()
            .ToList();

        var customers = customerIds.Count == 0
            ? new List<Customer>()
            : await _db.Customers.Find(item => customerIds.Contains(item.Id) && !item.IsDeleted).ToListAsync();

        var institutions = institutionIds.Count == 0
            ? new List<Institution>()
            : await _db.Institutions.Find(item => institutionIds.Contains(item.Id) && !item.IsDeleted).ToListAsync();

        foreach (var user in users)
        {
            if (user.UserType == UserType.Customer)
            {
                user.RelatedProfileType = "Cliente";
                user.RelatedProfileName = customers.FirstOrDefault(item => item.Id == user.ProfileId)?.FullName;
            }
            else if (user.UserType == UserType.Institution)
            {
                user.RelatedProfileType = "Institución";
                user.RelatedProfileName = institutions.FirstOrDefault(item => item.Id == user.ProfileId)?.Name;
            }
            else
            {
                user.RelatedProfileType = "Personal interno";
                user.RelatedProfileName = null;
            }
        }

        return Ok(
            ApiResponse<List<User>>.Ok(
                users,
                "Usuarios obtenidos correctamente"
            )
        );
    }

    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var id = GetCurrentUserId();

        var user = await _db.Users
            .Find(item =>
                item.Id == id &&
                !item.IsDeleted)
            .FirstOrDefaultAsync();

        if (user == null)
        {
            return NotFound(
                ApiResponse<User>.Fail(
                    "Usuario no encontrado"
                )
            );
        }

        return Ok(
            ApiResponse<User>.Ok(
                user,
                "Usuario obtenido correctamente"
            )
        );
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(
        string id,
        [FromBody] UserUpdateDto dto)
    {
        var user = await _db.Users
            .Find(item =>
                item.Id == id &&
                !item.IsDeleted)
            .FirstOrDefaultAsync();

        if (user == null)
        {
            return NotFound(
                ApiResponse<User>.Fail(
                    "Usuario no encontrado"
                )
            );
        }

        if (user.UserType is UserType.Customer or UserType.Institution)
        {
            return BadRequest(
                ApiResponse<User>.Fail(
                    "Los datos de una cuenta de portal se administran desde su perfil de Cliente o Institución."
                )
            );
        }

        if (string.IsNullOrWhiteSpace(dto.FirstNames) ||
            string.IsNullOrWhiteSpace(dto.PaternalLastName))
        {
            return BadRequest(
                ApiResponse<User>.Fail(
                    "Los nombres y el apellido paterno son obligatorios"
                )
            );
        }

        var role = await _db.Roles
            .Find(item =>
                item.Name == dto.RoleName &&
                item.IsActive &&
                !item.IsDeleted)
            .FirstOrDefaultAsync();

        if (role == null)
        {
            return BadRequest(
                ApiResponse<User>.Fail(
                    "El rol seleccionado no existe o está inactivo"
                )
            );
        }

        if (role.Name is "Client" or "Institution")
        {
            return BadRequest(
                ApiResponse<User>.Fail(
                    "Client e Institution solo se asignan mediante sus flujos comerciales."
                )
            );
        }

        if (user.RoleName == "Admin" &&
            role.Name != "Admin")
        {
            var activeAdmins = await _db.Users
                .Find(item =>
                    item.RoleName == "Admin" &&
                    item.IsActive &&
                    !item.IsDeleted)
                .CountDocumentsAsync();

            if (activeAdmins <= 1)
            {
                return BadRequest(
                    ApiResponse<User>.Fail(
                        "No puedes quitar el rol al último administrador activo"
                    )
                );
            }
        }

        if (!dto.IsActive &&
            user.Id == GetCurrentUserId())
        {
            return BadRequest(
                ApiResponse<User>.Fail(
                    "No puedes desactivar tu propia cuenta"
                )
            );
        }

        user.Name.FirstNames = dto.FirstNames.Trim();
        user.Name.PaternalLastName =
            dto.PaternalLastName.Trim();
        user.Name.MaternalLastName =
            string.IsNullOrWhiteSpace(
                dto.MaternalLastName)
                ? null
                : dto.MaternalLastName.Trim();

        user.LegacyFullName = null;
        user.RoleId = role.Id;
        user.RoleName = role.Name;
        user.UserType = ResolveUserType(role.Name);
        user.IsActive = dto.IsActive;
        user.UpdatedAt = DateTime.UtcNow;
        user.UpdatedBy = GetCurrentUserId();

        await _db.Users.ReplaceOneAsync(
            item => item.Id == user.Id,
            user
        );

        return Ok(
            ApiResponse<User>.Ok(
                user,
                "Usuario actualizado correctamente"
            )
        );
    }

    [HttpPut("{id}/status")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateStatus(
        string id,
        [FromBody] UserStatusUpdateDto dto)
    {
        if (!dto.IsActive &&
            id == GetCurrentUserId())
        {
            return BadRequest(
                ApiResponse<User>.Fail(
                    "No puedes desactivar tu propia cuenta"
                )
            );
        }

        var user = await _db.Users
            .Find(item =>
                item.Id == id &&
                !item.IsDeleted)
            .FirstOrDefaultAsync();

        if (user == null)
        {
            return NotFound(
                ApiResponse<User>.Fail(
                    "Usuario no encontrado"
                )
            );
        }

        if (!dto.IsActive &&
            user.RoleName == "Admin")
        {
            var activeAdmins = await _db.Users
                .Find(item =>
                    item.RoleName == "Admin" &&
                    item.IsActive &&
                    !item.IsDeleted)
                .CountDocumentsAsync();

            if (activeAdmins <= 1)
            {
                return BadRequest(
                    ApiResponse<User>.Fail(
                        "No puedes desactivar al último administrador activo"
                    )
                );
            }
        }

        user.IsActive = dto.IsActive;
        user.UpdatedAt = DateTime.UtcNow;
        user.UpdatedBy = GetCurrentUserId();

        await _db.Users.ReplaceOneAsync(
            item => item.Id == user.Id,
            user
        );

        return Ok(
            ApiResponse<User>.Ok(
                user,
                dto.IsActive
                    ? "Usuario activado"
                    : "Usuario desactivado"
            )
        );
    }

    [HttpPost("{id}/unlock")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Unlock(string id)
    {
        var user = await _db.Users
            .Find(item =>
                item.Id == id &&
                !item.IsDeleted)
            .FirstOrDefaultAsync();

        if (user == null)
        {
            return NotFound(
                ApiResponse<User>.Fail(
                    "Usuario no encontrado"
                )
            );
        }

        user.FailedLoginAttempts = 0;
        user.LockoutEnd = null;
        user.UpdatedAt = DateTime.UtcNow;
        user.UpdatedBy = GetCurrentUserId();

        await _db.Users.ReplaceOneAsync(
            item => item.Id == user.Id,
            user
        );

        return Ok(
            ApiResponse<User>.Ok(
                user,
                "Usuario desbloqueado correctamente"
            )
        );
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(string id)
    {
        if (id == GetCurrentUserId())
        {
            return BadRequest(
                ApiResponse<string>.Fail(
                    "No puedes eliminar tu propio usuario"
                )
            );
        }

        var user = await _db.Users
            .Find(item =>
                item.Id == id &&
                !item.IsDeleted)
            .FirstOrDefaultAsync();

        if (user == null)
        {
            return NotFound(
                ApiResponse<string>.Fail(
                    "Usuario no encontrado"
                )
            );
        }

        if (user.RoleName == "Admin")
        {
            var activeAdmins = await _db.Users
                .Find(item =>
                    item.RoleName == "Admin" &&
                    item.IsActive &&
                    !item.IsDeleted)
                .CountDocumentsAsync();

            if (activeAdmins <= 1)
            {
                return BadRequest(
                    ApiResponse<string>.Fail(
                        "No puedes eliminar al último administrador activo"
                    )
                );
            }
        }

        await _db.Users.UpdateOneAsync(
            item => item.Id == id,
            Builders<User>.Update
                .Set(item => item.IsDeleted, true)
                .Set(item => item.IsActive, false)
                .Set(item => item.UpdatedAt, DateTime.UtcNow)
                .Set(item => item.UpdatedBy, GetCurrentUserId())
        );

        return Ok(
            ApiResponse<string>.Ok(
                "Usuario eliminado correctamente"
            )
        );
    }

    private string? GetCurrentUserId()
    {
        return User.FindFirstValue(
            ClaimTypes.NameIdentifier
        );
    }

    private static UserType ResolveUserType(
        string roleName)
    {
        return roleName switch
        {
            "Admin" => UserType.Employee,
            "Employee" => UserType.Employee,
            "Institution" => UserType.Institution,
            _ => UserType.Customer
        };
    }
}
