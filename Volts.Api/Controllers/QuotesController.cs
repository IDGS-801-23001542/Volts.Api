using System.Net.Mail;
using System.Security.Claims;
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
public class QuotesController : ControllerBase
{
    private static readonly string[] AllowedStatuses =
    {
        "Pending",
        "Approved",
        "Rejected",
        "Cancelled"
    };

    private readonly MongoDbService _db;

    public QuotesController(MongoDbService db)
    {
        _db = db;
    }

    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> CreatePublic(
        [FromBody] QuoteCreateDto dto)
    {
        return await CreateInternal(
            dto,
            discount: 0,
            shipping: 0,
            validityDays: 15,
            conditions: null
        );
    }

    [HttpPost("backoffice")]
    [Authorize(Roles = "Admin,Employee")]
    public async Task<IActionResult> CreateBackoffice(
        [FromBody] QuoteBackofficeCreateDto dto)
    {
        return await CreateInternal(
            dto,
            dto.Discount,
            dto.Shipping,
            dto.ValidityDays,
            dto.Conditions
        );
    }

    [HttpGet]
    [Authorize(Roles = "Admin,Employee")]
    public async Task<IActionResult> GetAll()
    {
        var quotes = await _db.Quotes
            .Find(item => !item.IsDeleted)
            .SortByDescending(item => item.CreatedAt)
            .ToListAsync();

        return Ok(
            ApiResponse<List<Quote>>.Ok(
                quotes,
                "Cotizaciones obtenidas correctamente"
            )
        );
    }

    [HttpGet("{id}")]
    [Authorize(Roles = "Admin,Employee")]
    public async Task<IActionResult> GetById(string id)
    {
        var quote = await _db.Quotes
            .Find(item =>
                item.Id == id &&
                !item.IsDeleted)
            .FirstOrDefaultAsync();

        if (quote == null)
        {
            return NotFound(
                ApiResponse<Quote>.Fail(
                    "Cotización no encontrada"
                )
            );
        }

        return Ok(
            ApiResponse<Quote>.Ok(
                quote,
                "Cotización obtenida correctamente"
            )
        );
    }

    [HttpPut("{id}/pricing")]
    [Authorize(Roles = "Admin,Employee")]
    public async Task<IActionResult> UpdatePricing(
        string id,
        [FromBody] QuotePricingUpdateDto dto)
    {
        if (dto.Discount < 0 || dto.Shipping < 0)
        {
            return BadRequest(
                ApiResponse<Quote>.Fail(
                    "Descuento y envío no pueden ser negativos"
                )
            );
        }

        if (dto.ValidityDays < 1 || dto.ValidityDays > 90)
        {
            return BadRequest(
                ApiResponse<Quote>.Fail(
                    "La vigencia debe estar entre 1 y 90 días"
                )
            );
        }

        var quote = await _db.Quotes
            .Find(item =>
                item.Id == id &&
                !item.IsDeleted)
            .FirstOrDefaultAsync();

        if (quote == null)
        {
            return NotFound(
                ApiResponse<Quote>.Fail(
                    "Cotización no encontrada"
                )
            );
        }

        if (quote.Status == "Converted")
        {
            return BadRequest(
                ApiResponse<Quote>.Fail(
                    "Una cotización convertida ya no puede modificarse"
                )
            );
        }

        quote.Discount = dto.Discount;
        quote.Shipping = dto.Shipping;
        quote.Tax = RoundMoney(
            Math.Max(0, quote.Subtotal - quote.Discount) *
            quote.TaxRate
        );
        quote.Total = RoundMoney(
            Math.Max(0, quote.Subtotal - quote.Discount) +
            quote.Tax +
            quote.Shipping
        );
        quote.ValidUntil = DateTime.UtcNow
            .Date
            .AddDays(dto.ValidityDays);
        quote.Conditions = NormalizeOptional(dto.Conditions);
        quote.UpdatedAt = DateTime.UtcNow;
        quote.UpdatedBy = GetCurrentUserId();

        await _db.Quotes.ReplaceOneAsync(
            item => item.Id == quote.Id,
            quote
        );

        return Ok(
            ApiResponse<Quote>.Ok(
                quote,
                "Importes de la cotización actualizados correctamente"
            )
        );
    }

