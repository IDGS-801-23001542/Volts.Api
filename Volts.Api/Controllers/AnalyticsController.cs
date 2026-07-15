using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using Volts.Api.DTOs;
using Volts.Api.Responses;
using Volts.Api.Services;

namespace Volts.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class AnalyticsController : ControllerBase
{
    private readonly MongoDbService _db;

    public AnalyticsController(MongoDbService db)
    {
        _db = db;
    }

    [HttpGet("overview")]
    public async Task<IActionResult> GetOverview()
    {
        var quotes = await _db.Quotes
            .Find(item => !item.IsDeleted)
            .ToListAsync();

        var orders = await _db.Orders
            .Find(item => !item.IsDeleted)
            .ToListAsync();

        var sales = await _db.Sales
            .Find(item => !item.IsDeleted)
            .ToListAsync();

        var purchases = await _db.Purchases
            .Find(item => !item.IsDeleted)
            .ToListAsync();

        var licenses = await _db.Licenses
            .Find(item => !item.IsDeleted)
            .ToListAsync();

        var productionOrders = await _db.ProductionOrders
            .Find(item => !item.IsDeleted)
            .ToListAsync();

        var wastes = await _db.Wastes
            .Find(item => !item.IsDeleted)
            .ToListAsync();

        var windows = BuildMonthWindows(8);

        var monthlyRevenue = windows
            .Select(window =>
            {
                var records = sales
                    .Where(item =>
                        item.SaleDate >= window.Start &&
                        item.SaleDate < window.End)
                    .ToList();

                return new AnalyticsPointDto
                {
                    Label = window.Label,
                    Value = records.Sum(item => item.Total),
                    Count = records.Count
                };
            })
            .ToList();

        var monthlySales = monthlyRevenue
            .Select(item => new AnalyticsPointDto
            {
                Label = item.Label,
                Value = item.Count,
                Count = item.Count
            })
            .ToList();

        var monthlyPurchases = windows
            .Select(window =>
            {
                var records = purchases
                    .Where(item =>
                        item.PurchaseDate >= window.Start &&
                        item.PurchaseDate < window.End)
                    .ToList();

                return new AnalyticsPointDto
                {
                    Label = window.Label,
                    Value = records.Sum(item => item.Total),
                    Count = records.Count
                };
            })
            .ToList();

        var totalRevenue = sales.Sum(item => item.Total);
        var totalPurchases = purchases.Sum(item => item.Total);

        var overview = new AnalyticsOverviewDto
        {
            Funnel = BuildFunnel(quotes, orders, sales),
            MonthlyRevenue = monthlyRevenue,
            MonthlySales = monthlySales,
            MonthlyPurchases = monthlyPurchases,

            QuoteStatuses = GroupStatuses(
                quotes.Select(item => item.Status)
            ),

            OrderStatuses = GroupStatuses(
                orders.Select(item => item.Status)
            ),

            LicenseStatuses = GroupStatuses(
                licenses.Select(item => item.Status)
            ),

            ProductionStatuses = GroupStatuses(
                productionOrders.Select(item =>
                    item.Status.ToString())
            ),

            WasteClassifications = GroupStatuses(
                wastes.Select(item =>
                    item.Classification.ToString())
            ),

            TopProducts = sales
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
                .Take(6)
                .ToList(),

            TopCustomers = sales
                .GroupBy(item => new
                {
                    item.RecipientType,
                    item.RecipientName
                })
                .Select(group => new AnalyticsCategoryDto
                {
                    Key =
                        $"{group.Key.RecipientType}:{group.Key.RecipientName}",
                    Label = group.Key.RecipientName,
                    Count = group.Count(),
                    Value = group.Sum(item => item.Total)
                })
                .OrderByDescending(item => item.Value)
                .Take(6)
                .ToList(),

            TotalRevenue = totalRevenue,
            TotalPurchases = totalPurchases,
            GrossCommercialMargin =
                totalRevenue - totalPurchases,
            AverageTicket = sales.Count == 0
                ? 0
                : decimal.Round(
                    totalRevenue / sales.Count,
                    2,
                    MidpointRounding.AwayFromZero
                )
        };

        return Ok(
            ApiResponse<AnalyticsOverviewDto>.Ok(
                overview,
                "Analítica obtenida correctamente"
            )
        );
    }

    private static CommercialFunnelDto BuildFunnel(
        IReadOnlyCollection<Models.Quote> quotes,
        IReadOnlyCollection<Models.Order> orders,
        IReadOnlyCollection<Models.Sale> sales)
    {
        var approved = quotes.Count(item =>
            item.Status is "Approved" or "Converted");

        var converted = quotes.Count(item =>
            item.Status == "Converted");

        var soldOrders = orders.Count(item =>
            item.Status == "Sold");

        return new CommercialFunnelDto
        {
            Quotes = quotes.Count,
            ApprovedQuotes = approved,
            ConvertedQuotes = converted,
            Orders = orders.Count,
            SoldOrders = soldOrders,
            Sales = sales.Count,

            ApprovalRate = Percentage(
                approved,
                quotes.Count
            ),

            QuoteToOrderRate = Percentage(
                orders.Count,
                quotes.Count
            ),

            OrderToSaleRate = Percentage(
                sales.Count,
                orders.Count
            )
        };
    }

    private static List<AnalyticsCategoryDto> GroupStatuses(
        IEnumerable<string> values)
    {
        return values
            .Where(value =>
                !string.IsNullOrWhiteSpace(value))
            .GroupBy(value => value)
            .Select(group => new AnalyticsCategoryDto
            {
                Key = group.Key,
                Label = TranslateStatus(group.Key),
                Count = group.Count(),
                Value = group.Count()
            })
            .OrderByDescending(item => item.Count)
            .ToList();
    }

    private static string TranslateStatus(string status)
    {
        return status switch
        {
            "Pending" => "Pendiente",
            "Approved" => "Aprobada",
            "Rejected" => "Rechazada",
            "Cancelled" => "Cancelada",
            "Converted" => "Convertida",
            "PendingConfirmation" => "Por confirmar",
            "AwaitingProduction" => "Espera producción",
            "ReadyForSale" => "Lista para venta",
            "Sold" => "Vendida",
            "Available" => "Disponible",
            "Active" => "Activa",
            "Expired" => "Vencida",
            "Revoked" => "Revocada",
            "Created" => "Creada",
            "InProgress" => "En proceso",
            "Completed" => "Completada",
            "Reusable" => "Reutilizable",
            "Recyclable" => "Reciclable",
            "Sellable" => "Vendible",
            "Rework" => "Retrabajo",
            "FinalWaste" => "Desecho final",
            _ => status
        };
    }

    private static decimal Percentage(
        long numerator,
        long denominator)
    {
        return denominator == 0
            ? 0
            : decimal.Round(
                numerator * 100m / denominator,
                2,
                MidpointRounding.AwayFromZero
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
