using Market.Dtos;

namespace Market.Implimitation.Interfaces;

public interface IProduct
{
    Task<IEnumerable<ProductDto>> GetProductsAsync(ProductQueryDto query);
    Task<ProductDto?> CreateProductAsync(ProductCreateDto dto);
    Task<ProductDto?> DeleteProductAsync(int productId);
    Task<ProductDto?> RestoreProductAsync(int productId);
    Task<bool> HardDeleteProductAsync(int productId);
}