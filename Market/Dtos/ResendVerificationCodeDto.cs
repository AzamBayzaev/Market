using System.ComponentModel.DataAnnotations;

namespace Market.Dtos;

public class ResendVerificationCodeDto
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
}