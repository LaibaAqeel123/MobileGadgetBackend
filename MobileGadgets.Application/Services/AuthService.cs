using MobileGadgets.Application.Dtos;
using MobileGadgets.Application.Interfaces;
using MobileGadgets.Domain;

namespace MobileGadgets.Application.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IAuthTokenService _tokenService;

    public AuthService(
        IUserRepository userRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IPasswordHasher passwordHasher,
        IAuthTokenService tokenService)
    {
        _userRepository = userRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
    }

    public async Task<AuthResult> LoginAsync(string email, string password)
    {
        var user = await _userRepository.GetByEmailAsync(email)
            ?? throw new UnauthorizedAccessException("Invalid email or password.");

        if (!_passwordHasher.Verify(user.PasswordHash, password))
            throw new UnauthorizedAccessException("Invalid email or password.");

        return await IssueTokensAsync(user);
    }

    public async Task<AuthResult> RefreshAsync(string rawRefreshToken)
    {
        var tokenHash = _tokenService.HashRefreshToken(rawRefreshToken);
        var stored = await _refreshTokenRepository.GetByTokenHashAsync(tokenHash)
            ?? throw new UnauthorizedAccessException("Invalid refresh token.");

        if (!stored.IsActive)
        {
            // A revoked token being presented again means it was copied — kill every active
            // session for this user so both the thief and the legitimate user get logged out.
            if (stored.RevokedAt is not null)
            {
                var active = await _refreshTokenRepository.GetActiveForUserAsync(stored.UserId);
                foreach (var t in active) t.RevokedAt = DateTime.UtcNow;
                await _refreshTokenRepository.SaveChangesAsync();
            }
            throw new UnauthorizedAccessException("Refresh token is no longer valid.");
        }

        var user = await _userRepository.GetByIdAsync(stored.UserId)
            ?? throw new UnauthorizedAccessException("User no longer exists.");

        var result = await IssueTokensAsync(user);
        stored.RevokedAt = DateTime.UtcNow;
        stored.ReplacedByTokenHash = _tokenService.HashRefreshToken(result.RawRefreshToken);
        await _refreshTokenRepository.SaveChangesAsync();

        return result;
    }

    public async Task LogoutAsync(string rawRefreshToken)
    {
        var tokenHash = _tokenService.HashRefreshToken(rawRefreshToken);
        var stored = await _refreshTokenRepository.GetByTokenHashAsync(tokenHash);
        if (stored is null || !stored.IsActive) return;

        stored.RevokedAt = DateTime.UtcNow;
        await _refreshTokenRepository.SaveChangesAsync();
    }

    private async Task<AuthResult> IssueTokensAsync(User user)
    {
        var accessToken = _tokenService.GenerateAccessToken(user);
        var rawRefreshToken = _tokenService.GenerateRawRefreshToken();
        var expiresAt = DateTime.UtcNow.AddDays(_tokenService.RefreshTokenDays);

        await _refreshTokenRepository.AddAsync(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = _tokenService.HashRefreshToken(rawRefreshToken),
            ExpiresAt = expiresAt,
            CreatedAt = DateTime.UtcNow,
        });
        await _refreshTokenRepository.SaveChangesAsync();

        return new AuthResult
        {
            AccessToken = accessToken,
            RawRefreshToken = rawRefreshToken,
            RefreshTokenExpiresAt = expiresAt,
            User = new UserDto { Id = user.Id, Email = user.Email, Role = user.Role.ToString(), CreatedAt = user.CreatedAt },
        };
    }
}
