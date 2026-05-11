using System.ComponentModel.DataAnnotations;

namespace Market.Dtos;

public class VerifyEmailCodeDto
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MinLength(6)]
    [MaxLength(6)]
    public string Code { get; set; } = string.Empty;
}