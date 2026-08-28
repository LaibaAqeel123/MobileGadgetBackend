using MobileGadgets.Application.Dtos;

namespace MobileGadgets.Application.Interfaces;

public interface IUserService
{
    Task<List<UserDto>> GetAllAsync();

    /// <summary>Admin-only: provisions a team member account. Throws InvalidOperationException
    /// if the email is already taken or the role isn't valid.</summary>
    Task<UserDto> CreateAsync(CreateUserRequest request);
}
