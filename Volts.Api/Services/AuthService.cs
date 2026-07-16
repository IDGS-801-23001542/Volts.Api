using MongoDB.Driver;
using Volts.Api.DTOs;
using Volts.Api.Models;
using Volts.Api.Models.Common;
using Volts.Api.Models.Enums;
using Volts.Api.Responses;

namespace Volts.Api.Services;

public class AuthService
{
    private readonly MongoDbService _db;
    private readonly JwtService _jwtService;
    private readonly AuditTrailService _audit;
    private readonly NotificationDispatchService _notifications;

    public AuthService(
        MongoDbService db,
        JwtService jwtService,
        AuditTrailService audit,
        NotificationDispatchService notifications)
    {
        _db = db;
        _jwtService = jwtService;
        _audit = audit;
        _notifications = notifications;
    }

    public async Task<ApiResponse<LoginResponseDto>>
        LoginAsync(LoginRequestDto dto, string? ipAddress = null, string? userAgent = null, string? correlationId = null)
    {
        var email = NormalizeEmail(dto.Email);

        if (string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(dto.Password))
        {
            return ApiResponse<LoginResponseDto>.Fail(
                "El correo y la contraseña son obligatorios"
            );
        }

        var user = await _db.Users
            .Find(item =>
                item.Email == email &&
                !item.IsDeleted)
            .FirstOrDefaultAsync();

        if (user == null ||
            !BCrypt.Net.BCrypt.Verify(
                dto.Password,
                user.PasswordHash))
        {
            if (user != null)
            {
                await RegisterFailedLoginAsync(user);
            }

            await _audit.WriteAsync(
                user?.Id, user?.FullName ?? email, user?.RoleName,
                user == null ? "PublicVisitor" : "AuthenticatedUser",
                "Seguridad", "Autenticación", "Inicio de sesión fallido", "Sesión",
                $"Se rechazó un intento de inicio de sesión para {email}.", 400,
                "POST", "/api/Auth/login", correlationId ?? Guid.NewGuid().ToString("N"),
                ipAddress, userAgent, user?.Id);

            if (user != null && user.LockoutEnd.HasValue)
            {
                await _notifications.NotifyRolesAsync(new[]{"Admin"}, "Cuenta bloqueada",
                    $"La cuenta {user.Email} fue bloqueada temporalmente por intentos fallidos.",
                    "Security", "High", "Administración", "/backoffice/usuarios", "User", user.Id, null, user.Id);
            }

            return ApiResponse<LoginResponseDto>.Fail(
                "Correo o contraseña incorrectos"
            );
        }

        if (!user.IsActive)
        {
            return ApiResponse<LoginResponseDto>.Fail(
                "Usuario inactivo"
            );
        }

        if (user.LockoutEnd.HasValue &&
            user.LockoutEnd.Value > DateTime.UtcNow)
        {
            return ApiResponse<LoginResponseDto>.Fail(
                "Usuario bloqueado temporalmente"
            );
        }

        var role = await _db.Roles
            .Find(item =>
                item.Id == user.RoleId &&
                !item.IsDeleted)
            .FirstOrDefaultAsync();

        user.FailedLoginAttempts = 0;
        user.LockoutEnd = null;
        user.LastLoginAt = DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;

        await _db.Users.ReplaceOneAsync(
            item => item.Id == user.Id,
            user
        );

        var token = _jwtService.GenerateToken(user);

        await _audit.WriteAsync(
            user.Id, user.FullName, user.RoleName, "AuthenticatedUser",
            "Seguridad", "Autenticación", "Iniciar sesión", "Sesión",
            $"{user.FullName} inició sesión correctamente.", 200,
            "POST", "/api/Auth/login", correlationId ?? Guid.NewGuid().ToString("N"),
            ipAddress, userAgent, user.Id);

        return ApiResponse<LoginResponseDto>.Ok(
            BuildLoginResponse(
                user,
                token,
                role?.Permissions ?? []
            ),
            "Login correcto"
        );
    }

