using MongoDB.Driver;
using Volts.Api.Models;

namespace Volts.Api.Repositories;

public class GenericRepository<T> : IGenericRepository<T> where T : BaseEntity
{
    private readonly IMongoCollection<T> _collection;

    public GenericRepository(IMongoCollection<T> collection)
    {
        _collection = collection;
    }

    public async Task<List<T>> GetAllAsync()
    {
        return await _collection
            .Find(x => !x.IsDeleted)
            .ToListAsync();
    }

    public async Task<T?> GetByIdAsync(string id)
    {
        return await _collection
            .Find(x => x.Id == id && !x.IsDeleted)
            .FirstOrDefaultAsync();
    }

    public async Task<T> CreateAsync(T entity)
    {
        entity.CreatedAt = DateTime.UtcNow;
        await _collection.InsertOneAsync(entity);
        return entity;
    }

    public async Task<bool> UpdateAsync(string id, T entity)
    {
        entity.Id = id;
        entity.UpdatedAt = DateTime.UtcNow;

        var result = await _collection.ReplaceOneAsync(
            x => x.Id == id && !x.IsDeleted,
            entity
        );

        return result.ModifiedCount > 0;
    }

    public async Task<bool> SoftDeleteAsync(string id)
    {
        var update = Builders<T>.Update
            .Set(x => x.IsDeleted, true)
            .Set(x => x.UpdatedAt, DateTime.UtcNow);

        var result = await _collection.UpdateOneAsync(
            x => x.Id == id && !x.IsDeleted,
            update
        );

        return result.ModifiedCount > 0;
    }
}