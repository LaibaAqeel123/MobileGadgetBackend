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

    // A finished 3D picture is 2048x2048; even a busy one stays far below this. The design is the
    // customer's original file, so it gets more room.
    private const int MaxRenderedBytes = 25_000_000;
    private const int MaxUploadBytes = 40_000_000;

    public async Task<HeroGenerationDto> SaveRenderedAsync(
        int heroModelId, Stream design, Stream output, int? sceneId, Stream? customBackground = null)
    {
        var model = await _heroModelRepository.GetByIdAsync(heroModelId)
            ?? throw new KeyNotFoundException($"HeroModel {heroModelId} not found.");
        if (string.IsNullOrWhiteSpace(model.GlbUrl))
            throw new InvalidOperationException("Only 3D models are saved this way; other models are rendered on the server.");

        var scene = sceneId is null
            ? await _sceneRepository.GetDefaultAsync()
            : await _sceneRepository.GetByIdAsync(sceneId.Value) ?? throw new KeyNotFoundException($"Scene {sceneId} not found.");

        // Check everything before storing anything, so a bad upload leaves no files behind.
        var designImage = await ReadImageAsync(design, "design", MaxUploadBytes, pngOnly: false);
        var outputImage = await ReadImageAsync(output, "finished picture", MaxRenderedBytes, pngOnly: true);
        (byte[] Data, string Extension)? backgroundImage = null;
        if (customBackground is not null)
            backgroundImage = await ReadImageAsync(customBackground, "background", MaxUploadBytes, pngOnly: false);

        var stored = new List<string>();
        try
        {
            var designUrl = await StoreAsync(designImage, "design", stored);
            var backgroundUrl = backgroundImage is null ? null : await StoreAsync(backgroundImage.Value, "background", stored);
            var outputUrl = await StoreAsync(outputImage, "hero", stored);

            var generation = new HeroGeneration
            {
                HeroModelId = heroModelId,
                SceneId = scene.Id,
                DesignImageUrl = designUrl,
                CustomBackgroundImageUrl = backgroundUrl,
                OutputImageUrl = outputUrl,
                CreatedAt = DateTime.UtcNow,
            };
            await _generationRepository.AddAsync(generation);
            await _generationRepository.SaveChangesAsync();
            return ToDto(generation);
        }
        catch
        {
            // Do not leave files that no record points at.
            foreach (var url in stored) _imageStorage.DeleteFile(url);
            throw;
        }
    }

    private async Task<string> StoreAsync((byte[] Data, string Extension) image, string name, List<string> stored)
    {
        await using var stream = new MemoryStream(image.Data);
        var url = await _imageStorage.SaveImageAsync(stream, name + image.Extension);
        stored.Add(url);
        return url;
    }

    /// <summary>Reads an upload fully (up to a size limit) and works out what it really is from its
    /// first bytes. Only PNG, JPEG, WebP and GIF are accepted; the returned extension comes from the
    /// content, so a file called "x.html" or "x.png" cannot be stored as something it is not.</summary>
    private static async Task<(byte[] Data, string Extension)> ReadImageAsync(Stream stream, string what, int maxBytes, bool pngOnly)
    {
        var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(chunk)) > 0)
        {
            buffer.Write(chunk, 0, read);
            if (buffer.Length > maxBytes)
                throw new InvalidOperationException($"The {what} is too large (limit {maxBytes / 1_000_000} MB).");
        }

        var data = buffer.ToArray();
        var extension = DetectImageExtension(data);
        if (extension is null)
            throw new InvalidOperationException($"The {what} is not a supported image (use PNG, JPEG or WebP).");
        if (pngOnly && extension != ".png")
            throw new InvalidOperationException($"The {what} must be a PNG.");
        return (data, extension);
    }

    private static string? DetectImageExtension(byte[] d)
    {
        if (d.Length >= 8 && d[0] == 0x89 && d[1] == 0x50 && d[2] == 0x4E && d[3] == 0x47 && d[4] == 0x0D && d[5] == 0x0A && d[6] == 0x1A && d[7] == 0x0A) return ".png";
        if (d.Length >= 3 && d[0] == 0xFF && d[1] == 0xD8 && d[2] == 0xFF) return ".jpg";
        if (d.Length >= 12 && d[0] == 'R' && d[1] == 'I' && d[2] == 'F' && d[3] == 'F' && d[8] == 'W' && d[9] == 'E' && d[10] == 'B' && d[11] == 'P') return ".webp";
        if (d.Length >= 6 && d[0] == 'G' && d[1] == 'I' && d[2] == 'F' && d[3] == '8') return ".gif";
        return null;
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

        // A 3D model has no photos for this server-side renderer to work from: it is rendered in the
        // customer's browser from its .glb. Refuse clearly instead of failing on its empty image fields.
        if (!string.IsNullOrWhiteSpace(model.GlbUrl))
            throw new InvalidOperationException("3D models are rendered in the browser, not by this endpoint.");

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
