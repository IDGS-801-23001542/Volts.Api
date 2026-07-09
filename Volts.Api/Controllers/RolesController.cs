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

    public RolesController(MongoDbService db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var roles = await _db.Roles
            .Find(x => !x.IsDeleted)
            .SortBy(x => x.Name)
            .ToListAsync();

        return Ok(ApiResponse<List<Role>>.Ok(roles));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        var role = await _db.Roles
            .Find(x => x.Id == id && !x.IsDeleted)
            .FirstOrDefaultAsync();

        if (role == null)
            return NotFound(ApiResponse<Role>.Fail("Rol no encontrado"));

        return Ok(ApiResponse<Role>.Ok(role));
    }

    [HttpPost]
    public async Task<IActionResult> Create(RoleCreateDto dto)
    {
        var exists = await _db.Roles
            .Find(x => x.Name.ToLower() == dto.Name.ToLower() && !x.IsDeleted)
            .AnyAsync();

        if (exists)
            return BadRequest(ApiResponse<Role>.Fail("Ya existe un rol con ese nombre"));

        var role = new Role
        {
            Name = dto.Name,
            Description = dto.Description,
            Permissions = dto.Permissions,
            IsActive = true
        };

        await _db.Roles.InsertOneAsync(role);

        return Ok(ApiResponse<Role>.Ok(role, "Rol creado correctamente"));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, RoleUpdateDto dto)
    {
        var role = await _db.Roles
            .Find(x => x.Id == id && !x.IsDeleted)
            .FirstOrDefaultAsync();

        if (role == null)
            return NotFound(ApiResponse<Role>.Fail("Rol no encontrado"));

        role.Name = dto.Name;
        role.Description = dto.Description;
        role.Permissions = dto.Permissions;
        role.IsActive = dto.IsActive;
        role.UpdatedAt = DateTime.UtcNow;

        await _db.Roles.ReplaceOneAsync(x => x.Id == id, role);

        return Ok(ApiResponse<Role>.Ok(role, "Rol actualizado correctamente"));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var protectedRoles = new[] { "Admin", "Employee", "Client" };

        var role = await _db.Roles
            .Find(x => x.Id == id && !x.IsDeleted)
            .FirstOrDefaultAsync();

        if (role == null)
            return NotFound(ApiResponse<string>.Fail("Rol no encontrado"));

        if (protectedRoles.Contains(role.Name))
            return BadRequest(ApiResponse<string>.Fail("No puedes eliminar un rol base del sistema"));

        var update = Builders<Role>.Update
            .Set(x => x.IsDeleted, true)
            .Set(x => x.UpdatedAt, DateTime.UtcNow);

        await _db.Roles.UpdateOneAsync(x => x.Id == id, update);

        return Ok(ApiResponse<string>.Ok("Rol eliminado correctamente"));
    }
}