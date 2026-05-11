using Market.Data;
using Market.Dtos;
using Market.Entity;
using Market.Implimitation.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Market.Implimitation.Services;

public class ProductService : IProduct
{
    private readonly AppDbContext _db;
    private readonly IDistributedCache _cache;
    private readonly ILogger<ProductService> _logger;

    private const string ProductsVersionKey = "products:version";
    private static readonly TimeSpan ProductsCacheTtl = TimeSpan.FromMinutes(5);

    public ProductService(AppDbContext db, IDistributedCache cache, ILogger<ProductService> logger)
    {
        _db = db;
        _cache = cache;
        _logger = logger;
    }

    public async Task<IEnumerable<ProductDto>> GetProductsAsync(ProductQueryDto query)
    {
        _logger.LogDebug(
            "Getting products. Page: {PageNumber}, Size: {PageSize}, Name: {Name}, MinPrice: {MinPrice}, MaxPrice: {MaxPrice}, SellerId: {SellerId}",
            query.PageNumber, query.PageSize, query.Name, query.MinPrice, query.MaxPrice, query.SellerId);

        var version = await GetProductsVersionAsync();
        var cacheKey = BuildProductsCacheKey(query, version);

        var cachedJson = await _cache.GetStringAsync(cacheKey);

        if (!string.IsNullOrWhiteSpace(cachedJson))
        {
            _logger.LogDebug("Products cache hit. Key: {CacheKey}", cacheKey);

            var cachedProducts = JsonSerializer.Deserialize<List<ProductDto>>(cachedJson);
            if (cachedProducts != null)
                return cachedProducts;
        }
        else
        {
            _logger.LogDebug("Products cache miss. Key: {CacheKey}", cacheKey);
        }

        var productsQuery = _db.Products.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Name))
            productsQuery = productsQuery.Where(x => x.Name.Contains(query.Name));

        if (query.MinPrice.HasValue)
            productsQuery = productsQuery.Where(x => x.Price >= query.MinPrice.Value);

        if (query.MaxPrice.HasValue)
            productsQuery = productsQuery.Where(x => x.Price <= query.MaxPrice.Value);

        if (query.SellerId.HasValue)
            productsQuery = productsQuery.Where(x => x.SellerId == query.SellerId.Value);

        var products = await productsQuery
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(x => new ProductDto
            {
                Name = x.Name,
                Price = x.Price,
                ProductId = x.ProductId,
                CreatedAt = x.CreatedAt,
                SellerId = x.SellerId
            })
            .ToListAsync();

        await _cache.SetStringAsync(
            cacheKey,
            JsonSerializer.Serialize(products),
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = ProductsCacheTtl
            });

        _logger.LogInformation("Returned {Count} products", products.Count);

        return products;
    }

    public async Task<ProductDto?> CreateProductAsync(ProductCreateDto dto)
    {
        _logger.LogInformation("Creating product. Name: {Name}, SellerId: {SellerId}", dto.Name, dto.SellerId);

        var seller = await _db.Users.FirstOrDefaultAsync(x => x.UserId == dto.SellerId);
        if (seller == null)
        {
            _logger.LogWarning("Create product failed: seller not found. SellerId: {SellerId}", dto.SellerId);
            return null;
        }

        if (await _db.Products.AnyAsync(x => x.Name == dto.Name && x.SellerId == dto.SellerId))
        {
            _logger.LogWarning("Create product failed: duplicate product for seller. Name: {Name}, SellerId: {SellerId}", dto.Name, dto.SellerId);
            return null;
        }

        var product = new ProductEntity
        {
            Name = dto.Name,
            Price = dto.Price,
            SellerId = seller.UserId,
            CreatedAt = DateTime.UtcNow
        };

        await _db.AddAsync(product);
        await _db.SaveChangesAsync();

        await InvalidateProductsCacheAsync();

        _logger.LogInformation("Product created successfully. ProductId: {ProductId}", product.ProductId);

        return new ProductDto
        {
            ProductId = product.ProductId,
            Name = product.Name,
            Price = product.Price,
            SellerId = product.SellerId,
            CreatedAt = product.CreatedAt
        };
    }

    public async Task<ProductDto?> DeleteProductAsync(int productId)
    {
        _logger.LogInformation("Deleting product. ProductId: {ProductId}", productId);

        var res = await _db.Products.FirstOrDefaultAsync(x => x.ProductId == productId);

        if (res == null)
        {
            _logger.LogWarning("Delete product failed: product not found. ProductId: {ProductId}", productId);
            return null;
        }

        _db.Products.Remove(res);
        await _db.SaveChangesAsync();

        await InvalidateProductsCacheAsync();

        _logger.LogInformation("Product deleted successfully. ProductId: {ProductId}", productId);

        return new ProductDto { ProductId = res.ProductId };
    }

    public async Task<ProductDto?> RestoreProductAsync(int productId)
    {
        _logger.LogInformation("Restoring product. ProductId: {ProductId}", productId);

        var product = await _db.Products
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.ProductId == productId);

        if (product == null)
        {
            _logger.LogWarning("Restore product failed: product not found. ProductId: {ProductId}", productId);
            return null;
        }

        if (!product.IsDeleted)
        {
            _logger.LogWarning("Restore product failed: product is not deleted. ProductId: {ProductId}", productId);
            return null;
        }

        var conflict = await _db.Products.AnyAsync(x =>
            !x.IsDeleted &&
            x.ProductId != product.ProductId &&
            x.Name == product.Name &&
            x.SellerId == product.SellerId);

        if (conflict)
        {
            _logger.LogWarning(
                "Restore product failed: conflict found. ProductId: {ProductId}, Name: {Name}, SellerId: {SellerId}",
                productId, product.Name, product.SellerId);
            return null;
        }

        product.IsDeleted = false;
        product.DeletedAt = null;
        product.DeletedBy = null;

        await _db.SaveChangesAsync();
        await InvalidateProductsCacheAsync();

        _logger.LogInformation("Product restored successfully. ProductId: {ProductId}", productId);

        return new ProductDto
        {
            ProductId = product.ProductId,
            Name = product.Name,
            Price = product.Price,
            CreatedAt = product.CreatedAt,
            SellerId = product.SellerId
        };
    }

    public async Task<bool> HardDeleteProductAsync(int productId)
    {
        _logger.LogInformation("Hard deleting product. ProductId: {ProductId}", productId);

        var affected = await _db.Products
            .IgnoreQueryFilters()
            .Where(x => x.ProductId == productId)
            .ExecuteDeleteAsync();

        if (affected == 0)
        {
            _logger.LogWarning("Hard delete failed: product not found. ProductId: {ProductId}", productId);
            return false;
        }

        await InvalidateProductsCacheAsync();

        _logger.LogInformation("Product hard deleted successfully. ProductId: {ProductId}", productId);

        return true;
    }

    private static string BuildProductsCacheKey(ProductQueryDto query, string version)
    {
        var name = string.IsNullOrWhiteSpace(query.Name)
            ? "all"
            : query.Name.Trim().ToLowerInvariant();

        var sellerId = query.SellerId.HasValue
            ? query.SellerId.Value.ToString()
            : "all";

        var minPrice = query.MinPrice.HasValue
            ? query.MinPrice.Value.ToString()
            : "all";

        var maxPrice = query.MaxPrice.HasValue
            ? query.MaxPrice.Value.ToString()
            : "all";

        return $"products:v{version}:page:{query.PageNumber}:name:{name}:min:{minPrice}:max:{maxPrice}:seller:{sellerId}";
    }

    private async Task<string> GetProductsVersionAsync()
    {
        var version = await _cache.GetStringAsync(ProductsVersionKey);

        if (string.IsNullOrWhiteSpace(version))
        {
            version = "1";
            await _cache.SetStringAsync(ProductsVersionKey, version);
            _logger.LogDebug("Products version cache initialized");
        }

        return version;
    }

    private async Task InvalidateProductsCacheAsync()
    {
        var version = await _cache.GetStringAsync(ProductsVersionKey);

        var nextVersion = int.TryParse(version, out var current)
            ? (current + 1).ToString()
            : "1";

        await _cache.SetStringAsync(ProductsVersionKey, nextVersion);

        _logger.LogDebug("Products cache invalidated. New version: {Version}", nextVersion);
    }
}