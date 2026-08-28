using Microsoft.EntityFrameworkCore;
using MobileGadgets.Application.Interfaces;
using MobileGadgets.Domain;

namespace MobileGadgets.Infrastructure.Persistence;

public class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly MobileGadgetsDbContext _db;

    public RefreshTokenRepository(MobileGadgetsDbContext db)
    {
        _db = db;
    }

    public async Task<RefreshToken?> GetByTokenHashAsync(string tokenHash) =>
        await _db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash);

    public async Task<List<RefreshToken>> GetActiveForUserAsync(int userId) =>
        await _db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null && t.ExpiresAt > DateTime.UtcNow)
            .ToListAsync();

    public async Task AddAsync(RefreshToken token) =>
        await _db.RefreshTokens.AddAsync(token);

    public async Task SaveChangesAsync() =>
        await _db.SaveChangesAsync();
}
