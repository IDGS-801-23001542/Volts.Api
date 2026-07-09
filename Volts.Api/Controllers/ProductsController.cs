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
public class ProductsController : ControllerBase
{
    private readonly MongoDbService _db;

    public ProductsController(MongoDbService db)
    {
        _db = db;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetAll()
    {
        var products = await _db.Products
            .Find(x => !x.IsDeleted && x.IsActive)
            .ToListAsync();

        return Ok(ApiResponse<List<Product>>.Ok(products));
    }

    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetById(string id)
    {
        var product = await _db.Products
            .Find(x => x.Id == id && !x.IsDeleted)
            .FirstOrDefaultAsync();

        if (product == null)
            return NotFound(ApiResponse<Product>.Fail("Producto no encontrado"));

        return Ok(ApiResponse<Product>.Ok(product));
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create(ProductCreateDto dto)
    {
        var product = new Product
        {
            Name = dto.Name,
            Slug = dto.Slug,
            Description = dto.Description,
            Price = dto.Price,
            Category = dto.Category
        };

        await _db.Products.InsertOneAsync(product);

        return Ok(ApiResponse<Product>.Ok(product, "Producto creado correctamente"));
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(string id, ProductUpdateDto dto)
    {
        var product = await _db.Products
            .Find(x => x.Id == id && !x.IsDeleted)
            .FirstOrDefaultAsync();

        if (product == null)
            return NotFound(ApiResponse<Product>.Fail("Producto no encontrado"));

        product.Name = dto.Name;
        product.Slug = dto.Slug;
        product.Description = dto.Description;
        product.Price = dto.Price;
        product.Category = dto.Category;
        product.IsActive = dto.IsActive;
        product.UpdatedAt = DateTime.UtcNow;

        await _db.Products.ReplaceOneAsync(x => x.Id == id, product);

        return Ok(ApiResponse<Product>.Ok(product, "Producto actualizado correctamente"));
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(string id)
    {
        var update = Builders<Product>.Update
            .Set(x => x.IsDeleted, true)
            .Set(x => x.UpdatedAt, DateTime.UtcNow);

        var result = await _db.Products.UpdateOneAsync(x => x.Id == id, update);

        if (result.ModifiedCount == 0)
            return NotFound(ApiResponse<string>.Fail("Producto no encontrado"));

        return Ok(ApiResponse<string>.Ok("Producto eliminado correctamente"));
    }
}