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
[Authorize(Roles = "Admin")]
public class EtlLogsController : ControllerBase
{
    private readonly MongoDbService _db;

    public EtlLogsController(MongoDbService db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var logs = await _db.EtlLogs
            .Find(item => !item.IsDeleted)
            .SortByDescending(item => item.StartedAt)
            .Limit(250)
            .ToListAsync();

        return Ok(
            ApiResponse<List<EtlLog>>.Ok(
                logs,
                "Procesos ETL obtenidos correctamente"
            )
        );
    }

    [HttpPost("run-business-snapshot")]
    public async Task<IActionResult> RunBusinessSnapshot()
    {
        var log = new EtlLog
        {
            ProcessName = "VOLTS Business Snapshot",
            Source = "MongoDB empresarial",
            Destination = "Dashboard y analítica",
            Status = "Running",
            StartedAt = DateTime.UtcNow,
            Phases = new List<string>
            {
                "Selección",
                "Preprocesamiento",
                "Minería descriptiva",
                "Interpretación",
                "Difusión"
            },
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = User.Identity?.Name
        };

        await _db.EtlLogs.InsertOneAsync(log);

        try
        {
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

            var products = await _db.Products
                .Find(item => !item.IsDeleted)
                .ToListAsync();

            var rawMaterials = await _db.RawMaterials
                .Find(item => !item.IsDeleted)
                .ToListAsync();

            var sourceRecords =
                customers.Count +
                institutions.Count +
                quotes.Count +
                orders.Count +
                sales.Count +
                products.Count +
                rawMaterials.Count;

            var rejected =
                customers.Count(item =>
                    string.IsNullOrWhiteSpace(item.Email)) +
                institutions.Count(item =>
                    string.IsNullOrWhiteSpace(
                        item.Responsible.Email)) +
                quotes.Count(item =>
                    item.Total < 0) +
                orders.Count(item =>
                    item.Total < 0) +
                sales.Count(item =>
                    item.Total < 0) +
                products.Count(item =>
                    item.PhysicalStock < 0 ||
                    item.ReservedStock < 0 ||
                    item.AvailableStock < 0) +
                rawMaterials.Count(item =>
                    item.CurrentStock < 0);

            var findings = new List<string>
            {
                $"Se seleccionaron {sourceRecords} registros empresariales.",
                $"Se detectaron {rejected} registros con posibles inconsistencias.",
                $"La conversión de cotización a pedido es {Percentage(orders.Count, quotes.Count):0.##}%.",
                $"La conversión de pedido a venta es {Percentage(sales.Count, orders.Count):0.##}%.",
                $"Existen {products.Count(item => item.AvailableStock <= item.MinimumFinishedStock)} productos terminados bajo mínimo.",
                $"Existen {rawMaterials.Count(item => item.CurrentStock <= item.MinimumStock)} materias primas bajo mínimo."
            };

            log.Status = rejected == 0
                ? "Completed"
                : "CompletedWithWarnings";
            log.RecordsRead = sourceRecords;
            log.RecordsProcessed = sourceRecords - rejected;
            log.RecordsRejected = rejected;
            log.Findings = findings;
            log.FinishedAt = DateTime.UtcNow;
            log.UpdatedAt = DateTime.UtcNow;
            log.UpdatedBy = User.Identity?.Name;

            await _db.EtlLogs.ReplaceOneAsync(
                item => item.Id == log.Id,
                log
            );

            return Ok(
                ApiResponse<EtlLog>.Ok(
                    log,
                    "Proceso ETL ejecutado correctamente"
                )
            );
        }
        catch (Exception exception)
        {
            log.Status = "Failed";
            log.ErrorMessage = exception.Message;
            log.FinishedAt = DateTime.UtcNow;
            log.UpdatedAt = DateTime.UtcNow;

            await _db.EtlLogs.ReplaceOneAsync(
                item => item.Id == log.Id,
                log
            );

            throw;
        }
    }

    private static decimal Percentage(
        int numerator,
        int denominator)
    {
        return denominator == 0
            ? 0
            : decimal.Round(
                numerator * 100m / denominator,
                2,
                MidpointRounding.AwayFromZero
            );
    }
}
