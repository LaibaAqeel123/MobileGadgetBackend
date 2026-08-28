using Microsoft.EntityFrameworkCore;
using MobileGadgets.Application.Interfaces;
using MobileGadgets.Domain;

namespace MobileGadgets.Infrastructure.Persistence;

public class HeroModelRepository : IHeroModelRepository
{
    private readonly MobileGadgetsDbContext _db;

    public HeroModelRepository(MobileGadgetsDbContext db)
    {
        _db = db;
    }

    public async Task<List<HeroModel>> GetAllAsync() =>
        await _db.HeroModels.OrderByDescending(m => m.CreatedAt).ToListAsync();

    public async Task<HeroModel?> GetByIdAsync(int id) =>
        await _db.HeroModels.FindAsync(id);

    public async Task AddAsync(HeroModel heroModel) =>
        await _db.HeroModels.AddAsync(heroModel);

    public void Remove(HeroModel heroModel) =>
        _db.HeroModels.Remove(heroModel);

    public async Task SaveChangesAsync() =>
        await _db.SaveChangesAsync();
}
