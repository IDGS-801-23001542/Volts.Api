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
public class DocumentationController : ControllerBase
{
    private readonly MongoDbService _db;

    public DocumentationController(MongoDbService db)
    {
        _db = db;
    }

    [HttpGet("public")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPublic()
    {
        var docs = await _db.Documentation
            .Find(x => !x.IsDeleted && x.IsActive && x.IsPublic)
            .SortByDescending(x => x.CreatedAt)
            .ToListAsync();

        return Ok(ApiResponse<List<Documentation>>.Ok(docs));
    }

    [HttpGet]
    [Authorize(Roles = "Admin,Employee")]
    public async Task<IActionResult> GetAll()
    {
        var docs = await _db.Documentation
            .Find(x => !x.IsDeleted)
            .SortByDescending(x => x.CreatedAt)
            .ToListAsync();

        return Ok(ApiResponse<List<Documentation>>.Ok(docs));
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create(DocumentationCreateDto dto)
    {
        var doc = new Documentation
        {
            Title = dto.Title,
            DocumentType = dto.DocumentType,
            Description = dto.Description,
            FileUrl = dto.FileUrl,
            Version = dto.Version,
            IsPublic = dto.IsPublic,
            IsActive = true
        };

        await _db.Documentation.InsertOneAsync(doc);

        return Ok(ApiResponse<Documentation>.Ok(doc, "Documento creado correctamente"));
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(string id, DocumentationUpdateDto dto)
    {
        var doc = await _db.Documentation.Find(x => x.Id == id && !x.IsDeleted).FirstOrDefaultAsync();

        if (doc == null)
            return NotFound(ApiResponse<Documentation>.Fail("Documento no encontrado"));

        doc.Title = dto.Title;
        doc.DocumentType = dto.DocumentType;
        doc.Description = dto.Description;
        doc.FileUrl = dto.FileUrl;
        doc.Version = dto.Version;
        doc.IsPublic = dto.IsPublic;
        doc.IsActive = dto.IsActive;
        doc.UpdatedAt = DateTime.UtcNow;

        await _db.Documentation.ReplaceOneAsync(x => x.Id == id, doc);

        return Ok(ApiResponse<Documentation>.Ok(doc, "Documento actualizado correctamente"));
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(string id)
    {
        var update = Builders<Documentation>.Update
            .Set(x => x.IsDeleted, true)
            .Set(x => x.IsActive, false)
            .Set(x => x.UpdatedAt, DateTime.UtcNow);

        var result = await _db.Documentation.UpdateOneAsync(x => x.Id == id && !x.IsDeleted, update);

        if (result.ModifiedCount == 0)
            return NotFound(ApiResponse<string>.Fail("Documento no encontrado"));

        return Ok(ApiResponse<string>.Ok("Documento eliminado correctamente"));
    }
}