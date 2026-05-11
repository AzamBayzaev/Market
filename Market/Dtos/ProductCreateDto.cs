using System.ComponentModel.DataAnnotations;

namespace Market.Dtos;

public class ProductCreateDto
{
    [Required]
    [MinLength(2)]
    [MaxLength(20)]
    public string Name { get; set; } = string.Empty;

    [Range(0.01, double.MaxValue, ErrorMessage = "Price must be greater than zero")]
    public decimal Price { get; set; }

    [Required]
    public int SellerId { get; set; }
}