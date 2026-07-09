using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using Volts.Api.Responses;
using Volts.Api.Services;

namespace Volts.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin,Employee")]
public class DashboardController : ControllerBase
{
    private readonly MongoDbService _db;

    public DashboardController(MongoDbService db)
    {
        _db = db;
    }

    [HttpGet("summary")]
    public async Task<IActionResult> Summary()
    {
        var totalUsers = await _db.Users.CountDocumentsAsync(x => !x.IsDeleted);
        var totalCustomers = await _db.Customers.CountDocumentsAsync(x => !x.IsDeleted);
        var totalProducts = await _db.Products.CountDocumentsAsync(x => !x.IsDeleted);
        var totalQuotes = await _db.Quotes.CountDocumentsAsync(x => !x.IsDeleted);
        var totalContacts = await _db.ContactMessages.CountDocumentsAsync(x => !x.IsDeleted);

        var data = new
        {
            totalUsers,
            totalCustomers,
            totalProducts,
            totalQuotes,
            totalContacts
        };

        return Ok(ApiResponse<object>.Ok(data, "Resumen del dashboard"));
    }
}