    public async Task<ApiResponse<User>>
        CreateUserAsync(CreateUserDto dto)
    {
        var email = NormalizeEmail(dto.Email);

        if (!IsValidEmail(email))
        {
            return ApiResponse<User>.Fail(
                "El correo electrónico no es válido"
            );
        }

        if (string.IsNullOrWhiteSpace(dto.Password) ||
            dto.Password.Length < 8)
        {
            return ApiResponse<User>.Fail(
                "La contraseña debe tener al menos 8 caracteres"
            );
        }

        if (string.IsNullOrWhiteSpace(dto.FirstNames) ||
            string.IsNullOrWhiteSpace(
                dto.PaternalLastName))
        {
            return ApiResponse<User>.Fail(
                "Los nombres y el apellido paterno son obligatorios"
            );
        }

        var exists = await _db.Users
            .Find(item =>
                item.Email == email &&
                !item.IsDeleted)
            .AnyAsync();

        if (exists)
        {
            return ApiResponse<User>.Fail(
                "Ya existe un usuario con ese correo"
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
            return ApiResponse<User>.Fail(
                "El rol seleccionado no existe o está inactivo"
            );
        }

        if (role.Name is "Client" or "Institution")
        {
            return ApiResponse<User>.Fail(
                "Los clientes e instituciones deben registrarse desde su flujo de portal"
            );
        }

        var user = new User
        {
            Name = new PersonName
            {
                FirstNames = dto.FirstNames.Trim(),
                PaternalLastName =
                    dto.PaternalLastName.Trim(),
                MaternalLastName =
                    NormalizeOptional(
                        dto.MaternalLastName)
            },
            Email = email,
            PasswordHash =
                BCrypt.Net.BCrypt.HashPassword(
                    dto.Password),
            RoleId = role.Id,
            RoleName = role.Name,
            UserType =
                UserType.Employee,
            IsActive = true,
            IsEmailConfirmed = true,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow
        };

        await _db.Users.InsertOneAsync(user);

        return ApiResponse<User>.Ok(
            user,
            "Usuario creado correctamente"
        );
    }

    public async Task<ApiResponse<LoginResponseDto>>
        RegisterClientAsync(RegisterClientDto dto)
    {
        var email = NormalizeEmail(dto.Email);

        if (!IsValidEmail(email))
        {
            return ApiResponse<LoginResponseDto>.Fail(
                "El correo electrónico no es válido"
            );
        }

        if (string.IsNullOrWhiteSpace(dto.FirstNames) ||
            string.IsNullOrWhiteSpace(
                dto.PaternalLastName))
        {
            return ApiResponse<LoginResponseDto>.Fail(
                "Los nombres y el apellido paterno son obligatorios"
            );
        }

        if (dto.Password.Length < 8)
        {
            return ApiResponse<LoginResponseDto>.Fail(
                "La contraseña debe tener al menos 8 caracteres"
            );
        }

        if (dto.Password != dto.ConfirmPassword)
        {
            return ApiResponse<LoginResponseDto>.Fail(
                "Las contraseñas no coinciden"
            );
        }

        var phone = NormalizeOptional(dto.Phone);

        if (phone != null &&
            (phone.Length != 10 ||
             phone.Any(character =>
                 !char.IsDigit(character))))
        {
            return ApiResponse<LoginResponseDto>.Fail(
                "El teléfono debe contener exactamente 10 dígitos"
            );
        }

        if (await _db.Users
            .Find(item =>
                item.Email == email &&
                !item.IsDeleted)
            .AnyAsync())
        {
            return ApiResponse<LoginResponseDto>.Fail(
                "Ya existe una cuenta con ese correo"
            );
        }

        if (await _db.Customers
            .Find(item =>
                item.Email == email &&
                !item.IsDeleted)
            .AnyAsync())
        {
            return ApiResponse<LoginResponseDto>.Fail(
                "Ya existe un cliente con ese correo"
            );
        }

        var role = await _db.Roles
            .Find(item =>
                item.Name == "Client" &&
                item.IsActive &&
                !item.IsDeleted)
            .FirstOrDefaultAsync();

        if (role == null)
        {
            return ApiResponse<LoginResponseDto>.Fail(
                "El rol Client no está configurado"
            );
        }

        using var session =
            await _db.StartSessionAsync();

        session.StartTransaction();

        try
        {
            var name = new PersonName
            {
                FirstNames = dto.FirstNames.Trim(),
                PaternalLastName =
                    dto.PaternalLastName.Trim(),
                MaternalLastName =
                    NormalizeOptional(
                        dto.MaternalLastName)
            };

            var user = new User
            {
                Name = name,
                Email = email,
                PasswordHash =
                    BCrypt.Net.BCrypt.HashPassword(
                        dto.Password),
                RoleId = role.Id,
                RoleName = role.Name,
                UserType = UserType.Customer,
                IsActive = true,
                IsEmailConfirmed = false,
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow
            };

            await _db.Users.InsertOneAsync(
                session,
                user
            );

            var customer = new Customer
            {
                Name = name,
                Email = email,
                Phone = phone,
                Address = null,
                IsActive = true,
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = user.Id
            };

            await _db.Customers.InsertOneAsync(
                session,
                customer
            );

            user.ProfileId = customer.Id;

            await _db.Users.ReplaceOneAsync(
                session,
                item => item.Id == user.Id,
                user
            );

            await session.CommitTransactionAsync();

            var token =
                _jwtService.GenerateToken(user);

            return ApiResponse<LoginResponseDto>.Ok(
                BuildLoginResponse(
                    user,
                    token,
                    role.Permissions
                ),
                "Cuenta creada correctamente"
            );
        }
        catch
        {
            await session.AbortTransactionAsync();

            return ApiResponse<LoginResponseDto>.Fail(
                "No fue posible completar el registro"
            );
        }
    }

    private async Task RegisterFailedLoginAsync(
        User user)
    {
        user.FailedLoginAttempts++;

        if (user.FailedLoginAttempts >= 5)
        {
            user.LockoutEnd =
                DateTime.UtcNow.AddMinutes(15);
            user.FailedLoginAttempts = 0;
        }

        user.UpdatedAt = DateTime.UtcNow;

        await _db.Users.ReplaceOneAsync(
            item => item.Id == user.Id,
            user
        );
    }

    private static LoginResponseDto BuildLoginResponse(
        User user,
        string token,
        List<string> permissions)
    {
        return new LoginResponseDto
        {
            Token = token,
            UserId = user.Id,
            FullName = user.FullName,
            FirstNames = user.Name.FirstNames,
            PaternalLastName =
                user.Name.PaternalLastName,
            MaternalLastName =
                user.Name.MaternalLastName,
            Email = user.Email,
            RoleName = user.RoleName,
            UserType = user.UserType,
            ProfileId = user.ProfileId,
            Permissions = permissions
        };
    }

    private static string NormalizeEmail(
        string? email)
    {
        return email?.Trim().ToLowerInvariant()
            ?? string.Empty;
    }

    private static string? NormalizeOptional(
        string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            var address =
                new System.Net.Mail.MailAddress(
                    email);

            return address.Address == email;
        }
        catch
        {
            return false;
        }
    }
}
