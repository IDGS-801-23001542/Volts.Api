using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using Volts.Api.Responses;
using Volts.Api.Services;

namespace Volts.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private readonly MongoDbService _mongoDbService;

    public HealthController(MongoDbService mongoDbService)
    {
        _mongoDbService = mongoDbService;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<string>>> Get()
    {
        await _mongoDbService.Users.CountDocumentsAsync(_ => true);

        return Ok(ApiResponse<string>.Ok("API conectada correctamente con MongoDB Atlas"));
    }
}