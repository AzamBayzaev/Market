using Market.Dtos;
using Market.Implimitation.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Market.Controllers;

[ApiController]
[Route("api/")]
public class RegisterControler : ControllerBase
{
    private readonly IRegisterService _registerService;
    private readonly IVerificationCodeService _codeService;

    public RegisterControler(
        IRegisterService registerService,
        IVerificationCodeService codeService)
    {
        _registerService = registerService;
        _codeService = codeService;
    }
    
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterDto dto, CancellationToken cancellationToken)
    {
        var result = await _registerService.RegisterAsync(dto, cancellationToken);

        if (!result)
            return BadRequest("User already exists");

        return Ok("Verification code sent to email");
    }
    
    [HttpPost("verify")]
    public async Task<IActionResult> Verify([FromBody] VerifyEmailCodeDto dto, CancellationToken cancellationToken)
    {
        var result = await _codeService.VerifyCodeAsync(dto.Email, dto.Code, cancellationToken);

        if (!result)
            return BadRequest("Invalid or expired code");

        return Ok("Email confirmed successfully");
    }
}