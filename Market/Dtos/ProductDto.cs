using System.ComponentModel.DataAnnotations;

namespace Market.Dtos;

public class ProductDto
{
    [Key]
    public int ProductId { get; set; }

    [Required]
    public string Name { get; set; } = null!;

    [Required]
    public decimal Price { get; set; }

    [Required]
    public DateTime CreatedAt { get; set; }

    [Required]
    public int SellerId { get; set; }   
}