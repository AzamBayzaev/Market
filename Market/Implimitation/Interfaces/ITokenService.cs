using Market.Entity;

namespace Market.Implimitation.Interfaces;

public interface ITokenService
{
    string CreateToken(UserEntity user);
}