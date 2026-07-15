using System.Net.Mail;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using Volts.Api.DTOs;
using Volts.Api.Models;
using Volts.Api.Models.Common;
using Volts.Api.Responses;
using Volts.Api.Services;

namespace Volts.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin,Employee")]
public class CustomersController : ControllerBase
{
    private readonly MongoDbService _db;

    public CustomersController(MongoDbService db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var customers = await _db.Customers
            .Find(customer => !customer.IsDeleted)
            .SortByDescending(customer => customer.CreatedAt)
            .ToListAsync();

        return Ok(ApiResponse<List<Customer>>.Ok(customers));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        var customer = await _db.Customers
            .Find(customer =>
                customer.Id == id &&
                !customer.IsDeleted)
            .FirstOrDefaultAsync();

        if (customer == null)
        {
            return NotFound(
                ApiResponse<Customer>.Fail("Cliente no encontrado")
            );
        }

        return Ok(ApiResponse<Customer>.Ok(customer));
    }

    [HttpPost]
    public async Task<IActionResult> Create(CustomerCreateDto dto)
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

        var normalizedEmail = NormalizeEmail(dto.Email);

        var emailExists = await _db.Customers
            .Find(customer =>
                customer.Email == normalizedEmail &&
                !customer.IsDeleted)
            .AnyAsync();

        if (emailExists)
        {
            return BadRequest(
                ApiResponse<Customer>.Fail(
                    "Ya existe un cliente con ese correo"
                )
            );
        }

        var customer = new Customer
        {
            Name = BuildPersonName(
                dto.FirstNames,
                dto.PaternalLastName,
                dto.MaternalLastName
            ),

            /*
             * Si el frontend anterior todavía envía FullName,
             * se conserva en el campo antiguo.
             */
            LegacyFullName = NormalizeOptional(dto.FullName),

            Email = normalizedEmail,
            Phone = NormalizeOptional(dto.Phone),

            StructuredAddress = NormalizeAddress(
                dto.StructuredAddress
            ),

            LegacyAddress = NormalizeOptional(dto.Address),

            UserId = NormalizeOptional(dto.UserId),

            CustomerType = string.IsNullOrWhiteSpace(dto.CustomerType)
                ? "Individual"
                : dto.CustomerType.Trim(),

            InstitutionName = NormalizeOptional(
                dto.InstitutionName
            ),

            IsActive = true
        };

        await _db.Customers.InsertOneAsync(customer);

        return Ok(
            ApiResponse<Customer>.Ok(
                customer,
                "Cliente creado correctamente"
            )
        );
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(
        string id,
        CustomerUpdateDto dto)
    {
        var customer = await _db.Customers
            .Find(item =>
                item.Id == id &&
                !item.IsDeleted)
            .FirstOrDefaultAsync();

        if (customer == null)
        {
            return NotFound(
                ApiResponse<Customer>.Fail("Cliente no encontrado")
            );
        }

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

        var normalizedEmail = NormalizeEmail(dto.Email);

        var emailExists = await _db.Customers
            .Find(item =>
                item.Id != id &&
                item.Email == normalizedEmail &&
                !item.IsDeleted)
            .AnyAsync();

        if (emailExists)
        {
            return BadRequest(
                ApiResponse<Customer>.Fail(
                    "Ya existe otro cliente con ese correo"
                )
            );
        }

        customer.Name = BuildPersonName(
            dto.FirstNames,
            dto.PaternalLastName,
            dto.MaternalLastName
        );

        customer.LegacyFullName = NormalizeOptional(dto.FullName);

        customer.Email = normalizedEmail;
        customer.Phone = NormalizeOptional(dto.Phone);

        customer.StructuredAddress = NormalizeAddress(
            dto.StructuredAddress
        );

        customer.LegacyAddress = NormalizeOptional(dto.Address);

        customer.UserId = NormalizeOptional(dto.UserId);

        customer.CustomerType =
            string.IsNullOrWhiteSpace(dto.CustomerType)
                ? "Individual"
                : dto.CustomerType.Trim();

        customer.InstitutionName = NormalizeOptional(
            dto.InstitutionName
        );

        customer.IsActive = dto.IsActive;
        customer.UpdatedAt = DateTime.UtcNow;

        await _db.Customers.ReplaceOneAsync(
            item => item.Id == id,
            customer
        );

        return Ok(
            ApiResponse<Customer>.Ok(
                customer,
                "Cliente actualizado correctamente"
            )
        );
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var update = Builders<Customer>.Update
            .Set(customer => customer.IsDeleted, true)
            .Set(customer => customer.IsActive, false)
            .Set(customer => customer.UpdatedAt, DateTime.UtcNow);

        var result = await _db.Customers.UpdateOneAsync(
            customer =>
                customer.Id == id &&
                !customer.IsDeleted,
            update
        );

        if (result.ModifiedCount == 0)
        {
            return NotFound(
                ApiResponse<string>.Fail("Cliente no encontrado")
            );
        }

        return Ok(
            ApiResponse<string>.Ok(
                "Cliente eliminado correctamente"
            )
        );
    }