    [HttpPut("{id}/status")]
    [Authorize(Roles = "Admin,Employee")]
    public async Task<IActionResult> UpdateStatus(
        string id,
        [FromBody] QuoteStatusUpdateDto dto)
    {
        var status = dto.Status?.Trim();

        if (string.IsNullOrWhiteSpace(status) ||
            !AllowedStatuses.Contains(status))
        {
            return BadRequest(
                ApiResponse<Quote>.Fail(
                    "Estado de cotización inválido"
                )
            );
        }

        var quote = await _db.Quotes
            .Find(item =>
                item.Id == id &&
                !item.IsDeleted)
            .FirstOrDefaultAsync();

        if (quote == null)
        {
            return NotFound(
                ApiResponse<Quote>.Fail(
                    "Cotización no encontrada"
                )
            );
        }

        if (quote.Status == "Converted")
        {
            return BadRequest(
                ApiResponse<Quote>.Fail(
                    "Una cotización convertida no puede cambiar de estado"
                )
            );
        }

        quote.Status = status;
        quote.UpdatedAt = DateTime.UtcNow;
        quote.UpdatedBy = GetCurrentUserId();

        await _db.Quotes.ReplaceOneAsync(
            item => item.Id == quote.Id,
            quote
        );

        return Ok(
            ApiResponse<Quote>.Ok(
                quote,
                "Estado actualizado correctamente"
            )
        );
    }

    [HttpPost("{id}/convert-to-order")]
    [Authorize(Roles = "Admin,Employee")]
    public async Task<IActionResult> ConvertToOrder(string id)
    {
        var quote = await _db.Quotes
            .Find(item =>
                item.Id == id &&
                !item.IsDeleted)
            .FirstOrDefaultAsync();

        if (quote == null)
        {
            return NotFound(
                ApiResponse<Order>.Fail(
                    "Cotización no encontrada"
                )
            );
        }

        if (quote.Status != "Approved")
        {
            return BadRequest(
                ApiResponse<Order>.Fail(
                    "Solo una cotización aprobada puede convertirse en pedido"
                )
            );
        }

        if (!string.IsNullOrWhiteSpace(
                quote.ConvertedOrderId))
        {
            return Conflict(
                ApiResponse<Order>.Fail(
                    "La cotización ya fue convertida en pedido"
                )
            );
        }

        var existingOrder = await _db.Orders
            .Find(item =>
                item.QuoteId == quote.Id &&
                !item.IsDeleted)
            .FirstOrDefaultAsync();

        if (existingOrder != null)
        {
            return Conflict(
                ApiResponse<Order>.Fail(
                    "Ya existe un pedido para esta cotización"
                )
            );
        }

        var now = DateTime.UtcNow;

        var order = new Order
        {
            Folio = BuildFolio("ORD", now),
            QuoteId = quote.Id,
            QuoteFolio = quote.Folio,
            RecipientType = quote.RecipientType,
            CustomerId = quote.CustomerId,
            InstitutionId = quote.InstitutionId,
            RecipientName = quote.RecipientName,
            ContactName = quote.ContactName,
            Email = quote.Email,
            Phone = quote.Phone,
            CommercialPlanId = quote.CommercialPlanId,
            CommercialPlanName = quote.CommercialPlanName,
            CommercialPackageId = quote.CommercialPackageId,
            CommercialPackageName = quote.CommercialPackageName,
            Status = "PendingConfirmation",
            Subtotal = quote.Subtotal,
            Discount = quote.Discount,
            Tax = quote.Tax,
            Shipping = quote.Shipping,
            Total = quote.Total,
            Details = quote.Details
                .Select(item => new OrderDetail
                {
                    ProductId = item.ProductId,
                    ProductName = item.ProductName,
                    RequestedQuantity = item.TotalQuantity,
                    ReservedQuantity = 0,
                    PendingQuantity = item.TotalQuantity,
                    UnitPrice = item.UnitPrice,
                    Subtotal = item.Subtotal
                })
                .ToList(),
            IsDeleted = false,
            CreatedAt = now,
            CreatedBy = GetCurrentUserId()
        };

        using var session =
            await _db.StartSessionAsync();

        session.StartTransaction();

        try
        {
            await _db.Orders.InsertOneAsync(
                session,
                order
            );

            quote.Status = "Converted";
            quote.ConvertedOrderId = order.Id;
            quote.UpdatedAt = now;
            quote.UpdatedBy = GetCurrentUserId();

            await _db.Quotes.ReplaceOneAsync(
                session,
                item =>
                    item.Id == quote.Id &&
                    item.Status == "Approved",
                quote
            );

            await session.CommitTransactionAsync();
        }
        catch
        {
            await session.AbortTransactionAsync();
            throw;
        }

        return CreatedAtAction(
            "GetById",
            "Orders",
            new { id = order.Id },
            ApiResponse<Order>.Ok(
                order,
                "Cotización convertida en pedido correctamente"
            )
        );
    }

