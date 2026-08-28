using MobileGadgets.Application.Dtos;

namespace MobileGadgets.Application.Interfaces;

public interface IHeroGenerationService
{
    Task<List<HeroGenerationDto>> GetAllAsync();
    Task<HeroGenerationDto?> GetByIdAsync(int id);

    /// <summary>Runs the full generate flow: flattens + scene-warps the HeroModel's layers with
    /// the uploaded design, stores both the design and the full-resolution output permanently,
    /// and records a HeroGeneration. Throws KeyNotFoundException if heroModelId doesn't exist.</summary>
    Task<HeroGenerationDto> GenerateAsync(int heroModelId, Stream designContent, string designFileName);
}
