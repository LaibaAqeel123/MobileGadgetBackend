using MobileGadgets.Application.Dtos;

namespace MobileGadgets.Application.Interfaces;

public interface IHeroGenerationService
{
    Task<List<HeroGenerationDto>> GetAllAsync();
    Task<HeroGenerationDto?> GetByIdAsync(int id);

    /// <summary>Runs the full generate flow: flattens + scene-warps the HeroModel's layers with
    /// the uploaded design, stores both the design and the full-resolution output permanently,
    /// and records a HeroGeneration. sceneId null uses the default Scene. When customBackground
    /// is provided, it's stored and used as the background for this render instead of the
    /// selected Scene's own background (colour or preset photo) — the Scene still supplies pose.
    /// yawDegrees, when provided, overrides the selected Scene's own turn angle for this render
    /// only (clamped server-side to a pre-approved safe range — see HeroGenerationService); the
    /// Scene's stored angle is never modified. Throws KeyNotFoundException if heroModelId or
    /// sceneId doesn't exist.</summary>
    Task<HeroGenerationDto> GenerateAsync(
        int heroModelId,
        Stream designContent,
        string designFileName,
        int? sceneId,
        Stream? customBackground = null,
        string? customBackgroundFileName = null,
        double? yawDegrees = null);
}
