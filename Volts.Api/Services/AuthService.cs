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

    public AuthService(
        MongoDbService db,
        JwtService jwtService)
    {
        _db = db;
        _jwtService = jwtService;
    }

    public async Task<ApiResponse<LoginResponseDto>>
        LoginAsync(LoginRequestDto dto)
    {
        var normalizedEmail = NormalizeEmail(dto.Email);

        if (string.IsNullOrWhiteSpace(normalizedEmail) ||
            string.IsNullOrWhiteSpace(dto.Password))
        {
            return ApiResponse<LoginResponseDto>.Fail(
                "El correo y la contraseña son obligatorios"
            );
        }

        var user = await _db.Users
            .Find(x =>
                x.Email == normalizedEmail &&
                !x.IsDeleted)
            .FirstOrDefaultAsync();

        if (user == null)
        {
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
            var remainingMinutes = Math.Max(
                1,
                (int)Math.Ceiling(
                    (user.LockoutEnd.Value - DateTime.UtcNow)
                    .TotalMinutes
                )
            );

            return ApiResponse<LoginResponseDto>.Fail(
                $"Usuario bloqueado temporalmente. Intenta nuevamente en {remainingMinutes} minuto(s)"
            );
        }

        var validPassword = BCrypt.Net.BCrypt.Verify(
            dto.Password,
            user.PasswordHash
        );

        if (!validPassword)
        {
            await RegisterFailedLoginAsync(user);

            return ApiResponse<LoginResponseDto>.Fail(
                "Correo o contraseña incorrectos"
            );
        }

        /*
         * Corrige automáticamente usuarios antiguos que todavía
         * no tienen UserType almacenado.
         */
        user.UserType = ResolveUserType(user.RoleName);
        user.FailedLoginAttempts = 0;
        user.LockoutEnd = null;
        user.LastLoginAt = DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;

        await _db.Users.ReplaceOneAsync(
            x => x.Id == user.Id,
            user
        );

        var token = _jwtService.GenerateToken(user);

        return ApiResponse<LoginResponseDto>.Ok(
            BuildLoginResponse(user, token),
            "Login correcto"
        );
    }

    public async Task<ApiResponse<User>>
        CreateUserAsync(CreateUserDto dto)
    {
        var normalizedEmail = NormalizeEmail(dto.Email);

        if (string.IsNullOrWhiteSpace(normalizedEmail))
        {
            return ApiResponse<User>.Fail(
                "El correo electrónico es obligatorio"
            );
        }

        if (!IsValidEmail(normalizedEmail))
        {
            return ApiResponse<User>.Fail(
                "El correo electrónico no tiene un formato válido"
            );
        }

        if (string.IsNullOrWhiteSpace(dto.Password) ||
            dto.Password.Length < 8)
        {
            return ApiResponse<User>.Fail(
                "La contraseña debe tener al menos 8 caracteres"
            );
        }

        var nameResult = BuildPersonName(
            dto.FirstNames,
            dto.PaternalLastName,
            dto.MaternalLastName,
            dto.FullName
        );

        if (!nameResult.Success || nameResult.Name == null)
        {
            return ApiResponse<User>.Fail(
                nameResult.ErrorMessage
            );
        }

        var exists = await _db.Users
            .Find(x =>
                x.Email == normalizedEmail &&
                !x.IsDeleted)
            .AnyAsync();

        if (exists)
        {
            return ApiResponse<User>.Fail(
                "Ya existe un usuario con ese correo"
            );
        }

        var normalizedRoleName =
            NormalizeRoleName(dto.RoleName);

        var role = await _db.Roles
            .Find(x =>
                x.Name == normalizedRoleName &&
                x.IsActive &&
                !x.IsDeleted)
            .FirstOrDefaultAsync();

        if (role == null)
        {
            return ApiResponse<User>.Fail(
                "El rol seleccionado no existe o está inactivo"
            );
        }

        var user = new User
        {
            Name = nameResult.Name,
            LegacyFullName = null,
            Email = normalizedEmail,
            PasswordHash =
                BCrypt.Net.BCrypt.HashPassword(
                    dto.Password
                ),
            RoleId = role.Id,
            RoleName = role.Name,
            UserType = ResolveUserType(role.Name),
            ProfileId = null,
            IsActive = true,
            IsEmailConfirmed = false,
            TwoFactorEnabled = false,
            FailedLoginAttempts = 0,
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
        var normalizedEmail = NormalizeEmail(dto.Email);

        if (string.IsNullOrWhiteSpace(normalizedEmail))
        {
            return ApiResponse<LoginResponseDto>.Fail(
                "El correo electrónico es obligatorio"
            );
        }

        if (!IsValidEmail(normalizedEmail))
        {
            return ApiResponse<LoginResponseDto>.Fail(
                "El correo electrónico no tiene un formato válido"
            );
        }

        if (string.IsNullOrWhiteSpace(dto.Password) ||
            dto.Password.Length < 8)
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

        var nameResult = BuildPersonName(
            dto.FirstNames,
            dto.PaternalLastName,
            dto.MaternalLastName,
            dto.FullName
        );

        if (!nameResult.Success || nameResult.Name == null)
        {
            return ApiResponse<LoginResponseDto>.Fail(
                nameResult.ErrorMessage
            );
        }

        var userExists = await _db.Users
            .Find(x =>
                x.Email == normalizedEmail &&
                !x.IsDeleted)
            .AnyAsync();

        if (userExists)
        {
            return ApiResponse<LoginResponseDto>.Fail(
                "Ya existe una cuenta con ese correo"
            );
        }

        var customerExists = await _db.Customers
            .Find(x =>
                x.Email == normalizedEmail &&
                !x.IsDeleted)
            .AnyAsync();

        if (customerExists)
        {
            return ApiResponse<LoginResponseDto>.Fail(
                "Ya existe un cliente registrado con ese correo"
            );
        }

        var clientRole = await _db.Roles
            .Find(x =>
                x.Name == "Client" &&
                x.IsActive &&
                !x.IsDeleted)
            .FirstOrDefaultAsync();

        if (clientRole == null)
        {
            return ApiResponse<LoginResponseDto>.Fail(
                "El rol de cliente no está configurado"
            );
        }

        using var session =
            await _db.StartSessionAsync();

        session.StartTransaction();

        try
        {
            var user = new User
            {
                Name = nameResult.Name,
                LegacyFullName = null,
                Email = normalizedEmail,
                PasswordHash =
                    BCrypt.Net.BCrypt.HashPassword(
                        dto.Password
                    ),
                RoleId = clientRole.Id,
                RoleName = clientRole.Name,
                UserType = UserType.Customer,
                ProfileId = null,
                IsActive = true,
                IsEmailConfirmed = false,
                TwoFactorEnabled = false,
                FailedLoginAttempts = 0,
                CreatedAt = DateTime.UtcNow
            };

            await _db.Users.InsertOneAsync(
                session,
                user
            );

            /*
             * Customer todavía conserva temporalmente su modelo
             * anterior. Lo migraremos en el siguiente paso.
             */
            var customer = new Customer
            {
                CustomerType = "Individual",
                FullName = nameResult.Name.FullName,
                InstitutionName = null,
                Email = normalizedEmail,
                Phone = NormalizeOptional(dto.Phone),
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
            user.UpdatedAt = DateTime.UtcNow;

            await _db.Users.ReplaceOneAsync(
                session,
                x => x.Id == user.Id,
                user
            );

            await session.CommitTransactionAsync();

            var token =
                _jwtService.GenerateToken(user);

            return ApiResponse<LoginResponseDto>.Ok(
                BuildLoginResponse(user, token),
                "Cuenta creada correctamente"
            );
        }
        catch
        {
            await session.AbortTransactionAsync();

            return ApiResponse<LoginResponseDto>.Fail(
                "No fue posible completar el registro. Intenta nuevamente"
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

            /*
             * Reiniciamos el contador porque el bloqueo ya fue
             * aplicado. Después del periodo podrá volver a intentar.
             */
            user.FailedLoginAttempts = 0;
        }

        user.UpdatedAt = DateTime.UtcNow;

        await _db.Users.ReplaceOneAsync(
            x => x.Id == user.Id,
            user
        );
    }

    private static LoginResponseDto BuildLoginResponse(
        User user,
        string token)
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
            ProfileId = user.ProfileId
        };
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

    private static string NormalizeRoleName(
        string? roleName)
    {
        if (string.IsNullOrWhiteSpace(roleName))
            return "Employee";

        return roleName.Trim().ToLowerInvariant()
            switch
        {
            "admin" => "Admin",
            "employee" => "Employee",
            "client" => "Client",
            "institution" => "Institution",
            _ => roleName.Trim()
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

    private static bool IsValidEmail(
        string email)
    {
        try
        {
            var address =
                new System.Net.Mail.MailAddress(email);

            return address.Address == email;
        }
        catch
        {
            return false;
        }
    }

    private static PersonNameResult BuildPersonName(
        string? firstNames,
        string? paternalLastName,
        string? maternalLastName,
        string? legacyFullName)
    {
        var normalizedFirstNames =
            NormalizeOptional(firstNames);

        var normalizedPaternalLastName =
            NormalizeOptional(paternalLastName);

        var normalizedMaternalLastName =
            NormalizeOptional(maternalLastName);

        if (!string.IsNullOrWhiteSpace(
                normalizedFirstNames) &&
            !string.IsNullOrWhiteSpace(
                normalizedPaternalLastName))
        {
            return PersonNameResult.Ok(
                new PersonName
                {
                    FirstNames =
                        normalizedFirstNames,
                    PaternalLastName =
                        normalizedPaternalLastName,
                    MaternalLastName =
                        normalizedMaternalLastName
                }
            );
        }

        /*
         * Compatibilidad con el formulario Angular anterior.
         * El último elemento se toma como apellido paterno.
         */
        var normalizedLegacyName =
            NormalizeOptional(legacyFullName);

        if (string.IsNullOrWhiteSpace(
                normalizedLegacyName))
        {
            return PersonNameResult.Fail(
                "Los nombres y el apellido paterno son obligatorios"
            );
        }

        var parts = normalizedLegacyName
            .Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries
            );

        if (parts.Length < 2)
        {
            return PersonNameResult.Fail(
                "Ingresa al menos un nombre y un apellido"
            );
        }

        return PersonNameResult.Ok(
            new PersonName
            {
                FirstNames = string.Join(
                    " ",
                    parts.Take(parts.Length - 1)
                ),
                PaternalLastName = parts[^1],
                MaternalLastName = null
            }
        );
    }

    private sealed class PersonNameResult
    {
        public bool Success { get; private init; }

        public PersonName? Name { get; private init; }

        public string ErrorMessage { get; private init; }
            = string.Empty;

        public static PersonNameResult Ok(
            PersonName name)
        {
            return new PersonNameResult
            {
                Success = true,
                Name = name
            };
        }

        public static PersonNameResult Fail(
            string message)
        {
            return new PersonNameResult
            {
                Success = false,
                ErrorMessage = message
            };
        }
    }
}