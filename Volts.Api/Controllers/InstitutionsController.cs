using System.Net.Mail;
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
public class InstitutionsController : ControllerBase
{
    private readonly MongoDbService _db;

    public InstitutionsController(MongoDbService db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var institutions = await _db.Institutions
            .Find(institution => !institution.IsDeleted)
            .SortByDescending(institution => institution.CreatedAt)
            .ToListAsync();

        return Ok(
            ApiResponse<List<Institution>>.Ok(institutions)
        );
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        var institution = await _db.Institutions
            .Find(item =>
                item.Id == id &&
                !item.IsDeleted)
            .FirstOrDefaultAsync();

        if (institution == null)
        {
            return NotFound(
                ApiResponse<Institution>.Fail(
                    "Institución no encontrada"
                )
            );
        }

        return Ok(
            ApiResponse<Institution>.Ok(institution)
        );
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        InstitutionCreateDto dto)
    {
        var validationErrors = ValidateInstitution(dto);

        if (validationErrors.Count > 0)
        {
            return BadRequest(
                ApiResponse<Institution>.Fail(
                    "Los datos de la institución no son válidos",
                    validationErrors
                )
            );
        }

        var normalizedEmail = NormalizeEmail(dto.Email);

        var emailExists = await _db.Institutions
            .Find(institution =>
                institution.Email == normalizedEmail &&
                !institution.IsDeleted)
            .AnyAsync();

        if (emailExists)
        {
            return BadRequest(
                ApiResponse<Institution>.Fail(
                    "Ya existe una institución con ese correo"
                )
            );
        }

        var institutionType = ResolveInstitutionType(dto);

        var institution = new Institution
        {
            Name = dto.Name.Trim(),

            Type = institutionType,

            /*
             * Se mantiene mientras los documentos antiguos
             * continúan utilizando InstitutionType como texto.
             */
            LegacyInstitutionType =
                NormalizeOptional(dto.InstitutionType),

            EducationalLevel =
                NormalizeOptional(dto.EducationalLevel),

            EstimatedStudents = dto.EstimatedStudents,

            Website = NormalizeOptional(dto.Website),

            Responsible = BuildResponsible(
                dto.Responsible
            ),

            LegacyContactName =
                NormalizeOptional(dto.ContactName),

            Email = normalizedEmail,

            Phone = NormalizeOptional(dto.Phone),

            StructuredAddress =
                NormalizeAddress(dto.StructuredAddress),

            LegacyAddress = NormalizeOptional(dto.Address),

            UserId = NormalizeOptional(dto.UserId),

            IsActive = true
        };

        await _db.Institutions.InsertOneAsync(institution);

        return Ok(
            ApiResponse<Institution>.Ok(
                institution,
                "Institución creada correctamente"
            )
        );
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(
        string id,
        InstitutionUpdateDto dto)
    {
        var institution = await _db.Institutions
            .Find(item =>
                item.Id == id &&
                !item.IsDeleted)
            .FirstOrDefaultAsync();

        if (institution == null)
        {
            return NotFound(
                ApiResponse<Institution>.Fail(
                    "Institución no encontrada"
                )
            );
        }

        var validationErrors = ValidateInstitution(dto);

        if (validationErrors.Count > 0)
        {
            return BadRequest(
                ApiResponse<Institution>.Fail(
                    "Los datos de la institución no son válidos",
                    validationErrors
                )
            );
        }

        var normalizedEmail = NormalizeEmail(dto.Email);

        var emailExists = await _db.Institutions
            .Find(item =>
                item.Id != id &&
                item.Email == normalizedEmail &&
                !item.IsDeleted)
            .AnyAsync();

        if (emailExists)
        {
            return BadRequest(
                ApiResponse<Institution>.Fail(
                    "Ya existe otra institución con ese correo"
                )
            );
        }

        institution.Name = dto.Name.Trim();

        institution.Type = ResolveInstitutionType(dto);

        institution.LegacyInstitutionType =
            NormalizeOptional(dto.InstitutionType);

        institution.EducationalLevel =
            NormalizeOptional(dto.EducationalLevel);

        institution.EstimatedStudents =
            dto.EstimatedStudents;

        institution.Website =
            NormalizeOptional(dto.Website);

        institution.Responsible =
            BuildResponsible(dto.Responsible);

        institution.LegacyContactName =
            NormalizeOptional(dto.ContactName);

        institution.Email = normalizedEmail;

        institution.Phone =
            NormalizeOptional(dto.Phone);

        institution.StructuredAddress =
            NormalizeAddress(dto.StructuredAddress);

        institution.LegacyAddress =
            NormalizeOptional(dto.Address);

        institution.UserId =
            NormalizeOptional(dto.UserId);

        institution.IsActive = dto.IsActive;
        institution.UpdatedAt = DateTime.UtcNow;

        await _db.Institutions.ReplaceOneAsync(
            item => item.Id == id,
            institution
        );

        return Ok(
            ApiResponse<Institution>.Ok(
                institution,
                "Institución actualizada correctamente"
            )
        );
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var update = Builders<Institution>.Update
            .Set(institution => institution.IsDeleted, true)
            .Set(institution => institution.IsActive, false)
            .Set(
                institution => institution.UpdatedAt,
                DateTime.UtcNow
            );

        var result = await _db.Institutions.UpdateOneAsync(
            institution =>
                institution.Id == id &&
                !institution.IsDeleted,
            update
        );

        if (result.ModifiedCount == 0)
        {
            return NotFound(
                ApiResponse<string>.Fail(
                    "Institución no encontrada"
                )
            );
        }

        return Ok(
            ApiResponse<string>.Ok(
                "Institución eliminada correctamente"
            )
        );
    }

    private static List<string> ValidateInstitution(
        InstitutionCreateDto dto)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            errors.Add(
                "El nombre de la institución es obligatorio"
            );
        }

