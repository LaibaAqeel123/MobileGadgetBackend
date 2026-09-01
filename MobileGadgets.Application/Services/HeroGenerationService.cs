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

    public async Task<HeroGenerationDto> GenerateAsync(
        int heroModelId,
        Stream designContent,
        string designFileName,
        int? sceneId,
        Stream? customBackground = null,
        string? customBackgroundFileName = null)
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

        var png = await _renderer.RenderAsync(baseStream, cameraMaskStream, overlayStream, designBuffer, scene, backgroundStream);
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
