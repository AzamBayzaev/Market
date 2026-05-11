using Xunit;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Market.Data;
using Market.Dtos;
using Market.Entity;
using Market.Implimitation.Services;


namespace Market.Tests.IntegrationTests.Services;

public class ProductServiceIntegrationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;
    private readonly ProductService _service;

    public ProductServiceIntegrationTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new AppDbContext(options);
        _db.Database.EnsureCreated();

        IDistributedCache cache = new MemoryDistributedCache(
            Microsoft.Extensions.Options.Options.Create(new MemoryDistributedCacheOptions()));
        
        _service = new ProductService(
            _db,
            cache,
            NullLogger<ProductService>.Instance);
    }
    
    private static UserEntity CreateSeller(int id = 1) => new()
    {
        UserId = id,
        Name = "Seller",
        Email = "seller@mail.com",
        Role = "Seller",
        PasswordHash = "hashed_password"
    };

    [Fact]
    public async Task CreateProductAsync_ThenGetProductsAsync_ShouldReturnSavedProduct()
    {
        _db.Users.Add(CreateSeller());

        await _db.SaveChangesAsync();

        var created = await _service.CreateProductAsync(new ProductCreateDto
        {
            Name = "Phone",
            Price = 1000,
            SellerId = 1
        });

        var products = await _service.GetProductsAsync(new ProductQueryDto
        {
            PageNumber = 1,
            PageSize = 10
        });

        Assert.NotNull(created);
        Assert.Equal("Phone", created!.Name);

        Assert.Single(products);
        Assert.Equal("Phone", products.First().Name);
        Assert.Equal(1000, products.First().Price);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}
