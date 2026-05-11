using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Market.Implimitation.Interfaces;

namespace Market.Entity;

public class ProductEntity : ISoftDelete
{
    [Key]
    public int ProductId { get; set; }

    [Required]
    [MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [Range(0.01, double.MaxValue)]
    public decimal Price { get; set; }

    [Required]
    public DateTime CreatedAt { get; set; }

    [Required]
    public int SellerId { get; set; }

    public bool IsDeleted { get; set; }
    
    public DateTime? DeletedAt { get; set; }
    
    public string? DeletedBy { get; set; }
    
    [ForeignKey(nameof(SellerId))]
    public UserEntity Seller { get; set; } = null!;
}













