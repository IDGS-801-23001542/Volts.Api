using MongoDB.Driver;
using Volts.Api.DTOs;
using Volts.Api.Models;
using Volts.Api.Responses;

namespace Volts.Api.Services;

public class AuthService
{
    private readonly MongoDbService _db;
    private readonly JwtService _jwtService;

    public AuthService(MongoDbService db, JwtService jwtService)
    {
        _db = db;
        _jwtService = jwtService;
    }

    public async Task<ApiResponse<LoginResponseDto>> LoginAsync(LoginRequestDto dto)
    {
        var user = await _db.Users
            .Find(x => x.Email.ToLower() == dto.Email.ToLower() && !x.IsDeleted)
            .FirstOrDefaultAsync();

        if (user == null)
            return ApiResponse<LoginResponseDto>.Fail("Correo o contraseña incorrectos");

        if (!user.IsActive)
            return ApiResponse<LoginResponseDto>.Fail("Usuario inactivo");

        if (user.LockoutEnd != null && user.LockoutEnd > DateTime.UtcNow)
            return ApiResponse<LoginResponseDto>.Fail("Usuario bloqueado temporalmente");

        var validPassword = BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash);

        if (!validPassword)
        {
            user.FailedLoginAttempts++;

            if (user.FailedLoginAttempts >= 5)
                user.LockoutEnd = DateTime.UtcNow.AddMinutes(15);

            await _db.Users.ReplaceOneAsync(x => x.Id == user.Id, user);

            return ApiResponse<LoginResponseDto>.Fail("Correo o contraseña incorrectos");
        }

        user.FailedLoginAttempts = 0;
        user.LockoutEnd = null;
        user.LastLoginAt = DateTime.UtcNow;

        await _db.Users.ReplaceOneAsync(x => x.Id == user.Id, user);

        var token = _jwtService.GenerateToken(user);

        var response = new LoginResponseDto
        {
            Token = token,
            UserId = user.Id,
            FullName = user.FullName,
            Email = user.Email,
            RoleName = user.RoleName
        };

        return ApiResponse<LoginResponseDto>.Ok(response, "Login correcto");
    }

    public async Task<ApiResponse<User>> CreateUserAsync(CreateUserDto dto)
    {
        var exists = await _db.Users
            .Find(x => x.Email.ToLower() == dto.Email.ToLower() && !x.IsDeleted)
            .AnyAsync();

        if (exists)
            return ApiResponse<User>.Fail("Ya existe un usuario con ese correo");

        var role = await _db.Roles
            .Find(x => x.Name == dto.RoleName && !x.IsDeleted)
            .FirstOrDefaultAsync();

        if (role == null)
            return ApiResponse<User>.Fail("El rol no existe");

        var user = new User
        {
            FullName = dto.FullName,
            Email = dto.Email.ToLower(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            RoleId = role.Id,
            RoleName = role.Name,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        await _db.Users.InsertOneAsync(user);

        return ApiResponse<User>.Ok(user, "Usuario creado correctamente");
    }
}