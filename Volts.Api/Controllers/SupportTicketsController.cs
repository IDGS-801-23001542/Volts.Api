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
public class SupportTicketsController : ControllerBase
{
    private readonly MongoDbService _db;

    private static readonly string[] AllowedStatuses =
    {
        "Open",
        "InProgress",
        "Resolved",
        "Closed"
    };

    private static readonly string[] AllowedPriorities =
    {
        "Low",
        "Medium",
        "High",
        "Urgent"
    };

    public SupportTicketsController(
        MongoDbService db)
    {
        _db = db;
    }

    // =========================================================
    // LISTAR TICKETS PARA EL BACKOFFICE
    // =========================================================
    [HttpGet]
    [Authorize(Roles = "Admin,Employee")]
    public async Task<IActionResult> GetAll()
    {
        var tickets = await _db.SupportTickets
            .Find(x => !x.IsDeleted)
            .SortByDescending(x => x.CreatedAt)
            .ToListAsync();

        return Ok(
            ApiResponse<List<SupportTicket>>.Ok(
                tickets
            )
        );
    }

    // =========================================================
    // OBTENER TICKET POR ID
    // =========================================================
    [HttpGet("{id}")]
    [Authorize(Roles = "Admin,Employee")]
    public async Task<IActionResult> GetById(
        string id)
    {
        var ticket = await _db.SupportTickets
            .Find(x =>
                x.Id == id &&
                !x.IsDeleted
            )
            .FirstOrDefaultAsync();

        if (ticket == null)
        {
            return NotFound(
                ApiResponse<SupportTicket>.Fail(
                    "Ticket no encontrado."
                )
            );
        }

        return Ok(
            ApiResponse<SupportTicket>.Ok(
                ticket
            )
        );
    }

    // =========================================================
    // CREAR TICKET
    // =========================================================
    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> Create(
        SupportTicketCreateDto dto)
    {
        var validationErrors =
            ValidateCreateDto(dto);

        if (validationErrors.Count > 0)
        {
            return BadRequest(
                ApiResponse<SupportTicket>.Fail(
                    "No fue posible crear el ticket.",
                    validationErrors
                )
            );
        }

        var resolvedPriority =
            ResolveAllowedValue(
                dto.Priority,
                AllowedPriorities
            );

        Customer? customer = null;

        if (
            !string.IsNullOrWhiteSpace(
                dto.CustomerId
            )
        )
        {
            customer = await _db.Customers
                .Find(x =>
                    x.Id == dto.CustomerId.Trim() &&
                    !x.IsDeleted
                )
                .FirstOrDefaultAsync();
        }

        var ticket = new SupportTicket
        {
            CustomerId =
                customer?.Id ??
                dto.CustomerId?.Trim() ??
                string.Empty,

            CustomerName =
                customer?.FullName ??
                "Cliente no registrado",

            Email =
                dto.Email
                    .Trim()
                    .ToLowerInvariant(),

            Subject =
                dto.Subject.Trim(),

            Description =
                dto.Description.Trim(),

            Priority =
                resolvedPriority,

            Status =
                "Open",

            CreatedAt =
                DateTime.UtcNow,

            IsDeleted =
                false
        };

        await _db.SupportTickets
            .InsertOneAsync(ticket);

        return Ok(
            ApiResponse<SupportTicket>.Ok(
                ticket,
                "Ticket creado correctamente."
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
        SupportTicketStatusDto dto)
    {
        if (
            string.IsNullOrWhiteSpace(
                dto.Status
            ) ||
            !AllowedStatuses.Contains(
                dto.Status.Trim(),
                StringComparer.OrdinalIgnoreCase
            )
        )
        {
            return BadRequest(
                ApiResponse<SupportTicket>.Fail(
                    "Estado inválido. Los estados permitidos son: " +
                    "Open, InProgress, Resolved y Closed."
                )
            );
        }

        var ticket = await _db.SupportTickets
            .Find(x =>
                x.Id == id &&
                !x.IsDeleted
            )
            .FirstOrDefaultAsync();

        if (ticket == null)
        {
            return NotFound(
                ApiResponse<SupportTicket>.Fail(
                    "Ticket no encontrado."
                )
            );
        }

        var resolvedStatus =
            ResolveAllowedValue(
                dto.Status,
                AllowedStatuses
            );

        ticket.Status =
            resolvedStatus;

        ticket.UpdatedAt =
            DateTime.UtcNow;

        await _db.SupportTickets
            .ReplaceOneAsync(
                x => x.Id == id,
                ticket
            );

        return Ok(
            ApiResponse<SupportTicket>.Ok(
                ticket,
                "Estado del ticket actualizado correctamente."
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
        var update =
            Builders<SupportTicket>.Update
                .Set(
                    x => x.IsDeleted,
                    true
                )
                .Set(
                    x => x.UpdatedAt,
                    DateTime.UtcNow
                );

        var result =
            await _db.SupportTickets
                .UpdateOneAsync(
                    x =>
                        x.Id == id &&
                        !x.IsDeleted,
                    update
                );

        if (result.MatchedCount == 0)
        {
            return NotFound(
                ApiResponse<string>.Fail(
                    "Ticket no encontrado."
                )
            );
        }

        return Ok(
            ApiResponse<string>.Ok(
                id,
                "Ticket eliminado correctamente."
            )
        );
    }

    // =========================================================
    // VALIDACIONES
    // =========================================================
    private static List<string> ValidateCreateDto(
        SupportTicketCreateDto dto)
    {
        var errors =
            new List<string>();

        if (
            string.IsNullOrWhiteSpace(
                dto.Email
            )
        )
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

        if (
            string.IsNullOrWhiteSpace(
                dto.Subject
            )
        )
        {
            errors.Add(
                "El asunto es obligatorio."
            );
        }
        else if (
            dto.Subject.Trim().Length < 5
        )
        {
            errors.Add(
                "El asunto debe contener al menos 5 caracteres."
            );
        }
        else if (
            dto.Subject.Trim().Length > 180
        )
        {
            errors.Add(
                "El asunto no puede exceder 180 caracteres."
            );
        }

        if (
            string.IsNullOrWhiteSpace(
                dto.Description
            )
        )
        {
            errors.Add(
                "La descripción es obligatoria."
            );
        }
        else if (
            dto.Description.Trim().Length < 10
        )
        {
            errors.Add(
                "La descripción debe contener al menos 10 caracteres."
            );
        }
        else if (
            dto.Description.Trim().Length > 3000
        )
        {
            errors.Add(
                "La descripción no puede exceder 3000 caracteres."
            );
        }

        if (
            string.IsNullOrWhiteSpace(
                dto.Priority
            ) ||
            !AllowedPriorities.Contains(
                dto.Priority.Trim(),
                StringComparer.OrdinalIgnoreCase
            )
        )
        {
            errors.Add(
                "La prioridad debe ser Low, Medium, High o Urgent."
            );
        }

        return errors;
    }

    private static string ResolveAllowedValue(
        string value,
        IEnumerable<string> allowedValues)
    {
        return allowedValues.First(
            allowed =>
                allowed.Equals(
                    value.Trim(),
                    StringComparison.OrdinalIgnoreCase
                )
        );
    }
}