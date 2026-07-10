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

    public async Task<ApiResponse<LoginResponseDto>> RegisterClientAsync(
    RegisterClientDto dto)
    {
        dto.FullName = dto.FullName.Trim();
        dto.Email = dto.Email.Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(dto.FullName))
            return ApiResponse<LoginResponseDto>.Fail(
                "El nombre completo es obligatorio");

        if (string.IsNullOrWhiteSpace(dto.Email))
            return ApiResponse<LoginResponseDto>.Fail(
                "El correo electrónico es obligatorio");

        if (dto.Password.Length < 8)
            return ApiResponse<LoginResponseDto>.Fail(
                "La contraseña debe tener al menos 8 caracteres");

        if (dto.Password != dto.ConfirmPassword)
            return ApiResponse<LoginResponseDto>.Fail(
                "Las contraseñas no coinciden");

        var userExists = await _db.Users
            .Find(x => x.Email == dto.Email && !x.IsDeleted)
            .AnyAsync();

        if (userExists)
            return ApiResponse<LoginResponseDto>.Fail(
                "Ya existe una cuenta con ese correo");

        var clientRole = await _db.Roles
            .Find(x =>
                x.Name == "Client" &&
                x.IsActive &&
                !x.IsDeleted)
            .FirstOrDefaultAsync();

        if (clientRole == null)
            return ApiResponse<LoginResponseDto>.Fail(
                "El rol de cliente no está configurado");

        var user = new User
        {
            FullName = dto.FullName,
            Email = dto.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            RoleId = clientRole.Id,
            RoleName = clientRole.Name,
            IsActive = true,
            TwoFactorEnabled = false,
            CreatedAt = DateTime.UtcNow
        };

        await _db.Users.InsertOneAsync(user);

        var customer = new Customer
        {
            CustomerType = "Individual",
            FullName = dto.FullName,
            Email = dto.Email,
            Phone = dto.Phone,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = user.Id
        };

        await _db.Customers.InsertOneAsync(customer);

        var token = _jwtService.GenerateToken(user);

        var loginResponse = new LoginResponseDto
        {
            Token = token,
            UserId = user.Id,
            FullName = user.FullName,
            Email = user.Email,
            RoleName = user.RoleName
        };

        return ApiResponse<LoginResponseDto>.Ok(
            loginResponse,
            "Cuenta creada correctamente");
    }

}