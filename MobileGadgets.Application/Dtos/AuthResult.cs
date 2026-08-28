namespace MobileGadgets.Application.Dtos;

/// <summary>RawRefreshToken is only ever consumed by the API layer to set the httpOnly cookie —
/// it must never be serialized into an HTTP response body.</summary>
public class AuthResult
{
    public string AccessToken { get; set; } = string.Empty;
    public string RawRefreshToken { get; set; } = string.Empty;
    public DateTime RefreshTokenExpiresAt { get; set; }
    public UserDto User { get; set; } = null!;
}
