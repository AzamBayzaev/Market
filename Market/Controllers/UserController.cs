using Market.Dtos;
using Market.Implimitation.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace Market.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UserController : ControllerBase
{
    private readonly IUser _userService;

    public UserController(IUser userService) => _userService = userService;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<UserDto>>> GetAsync([FromQuery] UserQueryDto query)
    {
        var res = await _userService.GetAsync(query);
        return Ok(res);
    }

    [HttpPost]
    public async Task<ActionResult<UserDto>> PostAsync([FromBody] UserCreateDto user)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var res = await _userService.CreateAsync(user);

        if (res == null)
            return Conflict("User already exists or data is invalid");

        return Ok(res);
    }

    [HttpDelete("{id:int}")]
    public async Task<ActionResult<UserDto>> DeleteAsync(int id)
    {
        var res = await _userService.DeleteAsync(id);

        if (res.Item1 == null)
            return NotFound(res.Item2);

        return Ok(res.Item1);
    }

    [HttpPost("{id:int}/restore")]
    public async Task<ActionResult<UserDto>> RestoreAsync(int id)
    {
        var res = await _userService.RestoreAsync(id);

        if (res.Item1 == null)
            return BadRequest(res.Item2);

        return Ok(res.Item1);
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("hard/{id:int}")]
    public async Task<IActionResult> HardDeleteAsync(int id)
    {
        var ok = await _userService.HardDeleteAsync(id);

        if (!ok)
            return NotFound("User not found");

        return NoContent();
    }
}