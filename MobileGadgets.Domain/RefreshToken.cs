namespace MobileGadgets.Domain;

/// <summary>Only the SHA-256 hash of the raw token is stored — the raw value lives solely in
/// the client's httpOnly cookie. ReplacedByTokenHash marks the chain for rotation/reuse
/// detection: if a revoked token is presented again, every active token for that user gets
/// revoked (the token was stolen and both parties are now racing to use it).</summary>
public class RefreshToken
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? ReplacedByTokenHash { get; set; }
    public DateTime CreatedAt { get; set; }

    public bool IsActive => RevokedAt is null && DateTime.UtcNow < ExpiresAt;
}
