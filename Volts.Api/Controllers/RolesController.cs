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
[Authorize(Roles = "Admin")]
public class RolesController : ControllerBase
{
    private readonly MongoDbService _db;
    private readonly PermissionCatalogService _permissions;

    public RolesController(
        MongoDbService db,
        PermissionCatalogService permissions)
    {
        _db = db;
        _permissions = permissions;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var roles = await _db.Roles
            .Find(item => !item.IsDeleted)
            .SortBy(item => item.Name)
            .ToListAsync();

        /*
         * Limpia en memoria permisos antiguos para que el frontend
         * nunca vuelva a enviar claves heredadas como products.read.
         */
        foreach (var role in roles)
        {
            role.Permissions =
                _permissions.NormalizeForRole(
                    role.Name,
                    role.Permissions
                );
        }

        return Ok(
            ApiResponse<List<Role>>.Ok(
                roles,
                "Roles obtenidos correctamente"
            )
        );
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        var role = await _db.Roles
            .Find(item =>
                item.Id == id &&
                !item.IsDeleted)
            .FirstOrDefaultAsync();

        if (role == null)
        {
            return NotFound(
                ApiResponse<Role>.Fail(
                    "Rol no encontrado"
                )
            );
        }

        role.Permissions =
            _permissions.NormalizeForRole(
                role.Name,
                role.Permissions
            );

        return Ok(
            ApiResponse<Role>.Ok(
                role,
                "Rol obtenido correctamente"
            )
        );
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] RoleCreateDto dto)
    {
        var name = dto.Name?.Trim() ?? string.Empty;
        var description =
            dto.Description?.Trim() ?? string.Empty;

        var errors = Validate(name, description);

        if (errors.Count > 0)
        {
            return BadRequest(
                ApiResponse<Role>.Fail(
                    "No fue posible crear el rol",
                    errors
                )
            );
        }

        if (_permissions.IsProtectedRole(name))
        {
            return BadRequest(
                ApiResponse<Role>.Fail(
                    "Ese nombre está reservado para un rol base"
                )
            );
        }

        var duplicate = await _db.Roles
            .Find(item =>
                item.Name.ToLower() ==
                    name.ToLower() &&
                !item.IsDeleted)
            .AnyAsync();

        if (duplicate)
        {
            return BadRequest(
                ApiResponse<Role>.Fail(
                    "Ya existe un rol con ese nombre"
                )
            );
        }

        var role = new Role
        {
            Name = name,
            Description = description,
            Permissions =
                _permissions.NormalizeForRole(
                    name,
                    dto.Permissions
                ),
            IsActive = true,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = CurrentUserId()
        };

        await _db.Roles.InsertOneAsync(role);

        return Ok(
            ApiResponse<Role>.Ok(
                role,
                "Rol creado correctamente"
            )
        );
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(
        string id,
        [FromBody] RoleUpdateDto dto)
    {
        var role = await _db.Roles
            .Find(item =>
                item.Id == id &&
                !item.IsDeleted)
            .FirstOrDefaultAsync();

        if (role == null)
        {
            return NotFound(
                ApiResponse<Role>.Fail(
                    "Rol no encontrado"
                )
            );
        }

        var isProtected =
            _permissions.IsProtectedRole(
                role.Name
            );

        var requestedName =
            dto.Name?.Trim() ?? string.Empty;

        var effectiveName = isProtected
            ? role.Name
            : requestedName;

        var description =
            dto.Description?.Trim() ?? string.Empty;

        var errors = Validate(
            effectiveName,
            description
        );

        if (errors.Count > 0)
        {
            return BadRequest(
                ApiResponse<Role>.Fail(
                    "No fue posible actualizar el rol",
                    errors
                )
            );
        }

        if (isProtected &&
            !string.Equals(
                role.Name,
                requestedName,
                StringComparison.Ordinal))
        {
            return BadRequest(
                ApiResponse<Role>.Fail(
                    "El nombre de un rol base no puede modificarse"
                )
            );
        }

        if (isProtected && !dto.IsActive)
        {
            return BadRequest(
                ApiResponse<Role>.Fail(
                    "Un rol base no puede desactivarse"
                )
            );
        }

        if (!isProtected)
        {
            var duplicate = await _db.Roles
                .Find(item =>
                    item.Id != id &&
                    item.Name.ToLower() ==
                        effectiveName.ToLower() &&
                    !item.IsDeleted)
                .AnyAsync();

            if (duplicate)
            {
                return BadRequest(
                    ApiResponse<Role>.Fail(
                        "Ya existe otro rol con ese nombre"
                    )
                );
            }
        }

        if (!isProtected && !dto.IsActive)
        {
            var hasActiveUsers =
                await _db.Users
                    .Find(item =>
                        item.RoleId == role.Id &&
                        item.IsActive &&
                        !item.IsDeleted)
                    .AnyAsync();

            if (hasActiveUsers)
            {
                return BadRequest(
                    ApiResponse<Role>.Fail(
                        "No puedes desactivar un rol con usuarios activos",
                        [
                            "Cambia primero el rol de los usuarios asociados."
                        ]
                    )
                );
            }
        }

        role.Name = effectiveName;
        role.Description = description;

        /*
         * Esta normalización elimina silenciosamente permisos viejos.
         * Así Employee deja de fallar por dashboard.read, products.read, etc.
         */
        role.Permissions =
            _permissions.NormalizeForRole(
                effectiveName,
                dto.Permissions
            );

        role.IsActive =
            isProtected || dto.IsActive;

        role.UpdatedAt = DateTime.UtcNow;
        role.UpdatedBy = CurrentUserId();

        await _db.Roles.ReplaceOneAsync(
            item => item.Id == role.Id,
            role
        );

        return Ok(
            ApiResponse<Role>.Ok(
                role,
                "Rol actualizado correctamente"
            )
        );
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var role = await _db.Roles
            .Find(item =>
                item.Id == id &&
                !item.IsDeleted)
            .FirstOrDefaultAsync();

        if (role == null)
        {
            return NotFound(
                ApiResponse<string>.Fail(
                    "Rol no encontrado"
                )
            );
        }

        if (_permissions.IsProtectedRole(role.Name))
        {
            return BadRequest(
                ApiResponse<string>.Fail(
                    "No puedes eliminar un rol base del sistema"
                )
            );
        }

        var inUse = await _db.Users
            .Find(item =>
                item.RoleId == role.Id &&
                !item.IsDeleted)
            .AnyAsync();

        if (inUse)
        {
            return BadRequest(
                ApiResponse<string>.Fail(
                    "No puedes eliminar un rol asignado a usuarios"
                )
            );
        }

        await _db.Roles.UpdateOneAsync(
            item => item.Id == id,
            Builders<Role>.Update
                .Set(item => item.IsDeleted, true)
                .Set(item => item.IsActive, false)
                .Set(item => item.UpdatedAt, DateTime.UtcNow)
                .Set(item => item.UpdatedBy, CurrentUserId())
        );

        return Ok(
            ApiResponse<string>.Ok(
                "Rol eliminado correctamente"
            )
        );
    }

    private static List<string> Validate(
        string name,
        string description)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(name) ||
            name.Length < 3)
        {
            errors.Add(
                "El nombre debe tener al menos 3 caracteres."
            );
        }

        if (string.IsNullOrWhiteSpace(description) ||
            description.Length < 5)
        {
            errors.Add(
                "La descripción debe tener al menos 5 caracteres."
            );
        }

        return errors;
    }

    private string? CurrentUserId()
    {
        return User.FindFirstValue(
            ClaimTypes.NameIdentifier
        );
    }
}