    private async Task<IActionResult> CreateInternal(
        QuoteCreateDto dto,
        decimal discount,
        decimal shipping,
        int validityDays,
        string? conditions)
    {
        var validationError =
            ValidateCreate(
                dto,
                discount,
                shipping,
                validityDays
            );

        if (validationError != null)
        {
            return BadRequest(
                ApiResponse<Quote>.Fail(
                    validationError
                )
            );
        }

        var package = await _db.CommercialPackages
            .Find(item =>
                item.Id == dto.CommercialPackageId &&
                !item.IsDeleted &&
                item.IsActive)
            .FirstOrDefaultAsync();

        if (package == null)
        {
            return BadRequest(
                ApiResponse<Quote>.Fail(
                    "El paquete comercial no existe o está inactivo"
                )
            );
        }

        var plan = await _db.CommercialPlans
            .Find(item =>
                item.Id == package.CommercialPlanId &&
                !item.IsDeleted &&
                item.IsActive)
            .FirstOrDefaultAsync();

        if (plan == null)
        {
            return BadRequest(
                ApiResponse<Quote>.Fail(
                    "El plan comercial relacionado no existe o está inactivo"
                )
            );
        }

        var recipientResult =
            await ResolveRecipient(dto);

        if (!recipientResult.Success)
        {
            return BadRequest(
                ApiResponse<Quote>.Fail(
                    recipientResult.Error!
                )
            );
        }

        var details = package.Items
            .Select(item => new QuoteDetail
            {
                ProductId = item.ProductId,
                ProductName = item.ProductName,
                QuantityPerPackage = item.Quantity,
                TotalQuantity =
                    item.Quantity *
                    dto.PackageQuantity,
                UnitPrice = item.UnitPrice,
                Subtotal = RoundMoney(
                    item.UnitPrice *
                    item.Quantity *
                    dto.PackageQuantity
                )
            })
            .ToList();

        var subtotal = RoundMoney(
            package.Price *
            dto.PackageQuantity
        );

        var taxableBase =
            Math.Max(0, subtotal - discount);

        var tax = RoundMoney(
            taxableBase * 0.16m
        );

        var now = DateTime.UtcNow;

        var quote = new Quote
        {
            Folio = BuildFolio("QUO", now),
            RecipientType = dto.RecipientType.Trim(),
            CustomerId = recipientResult.CustomerId,
            InstitutionId = recipientResult.InstitutionId,
            RecipientName = recipientResult.RecipientName!,
            ContactName = recipientResult.ContactName!,
            Email = recipientResult.Email!,
            Phone = recipientResult.Phone,
            CommercialPlanId = plan.Id,
            CommercialPlanName = plan.Name,
            CommercialPackageId = package.Id,
            CommercialPackageName = package.Name,
            PackageQuantity = dto.PackageQuantity,
            PackageUnitPrice = package.Price,
            Details = details,
            Subtotal = subtotal,
            Discount = discount,
            TaxRate = 0.16m,
            Tax = tax,
            Shipping = shipping,
            Total = RoundMoney(
                taxableBase + tax + shipping
            ),
            ValidUntil = now.Date.AddDays(validityDays),
            Notes = NormalizeOptional(dto.Notes),
            Conditions = NormalizeOptional(conditions),
            Status = "Pending",
            IsDeleted = false,
            CreatedAt = now,
            CreatedBy = GetCurrentUserId()
        };

        await _db.Quotes.InsertOneAsync(quote);

        return CreatedAtAction(
            nameof(GetById),
            new { id = quote.Id },
            ApiResponse<Quote>.Ok(
                quote,
                "Cotización enviada correctamente"
            )
        );
    }

