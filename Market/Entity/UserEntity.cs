using System.ComponentModel.DataAnnotations;
using Market.Implimitation.Interfaces;

namespace Market.Entity;

public class UserEntity : ISoftDelete
{
    
    [Required]
    public string Name { get; set; }
    
    [Key]
    public int UserId { get; set; }
    
    [Required]
    [EmailAddress]
    
    public string Email { get; set; }

    [Required]
    public string PasswordHash  { get; set; }

    public string Role { get; set; } = "User";
    
    public bool IsDeleted { get; set; }
    
    public DateTime? DeletedAt { get; set; }
    
    public string? DeletedBy { get; set; }

    public bool IsVerified { get; set; } = false;
    
    public ICollection<ProductEntity> Products { get; set; } = new List<ProductEntity>();
}


















