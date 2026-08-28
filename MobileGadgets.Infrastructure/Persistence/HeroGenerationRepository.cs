using Microsoft.EntityFrameworkCore;
using MobileGadgets.Application.Interfaces;
using MobileGadgets.Domain;

namespace MobileGadgets.Infrastructure.Persistence;

public class HeroGenerationRepository : IHeroGenerationRepository
{
    private readonly MobileGadgetsDbContext _db;

    public HeroGenerationRepository(MobileGadgetsDbContext db)
    {
        _db = db;
    }

    public async Task<List<HeroGeneration>> GetAllAsync() =>
        await _db.HeroGenerations.OrderByDescending(g => g.CreatedAt).ToListAsync();

    public async Task<HeroGeneration?> GetByIdAsync(int id) =>
        await _db.HeroGenerations.FindAsync(id);

    public async Task AddAsync(HeroGeneration generation) =>
        await _db.HeroGenerations.AddAsync(generation);

    public async Task SaveChangesAsync() =>
        await _db.SaveChangesAsync();
}
