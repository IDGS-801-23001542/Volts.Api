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
public class DocumentationController :
    ControllerBase
{
    private readonly MongoDbService _db;

    private static readonly string[]
        AllowedDocumentTypes =
        {
            "Manual",
            "QuickGuide",
            "Firmware",
            "Video",
            "AndroidApp",
            "EducationalResource",
            "Warranty",
            "Other"
        };

    public DocumentationController(
        MongoDbService db)
    {
        _db = db;
    }

    // =========================================================
    // DOCUMENTACIÓN PÚBLICA
    // =========================================================
    [HttpGet("public")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPublic()
    {
        var documents =
            await _db.Documentation
                .Find(x =>
                    !x.IsDeleted &&
                    x.IsActive &&
                    x.IsPublic
                )
                .SortByDescending(
                    x => x.CreatedAt
                )
                .ToListAsync();

        return Ok(
            ApiResponse<List<Documentation>>
                .Ok(documents)
        );
    }

    // =========================================================
    // LISTADO COMPLETO DEL BACKOFFICE
    // =========================================================
    [HttpGet]
    [Authorize(Roles = "Admin,Employee")]
    public async Task<IActionResult> GetAll()
    {
        var documents =
            await _db.Documentation
                .Find(x => !x.IsDeleted)
                .SortByDescending(
                    x => x.CreatedAt
                )
                .ToListAsync();

        return Ok(
            ApiResponse<List<Documentation>>
                .Ok(documents)
        );
    }

    // =========================================================
    // OBTENER POR ID
    // =========================================================
    [HttpGet("{id}")]
    [Authorize(Roles = "Admin,Employee")]
    public async Task<IActionResult> GetById(
        string id)
    {
        var document =
            await _db.Documentation
                .Find(x =>
                    x.Id == id &&
                    !x.IsDeleted
                )
                .FirstOrDefaultAsync();

        if (document == null)
        {
            return NotFound(
                ApiResponse<Documentation>.Fail(
                    "Documento no encontrado."
                )
            );
        }

        return Ok(
            ApiResponse<Documentation>.Ok(
                document
            )
        );
    }

    // =========================================================
    // CREAR DOCUMENTO
    // =========================================================
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create(
        DocumentationCreateDto dto)
    {
        var errors =
            ValidateDocumentation(dto);

        if (errors.Count > 0)
        {
            return BadRequest(
                ApiResponse<Documentation>.Fail(
                    "No fue posible crear el documento.",
                    errors
                )
            );
        }

        var documentType =
            ResolveDocumentType(
                dto.DocumentType
            );

        var document =
            new Documentation
            {
                Title =
                    dto.Title.Trim(),

                DocumentType =
                    documentType,

                Description =
                    dto.Description.Trim(),

                FileUrl =
                    dto.FileUrl.Trim(),

                Version =
                    dto.Version.Trim(),

                IsPublic =
                    dto.IsPublic,

                IsActive =
                    true,

                CreatedAt =
                    DateTime.UtcNow,

                IsDeleted =
                    false
            };

        await _db.Documentation
            .InsertOneAsync(document);

        return Ok(
            ApiResponse<Documentation>.Ok(
                document,
                "Documento creado correctamente."
            )
        );
    }

    // =========================================================
    // ACTUALIZAR DOCUMENTO
    // =========================================================
    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(
        string id,
        DocumentationUpdateDto dto)
    {
        var errors =
            ValidateDocumentation(dto);

        if (errors.Count > 0)
        {
            return BadRequest(
                ApiResponse<Documentation>.Fail(
                    "No fue posible actualizar el documento.",
                    errors
                )
            );
        }

        var document =
            await _db.Documentation
                .Find(x =>
                    x.Id == id &&
                    !x.IsDeleted
                )
                .FirstOrDefaultAsync();

        if (document == null)
        {
            return NotFound(
                ApiResponse<Documentation>.Fail(
                    "Documento no encontrado."
                )
            );
        }

        document.Title =
            dto.Title.Trim();

        document.DocumentType =
            ResolveDocumentType(
                dto.DocumentType
            );

        document.Description =
            dto.Description.Trim();

        document.FileUrl =
            dto.FileUrl.Trim();

        document.Version =
            dto.Version.Trim();

        document.IsPublic =
            dto.IsPublic;

        document.IsActive =
            dto.IsActive;

        document.UpdatedAt =
            DateTime.UtcNow;

        await _db.Documentation
            .ReplaceOneAsync(
                x => x.Id == id,
                document
            );

        return Ok(
            ApiResponse<Documentation>.Ok(
                document,
                "Documento actualizado correctamente."
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
            Builders<Documentation>.Update
                .Set(
                    x => x.IsDeleted,
                    true
                )
                .Set(
                    x => x.IsActive,
                    false
                )
                .Set(
                    x => x.UpdatedAt,
                    DateTime.UtcNow
                );

        var result =
            await _db.Documentation
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
                    "Documento no encontrado."
                )
            );
        }

        return Ok(
            ApiResponse<string>.Ok(
                id,
                "Documento eliminado correctamente."
            )
        );
    }

    // =========================================================
    // VALIDACIONES
    // =========================================================
    private static List<string>
        ValidateDocumentation(
            DocumentationCreateDto dto)
    {
        var errors =
            new List<string>();

        if (
            string.IsNullOrWhiteSpace(
                dto.Title
            )
        )
        {
            errors.Add(
                "El título es obligatorio."
            );
        }
        else if (
            dto.Title.Trim().Length < 3
        )
        {
            errors.Add(
                "El título debe contener al menos 3 caracteres."
            );
        }
        else if (
            dto.Title.Trim().Length > 180
        )
        {
            errors.Add(
                "El título no puede exceder 180 caracteres."
            );
        }

        if (
            string.IsNullOrWhiteSpace(
                dto.DocumentType
            ) ||
            !AllowedDocumentTypes.Contains(
                dto.DocumentType.Trim(),
                StringComparer.OrdinalIgnoreCase
            )
        )
        {
            errors.Add(
                "El tipo de documento no es válido."
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
            dto.Description.Trim().Length > 1500
        )
        {
            errors.Add(
                "La descripción no puede exceder 1500 caracteres."
            );
        }

        if (
            string.IsNullOrWhiteSpace(
                dto.FileUrl
            )
        )
        {
            errors.Add(
                "La URL del recurso es obligatoria."
            );
        }
        else if (
            !Uri.TryCreate(
                dto.FileUrl.Trim(),
                UriKind.Absolute,
                out var uri
            ) ||
            (
                uri.Scheme != Uri.UriSchemeHttp &&
                uri.Scheme != Uri.UriSchemeHttps
            )
        )
        {
            errors.Add(
                "La URL debe comenzar con http:// o https://."
            );
        }

        if (
            string.IsNullOrWhiteSpace(
                dto.Version
            )
        )
        {
            errors.Add(
                "La versión es obligatoria."
            );
        }
        else if (
            dto.Version.Trim().Length > 30
        )
        {
            errors.Add(
                "La versión no puede exceder 30 caracteres."
            );
        }

        return errors;
    }

    private static string ResolveDocumentType(
        string value)
    {
        return AllowedDocumentTypes.First(
            type =>
                type.Equals(
                    value.Trim(),
                    StringComparison.OrdinalIgnoreCase
                )
        );
    }
}