    private static List<string> ValidateCustomer(
        CustomerCreateDto dto)
    {
        var errors = new List<string>();

        var hasStructuredName =
            !string.IsNullOrWhiteSpace(dto.FirstNames) &&
            !string.IsNullOrWhiteSpace(dto.PaternalLastName);

        var hasLegacyName =
            !string.IsNullOrWhiteSpace(dto.FullName);

        if (!hasStructuredName && !hasLegacyName)
        {
            errors.Add(
                "Debes proporcionar el nombre y apellido paterno del cliente"
            );
        }

        if (string.IsNullOrWhiteSpace(dto.Email))
        {
            errors.Add("El correo electrónico es obligatorio");
        }
        else if (!IsValidEmail(dto.Email))
        {
            errors.Add("El correo electrónico no tiene un formato válido");
        }

        if (dto.StructuredAddress != null)
        {
            ValidateAddress(dto.StructuredAddress, errors);
        }

        return errors;
    }

    private static void ValidateAddress(
        Address address,
        List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(address.Street))
            errors.Add("La calle es obligatoria");

        if (string.IsNullOrWhiteSpace(address.ExteriorNumber))
            errors.Add("El número exterior es obligatorio");

        if (string.IsNullOrWhiteSpace(address.Neighborhood))
            errors.Add("La colonia es obligatoria");

        if (string.IsNullOrWhiteSpace(address.PostalCode))
        {
            errors.Add("El código postal es obligatorio");
        }
        else if (
            address.PostalCode.Length != 5 ||
            !address.PostalCode.All(char.IsDigit))
        {
            errors.Add(
                "El código postal debe contener exactamente 5 dígitos"
            );
        }

        if (string.IsNullOrWhiteSpace(address.City))
            errors.Add("La ciudad es obligatoria");

        if (string.IsNullOrWhiteSpace(address.State))
            errors.Add("El estado es obligatorio");

        if (string.IsNullOrWhiteSpace(address.Country))
            errors.Add("El país es obligatorio");
    }

    private static PersonName BuildPersonName(
        string? firstNames,
        string? paternalLastName,
        string? maternalLastName)
    {
        return new PersonName
        {
            FirstNames = firstNames?.Trim() ?? string.Empty,
            PaternalLastName =
                paternalLastName?.Trim() ?? string.Empty,
            MaternalLastName =
                NormalizeOptional(maternalLastName)
        };
    }

    private static Address? NormalizeAddress(Address? address)
    {
        if (address == null)
            return null;

        return new Address
        {
            Street = address.Street?.Trim() ?? string.Empty,
            ExteriorNumber =
                address.ExteriorNumber?.Trim() ?? string.Empty,
            InteriorNumber =
                NormalizeOptional(address.InteriorNumber),
            Neighborhood =
                address.Neighborhood?.Trim() ?? string.Empty,
            PostalCode =
                address.PostalCode?.Trim() ?? string.Empty,
            City = address.City?.Trim() ?? string.Empty,
            State = address.State?.Trim() ?? string.Empty,
            Country = string.IsNullOrWhiteSpace(address.Country)
                ? "México"
                : address.Country.Trim(),
            References = NormalizeOptional(address.References)
        };
    }

    private static string NormalizeEmail(string email)
    {
        return email.Trim().ToLowerInvariant();
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            var mailAddress = new MailAddress(email.Trim());

            return string.Equals(
                mailAddress.Address,
                email.Trim(),
                StringComparison.OrdinalIgnoreCase
            );
        }
        catch
        {
            return false;
        }
    }
}