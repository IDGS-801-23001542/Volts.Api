using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volts.Api.DTOs;
using Volts.Api.Responses;
using Volts.Api.Services;

namespace Volts.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AuthService _authService;
    private readonly AuditTrailService _audit;

    public AuthController(AuthService authService, AuditTrailService audit)
    {
        _authService = authService;
        _audit = audit;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(LoginRequestDto dto)
    {
        var result = await _authService.LoginAsync(dto,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString(),
            HttpContext.TraceIdentifier);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var userName = User.FindFirstValue(ClaimTypes.Name) ?? User.FindFirstValue(ClaimTypes.Email);
        var role = User.FindFirstValue(ClaimTypes.Role);
        await _audit.WriteAsync(userId, userName, role, "AuthenticatedUser", "Seguridad", "Autenticación",
            "Cerrar sesión", "Sesión", $"{userName} cerró sesión.", 200, "POST", "/api/Auth/logout",
            HttpContext.TraceIdentifier, HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString(), userId);
        return Ok(ApiResponse<string>.Ok("Sesión cerrada correctamente."));
    }

    [HttpPost("register-client")]
    [AllowAnonymous]
    public async Task<IActionResult> RegisterClient(RegisterClientDto dto)
    {
        var result = await _authService.RegisterClientAsync(dto);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("users")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateUser(CreateUserDto dto)
    {
        var result = await _authService.CreateUserAsync(dto);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
