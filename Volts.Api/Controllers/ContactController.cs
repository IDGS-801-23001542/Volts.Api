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

    public ContactController(MongoDbService db)
    {
        _db = db;
    }

    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> Create(ContactCreateDto dto)
    {
        var message = new ContactMessage
        {
            FullName = dto.FullName,
            Email = dto.Email.ToLower(),
            Phone = dto.Phone,
            Subject = dto.Subject,
            Message = dto.Message,
            Status = "New"
        };

        await _db.ContactMessages.InsertOneAsync(message);

        return Ok(ApiResponse<ContactMessage>.Ok(message, "Mensaje enviado correctamente"));
    }

    [HttpGet]
    [Authorize(Roles = "Admin,Employee")]
    public async Task<IActionResult> GetAll()
    {
        var messages = await _db.ContactMessages
            .Find(x => !x.IsDeleted)
            .SortByDescending(x => x.CreatedAt)
            .ToListAsync();

        return Ok(ApiResponse<List<ContactMessage>>.Ok(messages));
    }
}