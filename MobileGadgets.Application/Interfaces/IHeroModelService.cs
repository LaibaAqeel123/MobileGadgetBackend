using MobileGadgets.Application.Dtos;

namespace MobileGadgets.Application.Interfaces;

public interface IHeroModelService
{
    Task<List<HeroModelDto>> GetAllAsync();
    Task<HeroModelDto?> GetByIdAsync(int id);
    Task<HeroModelDto> CreateAsync(CreateHeroModelRequest request);
    Task<HeroModelDto?> UpdateAsync(int id, UpdateHeroModelRequest request);
    Task<bool> DeleteAsync(int id);
}
