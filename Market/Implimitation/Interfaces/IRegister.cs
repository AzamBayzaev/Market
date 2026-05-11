using Market.Dtos;

namespace Market.Implimitation.Interfaces;

public interface IRegisterService
{
    Task<bool> RegisterAsync(RegisterDto dto, CancellationToken cancellationToken = default);
}