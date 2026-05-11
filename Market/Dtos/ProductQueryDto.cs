using System.ComponentModel.DataAnnotations;

namespace Market.Dtos;

public class ProductQueryDto
{
    [Required]
    public int PageNumber { get; set; } = 1;
    [Range(20,35)]
    public  int PageSize { get; set; } = 20;
    public string? Name { get; set; }
    public decimal? MinPrice { get; set; } = 1;
    public decimal? MaxPrice { get; set; } = 9999;
    public int? SellerId { get; set; }
}