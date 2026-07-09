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
        var totalInstitutions = await _db.Institutions.CountDocumentsAsync(x => !x.IsDeleted);
        var totalLicenses = await _db.Licenses.CountDocumentsAsync(x => !x.IsDeleted);
        var totalProducts = await _db.Products.CountDocumentsAsync(x => !x.IsDeleted);
        var totalQuotes = await _db.Quotes.CountDocumentsAsync(x => !x.IsDeleted);
        var totalOrders = await _db.Orders.CountDocumentsAsync(x => !x.IsDeleted);
        var totalSales = await _db.Sales.CountDocumentsAsync(x => !x.IsDeleted);
        var totalRawMaterials = await _db.RawMaterials.CountDocumentsAsync(x => !x.IsDeleted);
        var lowStock = await _db.RawMaterials.CountDocumentsAsync(x => !x.IsDeleted && x.CurrentStock <= x.MinimumStock);
        var totalSupportTickets = await _db.SupportTickets.CountDocumentsAsync(x => !x.IsDeleted);
        var openSupportTickets = await _db.SupportTickets.CountDocumentsAsync(x => !x.IsDeleted && x.Status == "Open");

        var sales = await _db.Sales.Find(x => !x.IsDeleted).ToListAsync();
        var totalRevenue = sales.Sum(x => x.Total);

        var data = new
        {
            totalUsers,
            totalCustomers,
            totalInstitutions,
            totalLicenses,
            totalProducts,
            totalQuotes,
            totalOrders,
            totalSales,
            totalRevenue,
            totalRawMaterials,
            lowStock,
            totalSupportTickets,
            openSupportTickets
        };

        return Ok(ApiResponse<object>.Ok(data, "Resumen ejecutivo del sistema"));
    }
}