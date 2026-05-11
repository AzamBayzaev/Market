using System.ComponentModel.DataAnnotations;

namespace Market.Dtos;

public class UserCreateDto
{
    [Required]
    [MinLength(2)]
    [MaxLength(20)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [MaxLength(30)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MinLength(6)]
    public string Password { get; set; } = string.Empty;

    [Required]
    [RegularExpression("^(Seller|User)$", ErrorMessage = "Role must be Seller or User")]
    public string Role { get; set; } = string.Empty;
}