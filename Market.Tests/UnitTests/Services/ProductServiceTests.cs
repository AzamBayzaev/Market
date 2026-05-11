    using Xunit;
    using Moq;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Caching.Distributed;
    using Microsoft.EntityFrameworkCore;
    using System.Text;
    using System.Text.Json;
    using Market.Data;
    using Market.Entity;
    using Market.Dtos;
    using Market.Implimitation.Services;

    public class ProductServiceTests
    {
        private AppDbContext GetDbContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            return new AppDbContext(options);
        }

        private (ProductService service,
                 Mock<IDistributedCache> cache,
                 Mock<ILogger<ProductService>> logger,
                 AppDbContext db) CreateService()
        {
            var db = GetDbContext();
            var cache = new Mock<IDistributedCache>();
            var logger = new Mock<ILogger<ProductService>>();

            var service = new ProductService(db, cache.Object, logger.Object);

            return (service, cache, logger, db);
        }

        private UserEntity CreateUser(int id = 1)
            => new UserEntity
            {
                UserId = id,
                Name = "Test User",
                Email = "test@mail.com",
                PasswordHash = "hash"
            };

        private ProductEntity CreateProduct(int sellerId = 1)
            => new ProductEntity
            {
                Name = "Test",
                Price = 100,
                SellerId = sellerId,
                CreatedAt = DateTime.UtcNow
            };

        private static byte[] ToBytes(string value)
            => Encoding.UTF8.GetBytes(value);

        [Fact]
        public async Task CreateProductAsync_ShouldCreateProduct_WhenSellerExists()
        {
            var (service, cache, logger, db) = CreateService();

            db.Users.Add(CreateUser());
            await db.SaveChangesAsync();

            var result = await service.CreateProductAsync(new ProductCreateDto
            {
                Name = "Phone",
                Price = 100,
                SellerId = 1
            });

            Assert.NotNull(result);
            Assert.Equal("Phone", result!.Name);
        }

        [Fact]
        public async Task CreateProductAsync_ShouldReturnNull_WhenSellerNotFound()
        {
            var (service, cache, logger, db) = CreateService();

            var result = await service.CreateProductAsync(new ProductCreateDto
            {
                Name = "Phone",
                Price = 100,
                SellerId = 999
            });

            Assert.Null(result);
        }

        [Fact]
        public async Task DeleteProductAsync_ShouldDeleteProduct_WhenExists()
        {
            var (service, cache, logger, db) = CreateService();

            var product = CreateProduct();
            db.Products.Add(product);
            await db.SaveChangesAsync();

            var result = await service.DeleteProductAsync(product.ProductId);

            Assert.NotNull(result);
            Assert.Equal(product.ProductId, result!.ProductId);
        }

        [Fact]
        public async Task DeleteProductAsync_ShouldReturnNull_WhenNotFound()
        {
            var (service, cache, logger, db) = CreateService();

            var result = await service.DeleteProductAsync(999);

            Assert.Null(result);
        }

        [Fact]
        public async Task GetProductsAsync_ShouldReturnFromDb_WhenCacheMiss()
        {
            var (service, cache, logger, db) = CreateService();

            db.Users.Add(CreateUser());
            db.Products.Add(CreateProduct());
            await db.SaveChangesAsync();

            cache
                .Setup(x => x.GetAsync(It.IsAny<string>(), default))
                .ReturnsAsync((byte[])null!);

            var result = await service.GetProductsAsync(new ProductQueryDto
            {
                PageNumber = 1,
                PageSize = 10
            });

            Assert.Single(result);
        }

        [Fact]
        public async Task GetProductsAsync_ShouldReturnFromCache_WhenCacheHit()
        {
            var (service, cache, logger, db) = CreateService();

            var cached = new List<ProductDto>
            {
                new ProductDto { Name = "Cached", Price = 1 }
            };

            cache
                .Setup(x => x.GetAsync(It.IsAny<string>(), default))
                .ReturnsAsync(ToBytes(JsonSerializer.Serialize(cached)));

            var result = await service.GetProductsAsync(new ProductQueryDto
            {
                PageNumber = 1,
                PageSize = 10
            });

            Assert.Single(result);
            Assert.Equal("Cached", result.First().Name);
        }
    }