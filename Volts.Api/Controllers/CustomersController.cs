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
[Authorize(Roles = "Admin,Employee")]
public class CustomersController : ControllerBase
{
    private readonly MongoDbService _db;

    public CustomersController(MongoDbService db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var customers = await _db.Customers
            .Find(x => !x.IsDeleted)
            .SortByDescending(x => x.CreatedAt)
            .ToListAsync();

        return Ok(ApiResponse<List<Customer>>.Ok(customers));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        var customer = await _db.Customers
            .Find(x => x.Id == id && !x.IsDeleted)
            .FirstOrDefaultAsync();

        if (customer == null)
            return NotFound(ApiResponse<Customer>.Fail("Cliente no encontrado"));

        return Ok(ApiResponse<Customer>.Ok(customer));
    }

    [HttpPost]
    public async Task<IActionResult> Create(CustomerCreateDto dto)
    {
        var exists = await _db.Customers
            .Find(x => x.Email.ToLower() == dto.Email.ToLower() && !x.IsDeleted)
            .AnyAsync();

        if (exists)
            return BadRequest(ApiResponse<Customer>.Fail("Ya existe un cliente con ese correo"));

        var customer = new Customer
        {
            CustomerType = dto.CustomerType,
            FullName = dto.FullName,
            InstitutionName = dto.InstitutionName,
            Email = dto.Email.ToLower(),
            Phone = dto.Phone,
            Address = dto.Address,
            IsActive = true
        };

        await _db.Customers.InsertOneAsync(customer);

        return Ok(ApiResponse<Customer>.Ok(customer, "Cliente creado correctamente"));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, CustomerUpdateDto dto)
    {
        var customer = await _db.Customers
            .Find(x => x.Id == id && !x.IsDeleted)
            .FirstOrDefaultAsync();

        if (customer == null)
            return NotFound(ApiResponse<Customer>.Fail("Cliente no encontrado"));

        customer.CustomerType = dto.CustomerType;
        customer.FullName = dto.FullName;
        customer.InstitutionName = dto.InstitutionName;
        customer.Email = dto.Email.ToLower();
        customer.Phone = dto.Phone;
        customer.Address = dto.Address;
        customer.IsActive = dto.IsActive;
        customer.UpdatedAt = DateTime.UtcNow;

        await _db.Customers.ReplaceOneAsync(x => x.Id == id, customer);

        return Ok(ApiResponse<Customer>.Ok(customer, "Cliente actualizado correctamente"));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var update = Builders<Customer>.Update
            .Set(x => x.IsDeleted, true)
            .Set(x => x.UpdatedAt, DateTime.UtcNow);

        var result = await _db.Customers.UpdateOneAsync(x => x.Id == id && !x.IsDeleted, update);

        if (result.ModifiedCount == 0)
            return NotFound(ApiResponse<string>.Fail("Cliente no encontrado"));

        return Ok(ApiResponse<string>.Ok("Cliente eliminado correctamente"));
    }
}