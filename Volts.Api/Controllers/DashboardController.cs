using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using Volts.Api.DTOs;
using Volts.Api.Models.Enums;
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
        var now = DateTime.UtcNow;
        var monthStart = new DateTime(
            now.Year,
            now.Month,
            1,
            0,
            0,
            0,
            DateTimeKind.Utc
        );

        var users = await _db.Users
            .Find(item => !item.IsDeleted)
            .ToListAsync();

        var customers = await _db.Customers
            .Find(item => !item.IsDeleted)
            .ToListAsync();

        var institutions = await _db.Institutions
            .Find(item => !item.IsDeleted)
            .ToListAsync();

        var quotes = await _db.Quotes
            .Find(item => !item.IsDeleted)
            .ToListAsync();

        var orders = await _db.Orders
            .Find(item => !item.IsDeleted)
            .ToListAsync();

        var sales = await _db.Sales
            .Find(item => !item.IsDeleted)
            .ToListAsync();

        var licenses = await _db.Licenses
            .Find(item => !item.IsDeleted)
            .ToListAsync();

        var products = await _db.Products
            .Find(item => !item.IsDeleted)
            .ToListAsync();

        var rawMaterials = await _db.RawMaterials
            .Find(item => !item.IsDeleted)
            .ToListAsync();

        var productionOrders = await _db.ProductionOrders
            .Find(item => !item.IsDeleted)
            .ToListAsync();

        var tickets = await _db.SupportTickets
            .Find(item => !item.IsDeleted)
            .ToListAsync();

        var comments = await _db.Comments
            .Find(item => !item.IsDeleted)
            .ToListAsync();

        var monthlyGroups = BuildMonthWindows(8)
            .Select(window =>
            {
                var monthSales = sales
                    .Where(item =>
                        item.SaleDate >= window.Start &&
                        item.SaleDate < window.End)
                    .ToList();

                return new
                {
                    window.Label,
                    Revenue = monthSales.Sum(item => item.Total),
                    Count = monthSales.Count
                };
            })
            .ToList();

        var topProducts = sales
            .SelectMany(item => item.Details)
            .GroupBy(item => new
            {
                item.ProductId,
                item.ProductName
            })
            .Select(group => new AnalyticsCategoryDto
            {
                Key = group.Key.ProductId,
                Label = group.Key.ProductName,
                Count = group.Sum(item => item.Quantity),
                Value = group.Sum(item => item.Subtotal)
            })
            .OrderByDescending(item => item.Value)
            .Take(5)
            .ToList();

        var data = new DashboardSummaryDto
        {
            TotalUsers = users.Count,
            InternalUsers = users.Count(item =>
                item.RoleName is "Admin" or "Employee"),
            PortalAccounts = users.Count(item =>
                item.RoleName is "Client" or "Institution"),
            TotalCustomers = customers.Count,
            TotalInstitutions = institutions.Count,

            TotalQuotes = quotes.Count,
            PendingQuotes = quotes.Count(item => item.Status == "Pending"),
            ApprovedQuotes = quotes.Count(item => item.Status == "Approved"),
            ConvertedQuotes = quotes.Count(item => item.Status == "Converted"),

            TotalOrders = orders.Count,
            PendingOrders = orders.Count(item =>
                item.Status == "PendingConfirmation"),
            AwaitingProductionOrders = orders.Count(item =>
                item.Status == "AwaitingProduction"),
            ReadyForSaleOrders = orders.Count(item =>
                item.Status == "ReadyForSale"),

            TotalSales = sales.Count,
            TotalRevenue = sales.Sum(item => item.Total),
            CurrentMonthRevenue = sales
                .Where(item => item.SaleDate >= monthStart)
                .Sum(item => item.Total),

            TotalLicenses = licenses.Count,
            AvailableLicenses = licenses.Count(item =>
                item.Status == "Available"),
            ActiveLicenses = licenses.Count(item =>
                item.Status == "Active"),
            ExpiredLicenses = licenses.Count(item =>
                item.Status == "Expired"),
            RevokedLicenses = licenses.Count(item =>
                item.Status == "Revoked"),

            TotalProducts = products.Count,
            LowFinishedStockProducts = products.Count(item =>
                item.IsActive &&
                item.AvailableStock <= item.MinimumFinishedStock),

            TotalRawMaterials = rawMaterials.Count,
            LowRawMaterialStock = rawMaterials.Count(item =>
                item.IsActive &&
                item.CurrentStock <= item.MinimumStock),

            TotalProductionOrders = productionOrders.Count,
            ActiveProductionOrders = productionOrders.Count(item =>
                item.Status is ProductionStatus.Created or
                ProductionStatus.InProgress),

            TotalSupportTickets = tickets.Count,
            OpenSupportTickets = tickets.Count(item =>
                item.Status == "Open"),

            PendingComments = comments.Count(item =>
                !item.IsApproved),
            ApprovedComments = comments.Count(item =>
                item.IsApproved),

            MonthlyRevenue = monthlyGroups
                .Select(item => new AnalyticsPointDto
                {
                    Label = item.Label,
                    Value = item.Revenue,
                    Count = item.Count
                })
                .ToList(),

            MonthlySales = monthlyGroups
                .Select(item => new AnalyticsPointDto
                {
                    Label = item.Label,
                    Value = item.Count,
                    Count = item.Count
                })
                .ToList(),

            TopProducts = topProducts
        };

        return Ok(
            ApiResponse<DashboardSummaryDto>.Ok(
                data,
                "Resumen ejecutivo obtenido correctamente"
            )
        );
    }

    private static List<MonthWindow> BuildMonthWindows(int count)
    {
        var current = new DateTime(
            DateTime.UtcNow.Year,
            DateTime.UtcNow.Month,
            1,
            0,
            0,
            0,
            DateTimeKind.Utc
        );

        return Enumerable
            .Range(0, count)
            .Select(offset =>
            {
                var start = current.AddMonths(offset - count + 1);

                return new MonthWindow(
                    start,
                    start.AddMonths(1),
                    start.ToString("MMM yy")
                );
            })
            .ToList();
    }

    private sealed record MonthWindow(
        DateTime Start,
        DateTime End,
        string Label
    );
}
