using MobileGadgets.Domain;

namespace MobileGadgets.Application.Interfaces;

public interface IAuthTokenService
{
    string GenerateAccessToken(User user);

    /// <summary>A random raw refresh token — the caller hashes it before storing.</summary>
    string GenerateRawRefreshToken();

    string HashRefreshToken(string rawToken);

    int RefreshTokenDays { get; }
}
