using Market.Dtos;


namespace Market.Implimitation.Interfaces;

public interface IUser
{
    Task<IEnumerable<UserDto>> GetAsync(UserQueryDto query);
    Task<UserDto?> CreateAsync(UserCreateDto user);
    Task<(UserDto?, string)> DeleteAsync(int id);
    Task<(UserDto?, string)> RestoreAsync(int id);
    Task<bool> HardDeleteAsync(int id);
}
