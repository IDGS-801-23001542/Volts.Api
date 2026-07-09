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
            .Find(x => !x.IsDeleted)
            .SortByDescending(x => x.CreatedAt)
            .ToListAsync();

        users.ForEach(x => x.PasswordHash = string.Empty);

        return Ok(ApiResponse<List<User>>.Ok(users));
    }

    [HttpGet("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetById(string id)
    {
        var user = await _db.Users
            .Find(x => x.Id == id && !x.IsDeleted)
            .FirstOrDefaultAsync();

        if (user == null)
            return NotFound(ApiResponse<User>.Fail("Usuario no encontrado"));

        user.PasswordHash = string.Empty;

        return Ok(ApiResponse<User>.Ok(user));
    }

    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized(ApiResponse<string>.Fail("Token inválido"));

        var user = await _db.Users
            .Find(x => x.Id == userId && !x.IsDeleted)
            .FirstOrDefaultAsync();

        if (user == null)
            return NotFound(ApiResponse<User>.Fail("Usuario no encontrado"));

        user.PasswordHash = string.Empty;

        return Ok(ApiResponse<User>.Ok(user));
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(string id, UserUpdateDto dto)
    {
        var user = await _db.Users
            .Find(x => x.Id == id && !x.IsDeleted)
            .FirstOrDefaultAsync();

        if (user == null)
            return NotFound(ApiResponse<User>.Fail("Usuario no encontrado"));

        var role = await _db.Roles
            .Find(x => x.Name == dto.RoleName && !x.IsDeleted && x.IsActive)
            .FirstOrDefaultAsync();

        if (role == null)
            return BadRequest(ApiResponse<User>.Fail("Rol inválido"));

        user.FullName = dto.FullName;
        user.RoleId = role.Id;
        user.RoleName = role.Name;
        user.IsActive = dto.IsActive;
        user.UpdatedAt = DateTime.UtcNow;

        await _db.Users.ReplaceOneAsync(x => x.Id == id, user);

        user.PasswordHash = string.Empty;

        return Ok(ApiResponse<User>.Ok(user, "Usuario actualizado correctamente"));
    }

    [HttpPut("{id}/status")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateStatus(string id, [FromQuery] bool isActive)
    {
        var update = Builders<User>.Update
            .Set(x => x.IsActive, isActive)
            .Set(x => x.UpdatedAt, DateTime.UtcNow);

        var result = await _db.Users.UpdateOneAsync(x => x.Id == id && !x.IsDeleted, update);

        if (result.ModifiedCount == 0)
            return NotFound(ApiResponse<string>.Fail("Usuario no encontrado"));

        return Ok(ApiResponse<string>.Ok(isActive ? "Usuario activado" : "Usuario desactivado"));
    }

    [HttpPut("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordDto dto)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized(ApiResponse<string>.Fail("Token inválido"));

        var user = await _db.Users
            .Find(x => x.Id == userId && !x.IsDeleted)
            .FirstOrDefaultAsync();

        if (user == null)
            return NotFound(ApiResponse<string>.Fail("Usuario no encontrado"));

        var validPassword = BCrypt.Net.BCrypt.Verify(dto.CurrentPassword, user.PasswordHash);

        if (!validPassword)
            return BadRequest(ApiResponse<string>.Fail("La contraseña actual es incorrecta"));

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
        user.UpdatedAt = DateTime.UtcNow;

        await _db.Users.ReplaceOneAsync(x => x.Id == user.Id, user);

        return Ok(ApiResponse<string>.Ok("Contraseña actualizada correctamente"));
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(string id)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (currentUserId == id)
            return BadRequest(ApiResponse<string>.Fail("No puedes eliminar tu propio usuario"));

        var update = Builders<User>.Update
            .Set(x => x.IsDeleted, true)
            .Set(x => x.IsActive, false)
            .Set(x => x.UpdatedAt, DateTime.UtcNow);

        var result = await _db.Users.UpdateOneAsync(x => x.Id == id && !x.IsDeleted, update);

        if (result.ModifiedCount == 0)
            return NotFound(ApiResponse<string>.Fail("Usuario no encontrado"));

        return Ok(ApiResponse<string>.Ok("Usuario eliminado correctamente"));
    }
}