    private async Task<RecipientResult> ResolveRecipient(
        QuoteCreateDto dto)
    {
        if (dto.RecipientType == "Customer")
        {
            if (!string.IsNullOrWhiteSpace(dto.CustomerId))
            {
                var customer = await _db.Customers
                    .Find(item =>
                        item.Id == dto.CustomerId &&
                        !item.IsDeleted &&
                        item.IsActive)
                    .FirstOrDefaultAsync();

                if (customer == null)
                {
                    return RecipientResult.Fail(
                        "El cliente no existe o está inactivo"
                    );
                }

                return RecipientResult.Ok(
                    customer.Id,
                    null,
                    customer.FullName,
                    customer.FullName,
                    customer.Email,
                    customer.Phone
                );
            }

            return RecipientResult.Ok(
                null,
                null,
                dto.ContactName.Trim(),
                dto.ContactName.Trim(),
                dto.Email.Trim().ToLowerInvariant(),
                NormalizeOptional(dto.Phone)
            );
        }

        if (dto.RecipientType == "Institution")
        {
            if (string.IsNullOrWhiteSpace(
                    dto.InstitutionId))
            {
                return RecipientResult.Fail(
                    "Debes seleccionar una institución"
                );
            }

            var institution = await _db.Institutions
                .Find(item =>
                    item.Id == dto.InstitutionId &&
                    !item.IsDeleted &&
                    item.IsActive)
                .FirstOrDefaultAsync();

            if (institution == null)
            {
                return RecipientResult.Fail(
                    "La institución no existe o está inactiva"
                );
            }

            return RecipientResult.Ok(
                null,
                institution.Id,
                institution.Name,
                institution.Responsible.Name.FullName,
                institution.Responsible.Email,
                institution.Responsible.Phone
            );
        }

        return RecipientResult.Fail(
            "El tipo de destinatario no es válido"
        );
    }

    private static string? ValidateCreate(
        QuoteCreateDto dto,
        decimal discount,
        decimal shipping,
        int validityDays)
    {
        if (dto.RecipientType != "Customer" &&
            dto.RecipientType != "Institution")
        {
            return "El tipo de destinatario debe ser Customer o Institution";
        }

        if (string.IsNullOrWhiteSpace(
                dto.CommercialPackageId))
        {
            return "Debes seleccionar un paquete comercial";
        }

        if (dto.PackageQuantity <= 0)
        {
            return "La cantidad de paquetes debe ser mayor a cero";
        }

        if (discount < 0 || shipping < 0)
        {
            return "Descuento y envío no pueden ser negativos";
        }

        if (validityDays < 1 || validityDays > 90)
        {
            return "La vigencia debe estar entre 1 y 90 días";
        }

        if (dto.RecipientType == "Customer" &&
            string.IsNullOrWhiteSpace(dto.CustomerId))
        {
            if (string.IsNullOrWhiteSpace(dto.ContactName))
            {
                return "El nombre de contacto es obligatorio";
            }

            if (!IsValidEmail(dto.Email))
            {
                return "El correo electrónico no es válido";
            }
        }

        var phone = NormalizeOptional(dto.Phone);

        if (phone != null &&
            (phone.Length != 10 ||
             phone.Any(character =>
                 !char.IsDigit(character))))
        {
            return "El teléfono debe contener exactamente 10 dígitos";
        }

        if ((dto.Notes?.Trim().Length ?? 0) > 1000)
        {
            return "Las notas no pueden superar 1000 caracteres";
        }

        return null;
    }

    private static bool IsValidEmail(string value)
    {
        try
        {
            _ = new MailAddress(value.Trim());
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static decimal RoundMoney(decimal value)
    {
        return decimal.Round(
            value,
            2,
            MidpointRounding.AwayFromZero
        );
    }

    private static string BuildFolio(
        string prefix,
        DateTime now)
    {
        return
            $"{prefix}-{now:yyyyMMdd-HHmmss}-" +
            Guid.NewGuid()
                .ToString("N")[..6]
                .ToUpperInvariant();
    }

    private static string? NormalizeOptional(
        string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private string? GetCurrentUserId()
    {
        return User.FindFirstValue(
            ClaimTypes.NameIdentifier
        );
    }

    private sealed class RecipientResult
    {
        public bool Success { get; private init; }
        public string? Error { get; private init; }
        public string? CustomerId { get; private init; }
        public string? InstitutionId { get; private init; }
        public string? RecipientName { get; private init; }
        public string? ContactName { get; private init; }
        public string? Email { get; private init; }
        public string? Phone { get; private init; }

        public static RecipientResult Ok(
            string? customerId,
            string? institutionId,
            string recipientName,
            string contactName,
            string email,
            string? phone)
        {
            return new RecipientResult
            {
                Success = true,
                CustomerId = customerId,
                InstitutionId = institutionId,
                RecipientName = recipientName,
                ContactName = contactName,
                Email = email,
                Phone = phone
            };
        }

        public static RecipientResult Fail(
            string error)
        {
            return new RecipientResult
            {
                Success = false,
                Error = error
            };
        }
    }
}