        if (string.IsNullOrWhiteSpace(dto.Email))
        {
            errors.Add(
                "El correo electrónico de la institución es obligatorio"
            );
        }
        else if (!IsValidEmail(dto.Email))
        {
            errors.Add(
                "El correo electrónico de la institución no es válido"
            );
        }

        var hasStructuredResponsible =
            dto.Responsible != null &&
            dto.Responsible.Name != null &&
            dto.Responsible.Name.HasStructuredName;

        var hasLegacyResponsible =
            !string.IsNullOrWhiteSpace(dto.ContactName);

        if (!hasStructuredResponsible &&
            !hasLegacyResponsible)
        {
            errors.Add(
                "Debes proporcionar el nombre del responsable"
            );
        }

        if (dto.Responsible != null &&
            !string.IsNullOrWhiteSpace(dto.Responsible.Email) &&
            !IsValidEmail(dto.Responsible.Email))
        {
            errors.Add(
                "El correo electrónico del responsable no es válido"
            );
        }

        if (dto.EstimatedStudents.HasValue &&
            dto.EstimatedStudents.Value < 0)
        {
            errors.Add(
                "La cantidad estimada de alumnos no puede ser negativa"
            );
        }

        if (!dto.Type.HasValue &&
            !string.IsNullOrWhiteSpace(dto.InstitutionType) &&
            !Enum.TryParse<InstitutionType>(
                dto.InstitutionType.Trim(),
                true,
                out _))
        {
            errors.Add(
                "El tipo de institución no es válido"
            );
        }

        if (dto.StructuredAddress != null)
        {
            ValidateAddress(
                dto.StructuredAddress,
                errors
            );
        }

        return errors;
    }

    private static InstitutionType? ResolveInstitutionType(
        InstitutionCreateDto dto)
    {
        if (dto.Type.HasValue)
            return dto.Type.Value;

        if (
            !string.IsNullOrWhiteSpace(dto.InstitutionType) &&
            Enum.TryParse<InstitutionType>(
                dto.InstitutionType.Trim(),
                true,
                out var parsedType))
        {
            return parsedType;
        }

        return null;
    }

    private static InstitutionResponsible BuildResponsible(
        InstitutionResponsible? responsible)
    {
        if (responsible == null)
            return new InstitutionResponsible();

        return new InstitutionResponsible
        {
            Name = new PersonName
            {
                FirstNames =
                    responsible.Name?.FirstNames?.Trim()
                    ?? string.Empty,

                PaternalLastName =
                    responsible.Name?.PaternalLastName?.Trim()
                    ?? string.Empty,

                MaternalLastName =
                    NormalizeOptional(
                        responsible.Name?.MaternalLastName
                    )
            },

            Email = string.IsNullOrWhiteSpace(
                responsible.Email)
                ? string.Empty
                : NormalizeEmail(responsible.Email),

            Phone = NormalizeOptional(responsible.Phone),

            Position = NormalizeOptional(
                responsible.Position
            )
        };
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

    private static Address? NormalizeAddress(
        Address? address)
    {
        if (address == null)
            return null;

        return new Address
        {
            Street =
                address.Street?.Trim() ?? string.Empty,

            ExteriorNumber =
                address.ExteriorNumber?.Trim()
                ?? string.Empty,

            InteriorNumber =
                NormalizeOptional(
                    address.InteriorNumber
                ),

            Neighborhood =
                address.Neighborhood?.Trim()
                ?? string.Empty,

            PostalCode =
                address.PostalCode?.Trim()
                ?? string.Empty,

            City =
                address.City?.Trim()
                ?? string.Empty,

            State =
                address.State?.Trim()
                ?? string.Empty,

            Country =
                string.IsNullOrWhiteSpace(address.Country)
                    ? "México"
                    : address.Country.Trim(),

            References =
                NormalizeOptional(address.References)
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