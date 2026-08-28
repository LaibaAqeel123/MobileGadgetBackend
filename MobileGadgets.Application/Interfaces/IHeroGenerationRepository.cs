using MobileGadgets.Domain;

namespace MobileGadgets.Application.Interfaces;

public interface IHeroGenerationRepository
{
    Task<List<HeroGeneration>> GetAllAsync();
    Task<HeroGeneration?> GetByIdAsync(int id);
    Task AddAsync(HeroGeneration generation);
    Task SaveChangesAsync();
}
