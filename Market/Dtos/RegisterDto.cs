using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Market.Dtos;

public class RegisterDto
{
    [Required]
    [MinLength(4)]
    [MaxLength(20)]
    public string? Name { get; set; }
    
    [Required]
    [EmailAddress]
    public string? Email { get; set; }
    
    [Required]
    [PasswordPropertyText(true)]
    public string? PasswordHash { get; set; }
}