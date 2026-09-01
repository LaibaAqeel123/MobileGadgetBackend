using MobileGadgets.Application.Dtos;
using MobileGadgets.Application.Interfaces;
using MobileGadgets.Domain;

namespace MobileGadgets.Application.Services;

public class HeroGenerationService : IHeroGenerationService
{
    private readonly IHeroGenerationRepository _generationRepository;
    private readonly IHeroModelRepository _heroModelRepository;
    private readonly ISceneRepository _sceneRepository;
    private readonly IImageStorageService _imageStorage;
    private readonly IHeroImageRenderer _renderer;

    public HeroGenerationService(
        IHeroGenerationRepository generationRepository,
        IHeroModelRepository heroModelRepository,
        ISceneRepository sceneRepository,
        IImageStorageService imageStorage,
        IHeroImageRenderer renderer)
    {
        _generationRepository = generationRepository;
        _heroModelRepository = heroModelRepository;
        _sceneRepository = sceneRepository;
        _imageStorage = imageStorage;
        _renderer = renderer;
    }

    public async Task<List<HeroGenerationDto>> GetAllAsync()
    {
        var generations = await _generationRepository.GetAllAsync();
        return generations.Select(ToDto).ToList();
    }

    public async Task<HeroGenerationDto?> GetByIdAsync(int id)
    {
        var generation = await _generationRepository.GetByIdAsync(id);
        return generation is null ? null : ToDto(generation);
    }

    // A wider turn was tried during development and rejected — it exposed a shadow/base gap at
    // the phone's far bottom corner. +/-12 degrees stays well inside the range confirmed clean.
    private const double MaxYawDegrees = 12;

    public async Task<HeroGenerationDto> GenerateAsync(
        int heroModelId,
        Stream designContent,
        string designFileName,
        int? sceneId,
        Stream? customBackground = null,
        string? customBackgroundFileName = null,
        double? yawDegrees = null)
    {
        var model = await _heroModelRepository.GetByIdAsync(heroModelId)
            ?? throw new KeyNotFoundException($"HeroModel {heroModelId} not found.");

        var scene = sceneId is null
            ? await _sceneRepository.GetDefaultAsync()
            : await _sceneRepository.GetByIdAsync(sceneId.Value) ?? throw new KeyNotFoundException($"Scene {sceneId} not found.");

        // Buffer the design once so it can be saved permanently AND fed to the renderer.
        var designBuffer = new MemoryStream();
        await designContent.CopyToAsync(designBuffer);
        designBuffer.Position = 0;
        var designUrl = await _imageStorage.SaveImageAsync(designBuffer, designFileName);
        designBuffer.Position = 0;

        // A customer-uploaded background overrides the Scene's own background (colour or preset
        // photo) for this render only, same buffer-then-save-then-rewind pattern as the design.
        string? customBackgroundUrl = null;
        MemoryStream? customBackgroundBuffer = null;
        if (customBackground is not null)
        {
            customBackgroundBuffer = new MemoryStream();
            await customBackground.CopyToAsync(customBackgroundBuffer);
            customBackgroundBuffer.Position = 0;
            customBackgroundUrl = await _imageStorage.SaveImageAsync(customBackgroundBuffer, customBackgroundFileName ?? "background.png");
            customBackgroundBuffer.Position = 0;
        }

        using var baseStream = _imageStorage.OpenRead(model.BaseImageUrl);
        using var cameraMaskStream = _imageStorage.OpenRead(model.CameraMaskImageUrl);
        using var overlayStream = _imageStorage.OpenRead(model.OverlayImageUrl);

        // No custom upload: fall back to the Scene's own preset photo, if it has one.
        Stream? backgroundStream = customBackgroundBuffer;
        using var presetBackgroundStream = backgroundStream is null && scene.BackgroundImageUrl is not null
            ? _imageStorage.OpenRead(scene.BackgroundImageUrl)
            : null;
        backgroundStream ??= presetBackgroundStream;

        // A separate, untracked copy carries the angle override into the renderer — mutating
        // `scene` directly would risk EF Core persisting the override back into that Scene's row
        // for every future generation, since it's still a tracked entity on this DbContext.
        var renderScene = yawDegrees is null ? scene : CloneWithYaw(scene, Math.Clamp(yawDegrees.Value, -MaxYawDegrees, MaxYawDegrees));

        var png = await _renderer.RenderAsync(baseStream, cameraMaskStream, overlayStream, designBuffer, renderScene, backgroundStream);
        await (customBackgroundBuffer?.DisposeAsync() ?? ValueTask.CompletedTask);

        await using var outputStream = new MemoryStream(png);
        var outputUrl = await _imageStorage.SaveImageAsync(outputStream, "hero.png");

        var generation = new HeroGeneration
        {
            HeroModelId = heroModelId,
            SceneId = scene.Id,
            DesignImageUrl = designUrl,
            CustomBackgroundImageUrl = customBackgroundUrl,
            OutputImageUrl = outputUrl,
            CreatedAt = DateTime.UtcNow,
        };

        await _generationRepository.AddAsync(generation);
        await _generationRepository.SaveChangesAsync();

        return ToDto(generation);
    }

    private static Scene CloneWithYaw(Scene scene, double yawDegrees) => new()
    {
        Id = scene.Id,
        Name = scene.Name,
        IsDefault = scene.IsDefault,
        CamY = scene.CamY,
        CamZ = scene.CamZ,
        PitchDegrees = scene.PitchDegrees,
        Focal = scene.Focal,
        LeanDegrees = scene.LeanDegrees,
        YawDegrees = yawDegrees,
        BackgroundTopColor = scene.BackgroundTopColor,
        BackgroundBottomColor = scene.BackgroundBottomColor,
        FloorTopColor = scene.FloorTopColor,
        FloorBottomColor = scene.FloorBottomColor,
        WallTopColor = scene.WallTopColor,
        WallBottomColor = scene.WallBottomColor,
        BackgroundImageUrl = scene.BackgroundImageUrl,
    };

    private static HeroGenerationDto ToDto(HeroGeneration g) => new()
    {
        Id = g.Id,
        HeroModelId = g.HeroModelId,
        SceneId = g.SceneId,
        DesignImageUrl = g.DesignImageUrl,
        CustomBackgroundImageUrl = g.CustomBackgroundImageUrl,
        OutputImageUrl = g.OutputImageUrl,
        CreatedAt = g.CreatedAt,
    };
}
