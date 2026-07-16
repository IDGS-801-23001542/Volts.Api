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
public class CommentsController : ControllerBase
{
    private readonly MongoDbService _db;

    public CommentsController(
        MongoDbService db)
    {
        _db = db;
    }

    // =========================================================
    // COMENTARIOS APROBADOS PARA EL SITIO PÚBLICO
    // =========================================================
    [HttpGet("approved")]
    [AllowAnonymous]
    public async Task<IActionResult> GetApproved()
    {
        var comments = await _db.Comments
            .Find(x =>
                !x.IsDeleted &&
                x.IsApproved
            )
            .SortByDescending(x => x.CreatedAt)
            .ToListAsync();

        return Ok(
            ApiResponse<List<Comment>>.Ok(
                comments
            )
        );
    }

    // =========================================================
    // LISTADO COMPLETO PARA EL BACKOFFICE
    // =========================================================
    [HttpGet]
    [Authorize(Roles = "Admin,Employee")]
    public async Task<IActionResult> GetAll()
    {
        var comments = await _db.Comments
            .Find(x => !x.IsDeleted)
            .SortByDescending(x => x.CreatedAt)
            .ToListAsync();

        return Ok(
            ApiResponse<List<Comment>>.Ok(
                comments
            )
        );
    }

    // =========================================================
    // OBTENER COMENTARIO POR ID
    // =========================================================
    [HttpGet("{id}")]
    [Authorize(Roles = "Admin,Employee")]
    public async Task<IActionResult> GetById(
        string id)
    {
        var comment = await _db.Comments
            .Find(x =>
                x.Id == id &&
                !x.IsDeleted
            )
            .FirstOrDefaultAsync();

        if (comment == null)
        {
            return NotFound(
                ApiResponse<Comment>.Fail(
                    "Comentario no encontrado."
                )
            );
        }

        return Ok(
            ApiResponse<Comment>.Ok(
                comment
            )
        );
    }

    // =========================================================
    // CREAR COMENTARIO
    // =========================================================
    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> Create(
        CommentCreateDto dto)
    {
        var validationErrors =
            ValidateCreateDto(dto);

        if (validationErrors.Count > 0)
        {
            return BadRequest(
                ApiResponse<Comment>.Fail(
                    "No fue posible registrar el comentario.",
                    validationErrors
                )
            );
        }

        var comment = new Comment
        {
            FullName =
                dto.FullName.Trim(),

            Email =
                dto.Email
                    .Trim()
                    .ToLowerInvariant(),

            Message =
                dto.Message.Trim(),

            Rating =
                dto.Rating,

            IsApproved =
                false,

            CreatedAt =
                DateTime.UtcNow,

            IsDeleted =
                false
        };

        await _db.Comments.InsertOneAsync(
            comment
        );

        return Ok(
            ApiResponse<Comment>.Ok(
                comment,
                "Comentario enviado correctamente. " +
                "Será visible después de ser aprobado."
            )
        );
    }

    // =========================================================
    // APROBAR U OCULTAR COMENTARIO
    // =========================================================
    [HttpPut("{id}/approval")]
    [Authorize(Roles = "Admin,Employee")]
    public async Task<IActionResult> Approval(
        string id,
        CommentApprovalDto dto)
    {
        var comment = await _db.Comments
            .Find(x =>
                x.Id == id &&
                !x.IsDeleted
            )
            .FirstOrDefaultAsync();

        if (comment == null)
        {
            return NotFound(
                ApiResponse<Comment>.Fail(
                    "Comentario no encontrado."
                )
            );
        }

        comment.IsApproved =
            dto.IsApproved;

        comment.UpdatedAt =
            DateTime.UtcNow;

        await _db.Comments.ReplaceOneAsync(
            x => x.Id == id,
            comment
        );

        var message = dto.IsApproved
            ? "Comentario aprobado correctamente."
            : "Comentario ocultado correctamente.";

        return Ok(
            ApiResponse<Comment>.Ok(
                comment,
                message
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
            Builders<Comment>.Update
                .Set(
                    x => x.IsDeleted,
                    true
                )
                .Set(
                    x => x.UpdatedAt,
                    DateTime.UtcNow
                );

        var result =
            await _db.Comments.UpdateOneAsync(
                x =>
                    x.Id == id &&
                    !x.IsDeleted,
                update
            );

        if (result.MatchedCount == 0)
        {
            return NotFound(
                ApiResponse<string>.Fail(
                    "Comentario no encontrado."
                )
            );
        }

        return Ok(
            ApiResponse<string>.Ok(
                id,
                "Comentario eliminado correctamente."
            )
        );
    }

    // =========================================================
    // VALIDACIONES
    // =========================================================
    private static List<string> ValidateCreateDto(
        CommentCreateDto dto)
    {
        var errors =
            new List<string>();

        if (
            string.IsNullOrWhiteSpace(
                dto.FullName
            )
        )
        {
            errors.Add(
                "El nombre es obligatorio."
            );
        }
        else if (
            dto.FullName.Trim().Length < 3
        )
        {
            errors.Add(
                "El nombre debe contener al menos 3 caracteres."
            );
        }
        else if (
            dto.FullName.Trim().Length > 120
        )
        {
            errors.Add(
                "El nombre no puede exceder 120 caracteres."
            );
        }

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
                dto.Message
            )
        )
        {
            errors.Add(
                "El comentario es obligatorio."
            );
        }
        else if (
            dto.Message.Trim().Length < 10
        )
        {
            errors.Add(
                "El comentario debe contener al menos 10 caracteres."
            );
        }
        else if (
            dto.Message.Trim().Length > 1500
        )
        {
            errors.Add(
                "El comentario no puede exceder 1500 caracteres."
            );
        }

        if (
            dto.Rating < 1 ||
            dto.Rating > 5
        )
        {
            errors.Add(
                "La calificación debe estar entre 1 y 5."
            );
        }

        return errors;
    }
}