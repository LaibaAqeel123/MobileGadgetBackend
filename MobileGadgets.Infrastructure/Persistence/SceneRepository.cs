using Microsoft.EntityFrameworkCore;
using MobileGadgets.Application.Interfaces;
using MobileGadgets.Domain;

namespace MobileGadgets.Infrastructure.Persistence;

public class SceneRepository : ISceneRepository
{
    private readonly MobileGadgetsDbContext _db;

    public SceneRepository(MobileGadgetsDbContext db)
    {
        _db = db;
    }

    public async Task<List<Scene>> GetAllAsync() =>
        await _db.Scenes.OrderBy(s => s.Id).ToListAsync();

    public async Task<Scene?> GetByIdAsync(int id) =>
        await _db.Scenes.FindAsync(id);

    public async Task<Scene> GetDefaultAsync() =>
        await _db.Scenes.FirstAsync(s => s.IsDefault);

    public async Task AddAsync(Scene scene) =>
        await _db.Scenes.AddAsync(scene);

    public void Remove(Scene scene) =>
        _db.Scenes.Remove(scene);

    public async Task SaveChangesAsync() =>
        await _db.SaveChangesAsync();
}
