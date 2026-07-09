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
        var exists = await _db.Institutions
            .Find(x => x.Email.ToLower() == dto.Email.ToLower() && !x.IsDeleted)
            .AnyAsync();

        if (exists)
            return BadRequest(ApiResponse<Institution>.Fail("Ya existe una institución con ese correo"));

        var institution = new Institution
        {
            Name = dto.Name,
            ContactName = dto.ContactName,
            Email = dto.Email.ToLower(),
            Phone = dto.Phone,
            Address = dto.Address,
            InstitutionType = dto.InstitutionType,
            IsActive = true
        };

        await _db.Institutions.InsertOneAsync(institution);

        return Ok(ApiResponse<Institution>.Ok(institution, "Institución creada correctamente"));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, InstitutionUpdateDto dto)
    {
        var institution = await _db.Institutions
            .Find(x => x.Id == id && !x.IsDeleted)
            .FirstOrDefaultAsync();

        if (institution == null)
            return NotFound(ApiResponse<Institution>.Fail("Institución no encontrada"));

        institution.Name = dto.Name;
        institution.ContactName = dto.ContactName;
        institution.Email = dto.Email.ToLower();
        institution.Phone = dto.Phone;
        institution.Address = dto.Address;
        institution.InstitutionType = dto.InstitutionType;
        institution.IsActive = dto.IsActive;
        institution.UpdatedAt = DateTime.UtcNow;

        await _db.Institutions.ReplaceOneAsync(x => x.Id == id, institution);

        return Ok(ApiResponse<Institution>.Ok(institution, "Institución actualizada correctamente"));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var update = Builders<Institution>.Update
            .Set(x => x.IsDeleted, true)
            .Set(x => x.IsActive, false)
            .Set(x => x.UpdatedAt, DateTime.UtcNow);

        var result = await _db.Institutions.UpdateOneAsync(x => x.Id == id && !x.IsDeleted, update);

        if (result.ModifiedCount == 0)
            return NotFound(ApiResponse<string>.Fail("Institución no encontrada"));

        return Ok(ApiResponse<string>.Ok("Institución eliminada correctamente"));
    }
}