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
public class UpdateNewsController : ControllerBase
{
    private readonly MongoDbService _db;

    public UpdateNewsController(MongoDbService db)
    {
        _db = db;
    }

    [HttpGet("published")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPublished()
    {
        var news = await _db.UpdateNews
            .Find(x => !x.IsDeleted && x.IsPublished)
            .SortByDescending(x => x.PublishDate)
            .ToListAsync();

        return Ok(ApiResponse<List<UpdateNews>>.Ok(news));
    }

    [HttpGet]
    [Authorize(Roles = "Admin,Employee")]
    public async Task<IActionResult> GetAll()
    {
        var news = await _db.UpdateNews
            .Find(x => !x.IsDeleted)
            .SortByDescending(x => x.PublishDate)
            .ToListAsync();

        return Ok(ApiResponse<List<UpdateNews>>.Ok(news));
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create(UpdateNewsCreateDto dto)
    {
        var news = new UpdateNews
        {
            Title = dto.Title,
            Content = dto.Content,
            Version = dto.Version,
            Platform = dto.Platform,
            PublishDate = DateTime.UtcNow,
            IsPublished = dto.IsPublished
        };

        await _db.UpdateNews.InsertOneAsync(news);

        return Ok(ApiResponse<UpdateNews>.Ok(news, "Actualización publicada correctamente"));
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(string id, UpdateNewsUpdateDto dto)
    {
        var news = await _db.UpdateNews.Find(x => x.Id == id && !x.IsDeleted).FirstOrDefaultAsync();

        if (news == null)
            return NotFound(ApiResponse<UpdateNews>.Fail("Actualización no encontrada"));

        news.Title = dto.Title;
        news.Content = dto.Content;
        news.Version = dto.Version;
        news.Platform = dto.Platform;
        news.IsPublished = dto.IsPublished;
        news.UpdatedAt = DateTime.UtcNow;

        await _db.UpdateNews.ReplaceOneAsync(x => x.Id == id, news);

        return Ok(ApiResponse<UpdateNews>.Ok(news, "Actualización editada correctamente"));
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(string id)
    {
        var update = Builders<UpdateNews>.Update
            .Set(x => x.IsDeleted, true)
            .Set(x => x.UpdatedAt, DateTime.UtcNow);

        var result = await _db.UpdateNews.UpdateOneAsync(x => x.Id == id && !x.IsDeleted, update);

        if (result.ModifiedCount == 0)
            return NotFound(ApiResponse<string>.Fail("Actualización no encontrada"));

        return Ok(ApiResponse<string>.Ok("Actualización eliminada correctamente"));
    }
}