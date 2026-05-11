using Market.Dtos;
using Market.Implimitation.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Market.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto dto)
    {
        var result = await _authService.AuthenticateAsync(dto);

        if (!result.Success)
            return Unauthorized(result);

        return Ok(result);
    }
}