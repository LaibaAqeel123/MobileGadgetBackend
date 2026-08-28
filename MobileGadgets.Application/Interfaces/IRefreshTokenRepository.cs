using MobileGadgets.Domain;

namespace MobileGadgets.Application.Interfaces;

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> GetByTokenHashAsync(string tokenHash);
    Task<List<RefreshToken>> GetActiveForUserAsync(int userId);
    Task AddAsync(RefreshToken token);
    Task SaveChangesAsync();
}
