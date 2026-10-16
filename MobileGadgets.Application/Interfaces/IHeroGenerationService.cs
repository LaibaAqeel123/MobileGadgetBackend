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

    /// <summary>Records a picture a customer's browser rendered from a 3D model (Model3D): stores the
    /// finished PNG, the design they used, and any background photo they uploaded, and saves a
    /// HeroGeneration pointing at them — so a 3D picture has the same permanent link and history entry
    /// as a server-rendered one. Nothing is rendered here. sceneId null records the default Scene (the
    /// column is required; a 3D render does not use a Scene's pose). Every upload must really be an
    /// image, and the stored file's extension is decided from its content, never from the client's
    /// file name. Throws KeyNotFoundException if the model or scene does not exist, and
    /// InvalidOperationException if the model is not a 3D model or an upload is not an acceptable image.</summary>
    Task<HeroGenerationDto> SaveRenderedAsync(
        int heroModelId,
        Stream design,
        Stream output,
        int? sceneId,
        Stream? customBackground = null);
}
