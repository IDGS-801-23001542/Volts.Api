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
public class UpdateNewsController :
    ControllerBase
{
    private readonly MongoDbService _db;

    private static readonly string[]
        AllowedPlatforms =
        {
            "Android",
            "Firmware",
            "Web",
            "General",
            "iOS"
        };

    public UpdateNewsController(
        MongoDbService db)
    {
        _db = db;
    }

    // =========================================================
    // ACTUALIZACIONES PUBLICADAS
    // Sitio público, portal cliente e institución
    // =========================================================
    [HttpGet("published")]
    [AllowAnonymous]
    public async Task<IActionResult>
        GetPublished()
    {
        var updates =
            await _db.UpdateNews
                .Find(x =>
                    !x.IsDeleted &&
                    x.IsPublished
                )
                .SortByDescending(
                    x => x.PublishDate
                )
                .ToListAsync();

        return Ok(
            ApiResponse<List<UpdateNews>>.Ok(
                updates
            )
        );
    }

    // =========================================================
    // LISTADO COMPLETO PARA BACKOFFICE
    // =========================================================
    [HttpGet]
    [Authorize(Roles = "Admin,Employee")]
    public async Task<IActionResult> GetAll()
    {
        var updates =
            await _db.UpdateNews
                .Find(x => !x.IsDeleted)
                .SortByDescending(
                    x => x.PublishDate
                )
                .ToListAsync();

        return Ok(
            ApiResponse<List<UpdateNews>>.Ok(
                updates
            )
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
        var update =
            await _db.UpdateNews
                .Find(x =>
                    x.Id == id &&
                    !x.IsDeleted
                )
                .FirstOrDefaultAsync();

        if (update == null)
        {
            return NotFound(
                ApiResponse<UpdateNews>.Fail(
                    "Actualización no encontrada."
                )
            );
        }

        return Ok(
            ApiResponse<UpdateNews>.Ok(
                update
            )
        );
    }

    // =========================================================
    // CREAR ACTUALIZACIÓN O BORRADOR
    // =========================================================
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create(
        UpdateNewsCreateDto dto)
    {
        var errors =
            ValidateUpdate(dto);

        if (errors.Count > 0)
        {
            return BadRequest(
                ApiResponse<UpdateNews>.Fail(
                    "No fue posible guardar la actualización.",
                    errors
                )
            );
        }

        var platform =
            ResolvePlatform(
                dto.Platform
            );

        var now =
            DateTime.UtcNow;

        var update =
            new UpdateNews
            {
                Title =
                    dto.Title.Trim(),

                Content =
                    dto.Content.Trim(),

                Version =
                    dto.Version.Trim(),

                Platform =
                    platform,

                PublishDate =
                    now,

                IsPublished =
                    dto.IsPublished,

                CreatedAt =
                    now,

                IsDeleted =
                    false
            };

        await _db.UpdateNews
            .InsertOneAsync(update);

        var message =
            dto.IsPublished
                ? "Actualización publicada correctamente."
                : "Borrador guardado correctamente.";

        return Ok(
            ApiResponse<UpdateNews>.Ok(
                update,
                message
            )
        );
    }

    // =========================================================
    // ACTUALIZAR
    // =========================================================
    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(
        string id,
        UpdateNewsUpdateDto dto)
    {
        var errors =
            ValidateUpdate(dto);

        if (errors.Count > 0)
        {
            return BadRequest(
                ApiResponse<UpdateNews>.Fail(
                    "No fue posible actualizar el registro.",
                    errors
                )
            );
        }

        var update =
            await _db.UpdateNews
                .Find(x =>
                    x.Id == id &&
                    !x.IsDeleted
                )
                .FirstOrDefaultAsync();

        if (update == null)
        {
            return NotFound(
                ApiResponse<UpdateNews>.Fail(
                    "Actualización no encontrada."
                )
            );
        }

        var wasPublished =
            update.IsPublished;

        update.Title =
            dto.Title.Trim();

        update.Content =
            dto.Content.Trim();

        update.Version =
            dto.Version.Trim();

        update.Platform =
            ResolvePlatform(
                dto.Platform
            );

        update.IsPublished =
            dto.IsPublished;

        /*
         * Si antes era borrador y ahora se publica,
         * renovamos la fecha de publicación.
         */
        if (
            !wasPublished &&
            dto.IsPublished
        )
        {
            update.PublishDate =
                DateTime.UtcNow;
        }

        update.UpdatedAt =
            DateTime.UtcNow;

        await _db.UpdateNews
            .ReplaceOneAsync(
                x => x.Id == id,
                update
            );

        var message =
            dto.IsPublished
                ? "Actualización guardada y publicada correctamente."
                : "Actualización guardada como borrador.";

        return Ok(
            ApiResponse<UpdateNews>.Ok(
                update,
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
            Builders<UpdateNews>.Update
                .Set(
                    x => x.IsDeleted,
                    true
                )
                .Set(
                    x => x.IsPublished,
                    false
                )
                .Set(
                    x => x.UpdatedAt,
                    DateTime.UtcNow
                );

        var result =
            await _db.UpdateNews
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
                    "Actualización no encontrada."
                )
            );
        }

        return Ok(
            ApiResponse<string>.Ok(
                id,
                "Actualización eliminada correctamente."
            )
        );
    }

    // =========================================================
    // VALIDACIONES
    // =========================================================
    private static List<string>
        ValidateUpdate(
            UpdateNewsCreateDto dto)
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
            dto.Title.Trim().Length < 4
        )
        {
            errors.Add(
                "El título debe contener al menos 4 caracteres."
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
                dto.Content
            )
        )
        {
            errors.Add(
                "El contenido es obligatorio."
            );
        }
        else if (
            dto.Content.Trim().Length < 10
        )
        {
            errors.Add(
                "El contenido debe contener al menos 10 caracteres."
            );
        }
        else if (
            dto.Content.Trim().Length > 5000
        )
        {
            errors.Add(
                "El contenido no puede exceder 5000 caracteres."
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

        if (
            string.IsNullOrWhiteSpace(
                dto.Platform
            ) ||
            !AllowedPlatforms.Contains(
                dto.Platform.Trim(),
                StringComparer.OrdinalIgnoreCase
            )
        )
        {
            errors.Add(
                "La plataforma debe ser Android, Firmware, Web, General o iOS."
            );
        }

        return errors;
    }

    private static string ResolvePlatform(
        string value)
    {
        return AllowedPlatforms.First(
            platform =>
                platform.Equals(
                    value.Trim(),
                    StringComparison.OrdinalIgnoreCase
                )
        );
    }
}