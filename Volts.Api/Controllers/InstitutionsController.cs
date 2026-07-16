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
public class InstitutionsController : ControllerBase
{
    private readonly MongoDbService _db;
    private readonly TemporaryPasswordService _passwords;

    public InstitutionsController(
        MongoDbService db,
        TemporaryPasswordService passwords)
    {
        _db = db;
        _passwords = passwords;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var institutions = await _db.Institutions
            .Find(x => !x.IsDeleted)
            .SortByDescending(x => x.CreatedAt)
            .ToListAsync();

        return Ok(ApiResponse<List<Institution>>.Ok(institutions));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        var institution = await _db.Institutions
            .Find(x => x.Id == id && !x.IsDeleted)
            .FirstOrDefaultAsync();

        if (institution == null)
            return NotFound(ApiResponse<Institution>.Fail("Institución no encontrada"));

        return Ok(ApiResponse<Institution>.Ok(institution));
    }

    [HttpPost]
    public async Task<IActionResult> Create(InstitutionCreateDto dto)
    {
        var validationErrors = ValidateInstitution(dto);

        if (dto.CreatePortalAccount && !dto.AutoGeneratePassword)
        {
            validationErrors.AddRange(
                _passwords.Validate(dto.TemporaryPassword)
            );
        }

        if (validationErrors.Count > 0)
        {
            return BadRequest(
                ApiResponse<EntityWithPortalAccountDto<Institution>>.Fail(
                    "Los datos de la institución no son válidos",
                    validationErrors
                )
            );
        }

        var normalizedEmail =
            NormalizeEmail(dto.Responsible.Email);

        var duplicateName = await _db.Institutions
            .Find(x =>
                x.Name.ToLower() ==
                    dto.Name.Trim().ToLower() &&
                !x.IsDeleted)
            .AnyAsync();

        if (duplicateName)
        {
            return BadRequest(
                ApiResponse<EntityWithPortalAccountDto<Institution>>.Fail(
                    "Ya existe una institución con ese nombre"
                )
            );
        }

        var duplicateEmail = await _db.Institutions
            .Find(x =>
                x.Responsible.Email ==
                    normalizedEmail &&
                !x.IsDeleted)
            .AnyAsync();

        if (duplicateEmail)
        {
            return BadRequest(
                ApiResponse<EntityWithPortalAccountDto<Institution>>.Fail(
                    "Ya existe una institución con ese correo de responsable"
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
                    ApiResponse<EntityWithPortalAccountDto<Institution>>.Fail(
                        "Ya existe una cuenta de acceso con ese correo"
                    )
                );
            }
        }

        var institution = new Institution
        {
            Name = dto.Name.Trim(),
            InstitutionType = dto.InstitutionType,
            Responsible = MapResponsible(dto.Responsible),
            Address = MapAddress(dto.Address),
            EstimatedStudents = dto.EstimatedStudents,
            Notes = NormalizeOptional(dto.Notes),
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
            await _db.Institutions.InsertOneAsync(
                session,
                institution
            );

            if (dto.CreatePortalAccount)
            {
                var role = await _db.Roles
                    .Find(
                        session,
                        x =>
                            x.Name == "Institution" &&
                            x.IsActive &&
                            !x.IsDeleted
                    )
                    .FirstOrDefaultAsync();

                if (role == null)
                {
                    throw new InvalidOperationException(
                        "El rol Institution no está configurado."
                    );
                }

                var temporaryPassword = dto.AutoGeneratePassword
                    ? _passwords.Generate()
                    : dto.TemporaryPassword!.Trim();

                var user = new User
                {
                    Name = institution.Responsible.Name,
                    LegacyFullName = null,
                    Email = normalizedEmail,
                    PasswordHash =
                        BCrypt.Net.BCrypt.HashPassword(
                            temporaryPassword
                        ),
                    RoleId = role.Id,
                    RoleName = role.Name,
                    UserType = UserType.Institution,
                    ProfileId = institution.Id,
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
                ApiResponse<EntityWithPortalAccountDto<Institution>>.Fail(
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
            ApiResponse<EntityWithPortalAccountDto<Institution>>.Ok(
                new EntityWithPortalAccountDto<Institution>
                {
                    Entity = institution,
                    PortalAccount = credentials
                },
                credentials == null
                    ? "Institución creada correctamente"
                    : "Institución y cuenta de portal creadas correctamente"
            )
        );
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(
        string id,
        InstitutionUpdateDto dto)
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

        var institution = await _db.Institutions
            .Find(x => x.Id == id && !x.IsDeleted)
            .FirstOrDefaultAsync();

        if (institution == null)
            return NotFound(ApiResponse<Institution>.Fail("Institución no encontrada"));

        var normalizedName = dto.Name.Trim();
        var normalizedEmail =
            NormalizeEmail(dto.Responsible.Email);

        var duplicateName = await _db.Institutions
            .Find(x =>
                x.Id != id &&
                x.Name.ToLower() ==
                    normalizedName.ToLower() &&
                !x.IsDeleted)
            .AnyAsync();

        if (duplicateName)
            return BadRequest(ApiResponse<Institution>.Fail("El nombre ya pertenece a otra institución"));

        var duplicateEmail = await _db.Institutions
            .Find(x =>
                x.Id != id &&
                x.Responsible.Email ==
                    normalizedEmail &&
                !x.IsDeleted)
            .AnyAsync();

        if (duplicateEmail)
            return BadRequest(ApiResponse<Institution>.Fail("El correo ya pertenece a otra institución"));

        institution.Name = normalizedName;
        institution.InstitutionType = dto.InstitutionType;
        institution.Responsible = MapResponsible(dto.Responsible);
        institution.Address = MapAddress(dto.Address);
        institution.EstimatedStudents = dto.EstimatedStudents;
        institution.Notes = NormalizeOptional(dto.Notes);
        institution.IsActive = dto.IsActive;
        institution.UpdatedAt = DateTime.UtcNow;
        institution.UpdatedBy = CurrentUserId();

        await _db.Institutions.ReplaceOneAsync(
            x => x.Id == id,
            institution
        );

        return Ok(
            ApiResponse<Institution>.Ok(
                institution,
                "Institución actualizada correctamente"
            )
        );
    }

    [HttpPatch("{id}/status")]
    public async Task<IActionResult> UpdateStatus(
        string id,
        EntityStatusUpdateDto dto)
    {
        var update = Builders<Institution>.Update
            .Set(x => x.IsActive, dto.IsActive)
            .Set(x => x.UpdatedAt, DateTime.UtcNow)
            .Set(x => x.UpdatedBy, CurrentUserId());

        var options = new FindOneAndUpdateOptions<Institution>
        {
            ReturnDocument = ReturnDocument.After
        };

        var institution = await _db.Institutions.FindOneAndUpdateAsync(
            x => x.Id == id && !x.IsDeleted,
            update,
            options
        );

        if (institution == null)
            return NotFound(ApiResponse<Institution>.Fail("Institución no encontrada"));

        return Ok(
            ApiResponse<Institution>.Ok(
                institution,
                dto.IsActive
                    ? "Institución activada correctamente"
                    : "Institución desactivada correctamente"
            )
        );
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var update = Builders<Institution>.Update
            .Set(x => x.IsDeleted, true)
            .Set(x => x.IsActive, false)
            .Set(x => x.UpdatedAt, DateTime.UtcNow)
            .Set(x => x.UpdatedBy, CurrentUserId());

        var result = await _db.Institutions.UpdateOneAsync(
            x => x.Id == id && !x.IsDeleted,
            update
        );

        if (result.ModifiedCount == 0)
            return NotFound(ApiResponse<string>.Fail("Institución no encontrada"));

        return Ok(ApiResponse<string>.Ok("Institución eliminada correctamente"));
    }

    private string? CurrentUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier);
    }

    private static List<string> ValidateInstitution(
        InstitutionCreateDto dto)
    {
        return ValidateInstitutionCore(
            dto.Name,
            dto.Responsible,
            dto.Address,
            dto.EstimatedStudents
        );
    }

    private static List<string> ValidateInstitution(
        InstitutionUpdateDto dto)
    {
        return ValidateInstitutionCore(
            dto.Name,
            dto.Responsible,
            dto.Address,
            dto.EstimatedStudents
        );
    }

    private static List<string> ValidateInstitutionCore(
        string name,
        InstitutionResponsibleDto responsible,
        AddressDto? address,
        int? estimatedStudents)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(name) ||
            name.Trim().Length < 3)
        {
            errors.Add("El nombre de la institución debe tener al menos 3 caracteres");
        }

