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

    public CommentsController(MongoDbService db)
    {
        _db = db;
    }

    [HttpGet("approved")]
    [AllowAnonymous]
    public async Task<IActionResult> GetApproved()
    {
        var comments = await _db.Comments
            .Find(x => !x.IsDeleted && x.IsApproved)
            .SortByDescending(x => x.CreatedAt)
            .ToListAsync();

        return Ok(ApiResponse<List<Comment>>.Ok(comments));
    }

    [HttpGet]
    [Authorize(Roles = "Admin,Employee")]
    public async Task<IActionResult> GetAll()
    {
        var comments = await _db.Comments
            .Find(x => !x.IsDeleted)
            .SortByDescending(x => x.CreatedAt)
            .ToListAsync();

        return Ok(ApiResponse<List<Comment>>.Ok(comments));
    }

    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> Create(CommentCreateDto dto)
    {
        if (dto.Rating < 1 || dto.Rating > 5)
            return BadRequest(ApiResponse<Comment>.Fail("La calificación debe estar entre 1 y 5"));

        var comment = new Comment
        {
            FullName = dto.FullName,
            Email = dto.Email.ToLower(),
            Message = dto.Message,
            Rating = dto.Rating,
            IsApproved = false
        };

        await _db.Comments.InsertOneAsync(comment);

        return Ok(ApiResponse<Comment>.Ok(comment, "Comentario enviado correctamente"));
    }

    [HttpPut("{id}/approval")]
    [Authorize(Roles = "Admin,Employee")]
    public async Task<IActionResult> Approval(string id, CommentApprovalDto dto)
    {
        var update = Builders<Comment>.Update
            .Set(x => x.IsApproved, dto.IsApproved)
            .Set(x => x.UpdatedAt, DateTime.UtcNow);

        var result = await _db.Comments.UpdateOneAsync(x => x.Id == id && !x.IsDeleted, update);

        if (result.ModifiedCount == 0)
            return NotFound(ApiResponse<string>.Fail("Comentario no encontrado"));

        return Ok(ApiResponse<string>.Ok("Estado del comentario actualizado correctamente"));
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(string id)
    {
        var update = Builders<Comment>.Update
            .Set(x => x.IsDeleted, true)
            .Set(x => x.UpdatedAt, DateTime.UtcNow);

        var result = await _db.Comments.UpdateOneAsync(x => x.Id == id && !x.IsDeleted, update);

        if (result.ModifiedCount == 0)
            return NotFound(ApiResponse<string>.Fail("Comentario no encontrado"));

        return Ok(ApiResponse<string>.Ok("Comentario eliminado correctamente"));
    }
}