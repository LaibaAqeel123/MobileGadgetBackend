using Microsoft.AspNetCore.Identity;
using MobileGadgets.Application.Interfaces;
using MobileGadgets.Domain;

namespace MobileGadgets.Infrastructure.Auth;

/// <summary>Wraps ASP.NET Identity's standalone PasswordHasher (PBKDF2) — same approach
/// phone-case-website's backend uses, without pulling in the rest of ASP.NET Core Identity.</summary>
public class PasswordHasherService : IPasswordHasher
{
    private readonly PasswordHasher<User> _hasher = new();

    public string Hash(string password) => _hasher.HashPassword(null!, password);

    public bool Verify(string hash, string password) =>
        _hasher.VerifyHashedPassword(null!, hash, password) != PasswordVerificationResult.Failed;
}
