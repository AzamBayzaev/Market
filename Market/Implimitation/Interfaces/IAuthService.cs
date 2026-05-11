using Market.Entity;
using Market.Dtos;

namespace Market.Implimitation.Interfaces;

public interface IAuthService
{
   Task<AuthResultDto> AuthenticateAsync(LoginDto login);
}