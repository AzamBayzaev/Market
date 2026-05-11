using Market.Entity;

namespace Market.Dtos;

public class AuthResultDto
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public AuthDataDto? Data { get; set; }
}