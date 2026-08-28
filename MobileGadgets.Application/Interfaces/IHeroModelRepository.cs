using MobileGadgets.Domain;

namespace MobileGadgets.Application.Interfaces;

public interface IHeroModelRepository
{
    Task<List<HeroModel>> GetAllAsync();
    Task<HeroModel?> GetByIdAsync(int id);
    Task AddAsync(HeroModel heroModel);
    void Remove(HeroModel heroModel);
    Task SaveChangesAsync();
}
