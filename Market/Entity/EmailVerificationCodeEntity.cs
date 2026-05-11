using System.ComponentModel.DataAnnotations;

namespace Market.Entity;

public class EmailVerificationCodeEntity
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int UserId { get; set; }

    [Required]
    public string Code { get; set; } = string.Empty;

    [Required]
    public DateTime ExpiresAt { get; set; }
    
    public bool IsUsed { get; set; }

    public UserEntity User { get; set; } = null!;
}