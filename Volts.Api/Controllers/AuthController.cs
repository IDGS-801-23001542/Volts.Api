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

    public AuthController(AuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(LoginRequestDto dto)
    {
        var result = await _authService.LoginAsync(dto);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    [HttpPost("users")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateUser(CreateUserDto dto)
    {
        var result = await _authService.CreateUserAsync(dto);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }
}