        if (string.IsNullOrWhiteSpace(responsible.Name.FirstNames))
            errors.Add("Los nombres del responsable son obligatorios");

        if (string.IsNullOrWhiteSpace(responsible.Name.PaternalLastName))
            errors.Add("El apellido paterno del responsable es obligatorio");

        if (string.IsNullOrWhiteSpace(responsible.Email))
            errors.Add("El correo del responsable es obligatorio");
        else if (!IsValidEmail(responsible.Email))
            errors.Add("El correo del responsable no tiene un formato válido");

        if (!string.IsNullOrWhiteSpace(responsible.Phone) &&
            (
                responsible.Phone.Trim().Length != 10 ||
                !responsible.Phone.Trim().All(char.IsDigit)
            ))
        {
            errors.Add("El teléfono del responsable debe contener exactamente 10 dígitos");
        }

        if (estimatedStudents.HasValue &&
            estimatedStudents.Value < 0)
        {
            errors.Add("La cantidad estimada de personas no puede ser negativa");
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

    private static InstitutionResponsible MapResponsible(
        InstitutionResponsibleDto dto)
    {
        return new InstitutionResponsible
        {
            Name = new PersonName
            {
                FirstNames = dto.Name.FirstNames.Trim(),
                PaternalLastName =
                    dto.Name.PaternalLastName.Trim(),
                MaternalLastName =
                    NormalizeOptional(
                        dto.Name.MaternalLastName
                    )
            },
            Email = NormalizeEmail(dto.Email),
            Phone = NormalizeOptional(dto.Phone),
            Position = NormalizeOptional(dto.Position)
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
