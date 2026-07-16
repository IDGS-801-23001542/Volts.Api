using System.Net.Mail;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using Volts.Api.DTOs;
using Volts.Api.Models;
using Volts.Api.Models.Common;
using Volts.Api.Models.Enums;
using Volts.Api.Responses;
using Volts.Api.Services;

namespace Volts.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin,Employee")]
public class CustomersController : ControllerBase
{
    private readonly MongoDbService _db;
    private readonly TemporaryPasswordService _passwords;

    public CustomersController(
        MongoDbService db,
        TemporaryPasswordService passwords)
    {
        _db = db;
        _passwords = passwords;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var customers = await _db.Customers
            .Find(x => !x.IsDeleted)
            .SortByDescending(x => x.CreatedAt)
            .ToListAsync();

        return Ok(ApiResponse<List<Customer>>.Ok(customers));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        var customer = await _db.Customers
            .Find(x => x.Id == id && !x.IsDeleted)
            .FirstOrDefaultAsync();

        if (customer == null)
            return NotFound(ApiResponse<Customer>.Fail("Cliente no encontrado"));

        return Ok(ApiResponse<Customer>.Ok(customer));
    }

    [HttpPost]
    public async Task<IActionResult> Create(CustomerCreateDto dto)
    {
        var validationErrors = ValidateCustomer(dto);

        if (dto.CreatePortalAccount && !dto.AutoGeneratePassword)
        {
            validationErrors.AddRange(
                _passwords.Validate(dto.TemporaryPassword)
            );
        }

        if (validationErrors.Count > 0)
        {
            return BadRequest(
                ApiResponse<EntityWithPortalAccountDto<Customer>>.Fail(
                    "Los datos del cliente no son válidos",
                    validationErrors
                )
            );
        }

        var normalizedEmail = NormalizeEmail(dto.Email);

        var customerExists = await _db.Customers
            .Find(x =>
                x.Email == normalizedEmail &&
                !x.IsDeleted)
            .AnyAsync();

        if (customerExists)
        {
            return BadRequest(
                ApiResponse<EntityWithPortalAccountDto<Customer>>.Fail(
                    "Ya existe un cliente con ese correo"
                )
            );
        }

        if (dto.CreatePortalAccount)
        {
            var userExists = await _db.Users
                .Find(x =>
                    x.Email == normalizedEmail &&
                    !x.IsDeleted)
                .AnyAsync();

            if (userExists)
            {
                return BadRequest(
                    ApiResponse<EntityWithPortalAccountDto<Customer>>.Fail(
                        "Ya existe una cuenta de acceso con ese correo"
                    )
                );
            }
        }

        var customer = new Customer
        {
            Name = MapName(dto.Name),
            LegacyFullName = null,
            Email = normalizedEmail,
            Phone = NormalizeOptional(dto.Phone),
            Address = MapAddress(dto.Address),
            IsActive = true,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = CurrentUserId()
        };

        PortalAccountCredentialsDto? credentials = null;

        using var session = await _db.StartSessionAsync();
        session.StartTransaction();

        try
        {
            await _db.Customers.InsertOneAsync(
                session,
                customer
            );

            if (dto.CreatePortalAccount)
            {
                var role = await _db.Roles
                    .Find(
                        session,
                        x =>
                            x.Name == "Client" &&
                            x.IsActive &&
                            !x.IsDeleted
                    )
                    .FirstOrDefaultAsync();

                if (role == null)
                {
                    throw new InvalidOperationException(
                        "El rol Client no está configurado."
                    );
                }

                var temporaryPassword = dto.AutoGeneratePassword
                    ? _passwords.Generate()
                    : dto.TemporaryPassword!.Trim();

                var user = new User
                {
                    Name = customer.Name,
                    LegacyFullName = null,
                    Email = customer.Email,
                    PasswordHash =
                        BCrypt.Net.BCrypt.HashPassword(
                            temporaryPassword
                        ),
                    RoleId = role.Id,
                    RoleName = role.Name,
                    UserType = UserType.Customer,
                    ProfileId = customer.Id,
                    IsActive = true,
                    IsEmailConfirmed = true,
                    TwoFactorEnabled = false,
                    MustChangePassword = true,
                    FailedLoginAttempts = 0,
                    IsDeleted = false,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = CurrentUserId()
                };

                await _db.Users.InsertOneAsync(
                    session,
                    user
                );

                credentials = new PortalAccountCredentialsDto
                {
                    Created = true,
                    Email = user.Email,
                    TemporaryPassword = temporaryPassword,
                    MustChangePassword = true
                };
            }

            await session.CommitTransactionAsync();
        }
        catch (InvalidOperationException exception)
        {
            await session.AbortTransactionAsync();

            return BadRequest(
                ApiResponse<EntityWithPortalAccountDto<Customer>>.Fail(
                    exception.Message
                )
            );
        }
        catch
        {
            await session.AbortTransactionAsync();
            throw;
        }

        return Ok(
            ApiResponse<EntityWithPortalAccountDto<Customer>>.Ok(
                new EntityWithPortalAccountDto<Customer>
                {
                    Entity = customer,
                    PortalAccount = credentials
                },
                credentials == null
                    ? "Cliente creado correctamente"
                    : "Cliente y cuenta de portal creados correctamente"
            )
        );
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(
        string id,
        CustomerUpdateDto dto)
    {
        var validationErrors = ValidateCustomer(dto);

        if (validationErrors.Count > 0)
        {
            return BadRequest(
                ApiResponse<Customer>.Fail(
                    "Los datos del cliente no son válidos",
                    validationErrors
                )
            );
        }

        var customer = await _db.Customers
            .Find(x =>
                x.Id == id &&
                !x.IsDeleted)
            .FirstOrDefaultAsync();

        if (customer == null)
            return NotFound(ApiResponse<Customer>.Fail("Cliente no encontrado"));

        var normalizedEmail = NormalizeEmail(dto.Email);

        var emailInUse = await _db.Customers
            .Find(x =>
                x.Id != id &&
                x.Email == normalizedEmail &&
                !x.IsDeleted)
            .AnyAsync();

        if (emailInUse)
            return BadRequest(ApiResponse<Customer>.Fail("El correo ya pertenece a otro cliente"));

        customer.Name = MapName(dto.Name);
        customer.LegacyFullName = null;
        customer.Email = normalizedEmail;
        customer.Phone = NormalizeOptional(dto.Phone);
        customer.Address = MapAddress(dto.Address);
        customer.IsActive = dto.IsActive;
        customer.UpdatedAt = DateTime.UtcNow;
        customer.UpdatedBy = CurrentUserId();

        await _db.Customers.ReplaceOneAsync(
            x => x.Id == id,
            customer
        );

        return Ok(
            ApiResponse<Customer>.Ok(
                customer,
                "Cliente actualizado correctamente"
            )
        );
    }

    [HttpPatch("{id}/status")]
    public async Task<IActionResult> UpdateStatus(
        string id,
        EntityStatusUpdateDto dto)
    {
        var update = Builders<Customer>.Update
            .Set(x => x.IsActive, dto.IsActive)
            .Set(x => x.UpdatedAt, DateTime.UtcNow)
            .Set(x => x.UpdatedBy, CurrentUserId());

        var options = new FindOneAndUpdateOptions<Customer>
        {
            ReturnDocument = ReturnDocument.After
        };

        var customer = await _db.Customers.FindOneAndUpdateAsync(
            x => x.Id == id && !x.IsDeleted,
            update,
            options
        );

        if (customer == null)
            return NotFound(ApiResponse<Customer>.Fail("Cliente no encontrado"));

        return Ok(
            ApiResponse<Customer>.Ok(
                customer,
                dto.IsActive
                    ? "Cliente activado correctamente"
                    : "Cliente desactivado correctamente"
            )
        );
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var update = Builders<Customer>.Update
            .Set(x => x.IsDeleted, true)
            .Set(x => x.IsActive, false)
            .Set(x => x.UpdatedAt, DateTime.UtcNow)
            .Set(x => x.UpdatedBy, CurrentUserId());

        var result = await _db.Customers.UpdateOneAsync(
            x => x.Id == id && !x.IsDeleted,
            update
        );

        if (result.ModifiedCount == 0)
            return NotFound(ApiResponse<string>.Fail("Cliente no encontrado"));

        return Ok(ApiResponse<string>.Ok("Cliente eliminado correctamente"));
    }

    private string? CurrentUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier);
    }

    private static List<string> ValidateCustomer(
        CustomerCreateDto dto)
    {
        return ValidateCustomerCore(
            dto.Name,
            dto.Email,
            dto.Phone,
            dto.Address
        );
    }

    private static List<string> ValidateCustomer(
        CustomerUpdateDto dto)
    {
        return ValidateCustomerCore(
            dto.Name,
            dto.Email,
            dto.Phone,
            dto.Address
        );
    }

    private static List<string> ValidateCustomerCore(
        PersonNameDto name,
        string email,
        string? phone,
        AddressDto? address)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(name.FirstNames))
            errors.Add("Los nombres son obligatorios");

        if (string.IsNullOrWhiteSpace(name.PaternalLastName))
            errors.Add("El apellido paterno es obligatorio");

        if (string.IsNullOrWhiteSpace(email))
            errors.Add("El correo electrónico es obligatorio");
        else if (!IsValidEmail(email))
            errors.Add("El correo electrónico no tiene un formato válido");

        if (!string.IsNullOrWhiteSpace(phone) &&
            (
                phone.Trim().Length != 10 ||
                !phone.Trim().All(char.IsDigit)
            ))
        {
            errors.Add("El teléfono debe contener exactamente 10 dígitos");
        }

        ValidateAddress(address, errors);

        return errors;
    }

    private static void ValidateAddress(
        AddressDto? address,
        List<string> errors)
    {
        if (address == null)
            return;

        if (string.IsNullOrWhiteSpace(address.Street))
            errors.Add("La calle es obligatoria cuando se captura una dirección");

        if (string.IsNullOrWhiteSpace(address.ExteriorNumber))
            errors.Add("El número exterior es obligatorio cuando se captura una dirección");

        if (string.IsNullOrWhiteSpace(address.Neighborhood))
            errors.Add("La colonia es obligatoria cuando se captura una dirección");

        if (string.IsNullOrWhiteSpace(address.PostalCode) ||
            address.PostalCode.Trim().Length != 5 ||
            !address.PostalCode.All(char.IsDigit))
        {
            errors.Add("El código postal debe contener 5 dígitos");
        }

        if (string.IsNullOrWhiteSpace(address.City))
            errors.Add("La ciudad es obligatoria cuando se captura una dirección");

        if (string.IsNullOrWhiteSpace(address.State))
            errors.Add("El estado es obligatorio cuando se captura una dirección");
    }

    private static PersonName MapName(PersonNameDto dto)
    {
        return new PersonName
        {
            FirstNames = dto.FirstNames.Trim(),
            PaternalLastName = dto.PaternalLastName.Trim(),
            MaternalLastName = NormalizeOptional(dto.MaternalLastName)
        };
    }

    private static Address? MapAddress(AddressDto? dto)
    {
        if (dto == null)
            return null;

        return new Address
        {
            Street = dto.Street.Trim(),
            ExteriorNumber = dto.ExteriorNumber.Trim(),
            InteriorNumber = NormalizeOptional(dto.InteriorNumber),
            Neighborhood = dto.Neighborhood.Trim(),
            PostalCode = dto.PostalCode.Trim(),
            City = dto.City.Trim(),
            State = dto.State.Trim(),
            Country = string.IsNullOrWhiteSpace(dto.Country)
                ? "México"
                : dto.Country.Trim(),
            References = NormalizeOptional(dto.References)
        };
    }

    private static string NormalizeEmail(string email)
        => email.Trim().ToLowerInvariant();

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();

    private static bool IsValidEmail(string email)
    {
        try
        {
            return new MailAddress(email.Trim()).Address == email.Trim();
        }
        catch
        {
            return false;
        }
    }
}
