using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using Volts.Api.DTOs;
using Volts.Api.Models;
using Volts.Api.Responses;
using Volts.Api.Services;

namespace Volts.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ContactController : ControllerBase
{
    private readonly MongoDbService _db;

    private static readonly string[] AllowedStatuses =
    {
        "New",
        "InProgress",
        "Responded",
        "Closed"
    };

    public ContactController(MongoDbService db)
    {
        _db = db;
    }

    // =========================================================
    // CREAR MENSAJE DESDE EL SITIO PÚBLICO
    // =========================================================
    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> Create(
        ContactCreateDto dto)
    {
        var validationErrors = ValidateCreateDto(dto);

        if (validationErrors.Count > 0)
        {
            return BadRequest(
                ApiResponse<ContactMessage>.Fail(
                    "No fue posible enviar el mensaje.",
                    validationErrors
                )
            );
        }

        var message = new ContactMessage
        {
            FullName = dto.FullName.Trim(),
            Email = dto.Email.Trim().ToLowerInvariant(),
            Phone = NormalizeOptional(dto.Phone),
            Subject = dto.Subject.Trim(),
            Message = dto.Message.Trim(),
            Status = "New",
            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };

        await _db.ContactMessages.InsertOneAsync(message);

        return Ok(
            ApiResponse<ContactMessage>.Ok(
                message,
                "Mensaje enviado correctamente."
            )
        );
    }

    // =========================================================
    // LISTAR MENSAJES PARA EL BACKOFFICE
    // =========================================================
    [HttpGet]
    [Authorize(Roles = "Admin,Employee")]
    public async Task<IActionResult> GetAll()
    {
        var messages = await _db.ContactMessages
            .Find(x => !x.IsDeleted)
            .SortByDescending(x => x.CreatedAt)
            .ToListAsync();

        return Ok(
            ApiResponse<List<ContactMessage>>.Ok(
                messages
            )
        );
    }

    // =========================================================
    // OBTENER UN MENSAJE POR ID
    // =========================================================
    [HttpGet("{id}")]
    [Authorize(Roles = "Admin,Employee")]
    public async Task<IActionResult> GetById(
        string id)
    {
        var message = await _db.ContactMessages
            .Find(x =>
                x.Id == id &&
                !x.IsDeleted
            )
            .FirstOrDefaultAsync();

        if (message == null)
        {
            return NotFound(
                ApiResponse<ContactMessage>.Fail(
                    "Mensaje no encontrado."
                )
            );
        }

        return Ok(
            ApiResponse<ContactMessage>.Ok(
                message
            )
        );
    }

    // =========================================================
    // ACTUALIZAR ESTADO
    // =========================================================
    [HttpPut("{id}/status")]
    [Authorize(Roles = "Admin,Employee")]
    public async Task<IActionResult> UpdateStatus(
        string id,
        ContactStatusDto dto)
    {
        var normalizedStatus = dto.Status?.Trim();

        if (
            string.IsNullOrWhiteSpace(normalizedStatus) ||
            !AllowedStatuses.Contains(
                normalizedStatus,
                StringComparer.OrdinalIgnoreCase
            )
        )
        {
            return BadRequest(
                ApiResponse<string>.Fail(
                    "Estado inválido. Los estados permitidos son: " +
                    "New, InProgress, Responded y Closed."
                )
            );
        }

        var resolvedStatus = AllowedStatuses.First(
            status => status.Equals(
                normalizedStatus,
                StringComparison.OrdinalIgnoreCase
            )
        );

        var message = await _db.ContactMessages
            .Find(x =>
                x.Id == id &&
                !x.IsDeleted
            )
            .FirstOrDefaultAsync();

        if (message == null)
        {
            return NotFound(
                ApiResponse<string>.Fail(
                    "Mensaje no encontrado."
                )
            );
        }

        message.Status = resolvedStatus;
        message.UpdatedAt = DateTime.UtcNow;

        await _db.ContactMessages.ReplaceOneAsync(
            x => x.Id == id,
            message
        );

        return Ok(
            ApiResponse<ContactMessage>.Ok(
                message,
                "Estado del mensaje actualizado correctamente."
            )
        );
    }

    // =========================================================
    // ELIMINACIÓN LÓGICA
    // =========================================================
    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(
        string id)
    {
        var update = Builders<ContactMessage>.Update
            .Set(x => x.IsDeleted, true)
            .Set(x => x.UpdatedAt, DateTime.UtcNow);

        var result =
            await _db.ContactMessages.UpdateOneAsync(
                x =>
                    x.Id == id &&
                    !x.IsDeleted,
                update
            );

        if (result.MatchedCount == 0)
        {
            return NotFound(
                ApiResponse<string>.Fail(
                    "Mensaje no encontrado."
                )
            );
        }

        return Ok(
            ApiResponse<string>.Ok(
                id,
                "Mensaje eliminado correctamente."
            )
        );
    }

    // =========================================================
    // VALIDACIONES
    // =========================================================
    private static List<string> ValidateCreateDto(
        ContactCreateDto dto)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(dto.FullName))
        {
            errors.Add(
                "El nombre es obligatorio."
            );
        }
        else if (dto.FullName.Trim().Length < 3)
        {
            errors.Add(
                "El nombre debe contener al menos 3 caracteres."
            );
        }

        if (string.IsNullOrWhiteSpace(dto.Email))
        {
            errors.Add(
                "El correo electrónico es obligatorio."
            );
        }
        else if (
            !dto.Email.Contains('@') ||
            !dto.Email.Contains('.')
        )
        {
            errors.Add(
                "El correo electrónico no tiene un formato válido."
            );
        }

        if (string.IsNullOrWhiteSpace(dto.Subject))
        {
            errors.Add(
                "El asunto es obligatorio."
            );
        }
        else if (dto.Subject.Trim().Length < 4)
        {
            errors.Add(
                "El asunto debe contener al menos 4 caracteres."
            );
        }

        if (string.IsNullOrWhiteSpace(dto.Message))
        {
            errors.Add(
                "El mensaje es obligatorio."
            );
        }
        else if (dto.Message.Trim().Length < 10)
        {
            errors.Add(
                "El mensaje debe contener al menos 10 caracteres."
            );
        }

        return errors;
    }

    private static string? NormalizeOptional(
        string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}