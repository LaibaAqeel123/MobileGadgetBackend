using MobileGadgets.Application.Dtos;

namespace MobileGadgets.Application.Interfaces;

public interface IAuthService
{
    /// <summary>Throws UnauthorizedAccessException on bad credentials.</summary>
    Task<AuthResult> LoginAsync(string email, string password);

    /// <summary>Rotates the refresh token. Throws UnauthorizedAccessException if the token is
    /// missing/expired/revoked (a revoked-but-presented token revokes the whole session chain —
    /// that's a sign of theft).</summary>
    Task<AuthResult> RefreshAsync(string rawRefreshToken);

    Task LogoutAsync(string rawRefreshToken);
}
