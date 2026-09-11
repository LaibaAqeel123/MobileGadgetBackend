using System.Text.Json;
using MobileGadgets.Application.Dtos;
using MobileGadgets.Application.Interfaces;
using MobileGadgets.Domain;

namespace MobileGadgets.Application.Services;

public class HeroModelService : IHeroModelService
{
    private readonly IHeroModelRepository _repository;
    private readonly IImageStorageService _imageStorage;
    private readonly ITiltedDetectionService _tiltedDetection;
    private readonly ITiltedHeroImageRenderer _tiltedRenderer;

    public HeroModelService(
        IHeroModelRepository repository,
        IImageStorageService imageStorage,
        ITiltedDetectionService tiltedDetection,
        ITiltedHeroImageRenderer tiltedRenderer)
    {
        _repository = repository;
        _imageStorage = imageStorage;
        _tiltedDetection = tiltedDetection;
        _tiltedRenderer = tiltedRenderer;
    }

    public async Task<List<HeroModelDto>> GetAllAsync()
    {
        var models = await _repository.GetAllAsync();
        return models.Select(ToDto).ToList();
    }

    public async Task<HeroModelDto?> GetByIdAsync(int id)
    {
        var model = await _repository.GetByIdAsync(id);
        return model is null ? null : ToDto(model);
    }

    public async Task<HeroModelDto> CreateAsync(CreateHeroModelRequest request)
    {
        var heroModel = new HeroModel
        {
            PhoneName = request.PhoneName,
            CaseType = request.CaseType,
            PoseType = request.PoseType,
            BaseImageUrl = request.BaseImageUrl,
            DesignMaskImageUrl = request.DesignMaskImageUrl,
            CameraMaskImageUrl = request.CameraMaskImageUrl,
            OverlayImageUrl = request.OverlayImageUrl,
            DesignQuadJson = request.DesignQuadJson,
            CaseBboxJson = request.CaseBboxJson,
            CameraOutlinePointsJson = request.CameraOutlinePointsJson,
            CameraSpotsJson = request.CameraSpotsJson,
            CreatedAt = DateTime.UtcNow,
        };

        await _repository.AddAsync(heroModel);
        await _repository.SaveChangesAsync();

        return ToDto(heroModel);
    }

    public async Task<HeroModelDto?> UpdateAsync(int id, UpdateHeroModelRequest request)
    {
        var model = await _repository.GetByIdAsync(id);
        if (model is null) return null;

        model.PhoneName = request.PhoneName;
        model.CaseType = request.CaseType;
        model.PoseType = request.PoseType;
        model.BaseImageUrl = request.BaseImageUrl;
        model.DesignMaskImageUrl = request.DesignMaskImageUrl;
        model.CameraMaskImageUrl = request.CameraMaskImageUrl;
        model.OverlayImageUrl = request.OverlayImageUrl;
        model.DesignQuadJson = request.DesignQuadJson;
        model.CaseBboxJson = request.CaseBboxJson;
        model.CameraOutlinePointsJson = request.CameraOutlinePointsJson;
        model.CameraSpotsJson = request.CameraSpotsJson;

        await _repository.SaveChangesAsync();
        return ToDto(model);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var model = await _repository.GetByIdAsync(id);
        if (model is null) return false;

        _repository.Remove(model);
        await _repository.SaveChangesAsync();
        return true;
    }

    public async Task<TiltedDetectionPreviewDto> DetectTiltedAsync(Stream photo)
    {
        // Buffer once: TiltedDetectionService needs to read the photo for detection, and it's
        // saved as BaseImageUrl separately so the frontend never has to upload it twice.
        var photoBuffer = new MemoryStream();
        await photo.CopyToAsync(photoBuffer);
        photoBuffer.Position = 0;
        var baseImageUrl = await _imageStorage.SaveImageAsync(photoBuffer, "tilted-base.png");
        photoBuffer.Position = 0;

        var result = await _tiltedDetection.DetectAsync(photoBuffer);

        await using var silhouetteStream = new MemoryStream(result.SilhouettePng);
        await using var cameraMaskStream = new MemoryStream(result.CameraMaskPng);
        await using var overlayStream = new MemoryStream(result.OverlayPng);

        var silhouetteUrl = await _imageStorage.SaveImageAsync(silhouetteStream, "tilted-silhouette.png");
        var cameraMaskUrl = await _imageStorage.SaveImageAsync(cameraMaskStream, "tilted-cameramask.png");
        var overlayUrl = await _imageStorage.SaveImageAsync(overlayStream, "tilted-overlay.png");

        return new TiltedDetectionPreviewDto
        {
            BaseImageUrl = baseImageUrl,
            SilhouetteImageUrl = silhouetteUrl,
            CameraMaskImageUrl = cameraMaskUrl,
            OverlayImageUrl = overlayUrl,
            DesignQuadJson = JsonSerializer.Serialize(result.Quad),
            CaseBboxJson = JsonSerializer.Serialize(result.Bbox),
            CameraOutlinePointsJson = JsonSerializer.Serialize(result.CameraOutlinePoints),
            CameraSpotsJson = "[]",
        };
    }

    public async Task<byte[]> PreviewTiltedRenderAsync(TiltedDetectionPreviewDto preview, Stream sampleDesign)
    {
        using var baseStream = _imageStorage.OpenRead(preview.BaseImageUrl);
        using var silhouetteStream = _imageStorage.OpenRead(preview.SilhouetteImageUrl);
        using var cameraMaskStream = _imageStorage.OpenRead(preview.CameraMaskImageUrl);
        using var overlayStream = _imageStorage.OpenRead(preview.OverlayImageUrl);

        var quad = JsonSerializer.Deserialize<TiltedQuad>(preview.DesignQuadJson);
        var bbox = JsonSerializer.Deserialize<TiltedBbox>(preview.CaseBboxJson);

        return await _tiltedRenderer.RenderAsync(baseStream, silhouetteStream, cameraMaskStream, overlayStream, sampleDesign, quad, bbox);
    }

    public async Task<string> BakeCameraMaskAsync(string baseImageUrl, string cameraOutlinePointsJson, string cameraSpotsJson)
    {
        using var baseStream = _imageStorage.OpenRead(baseImageUrl);
        var maskPng = await _tiltedDetection.BakeCameraMaskAsync(baseStream, cameraOutlinePointsJson, cameraSpotsJson);
        await using var maskStream = new MemoryStream(maskPng);
        return await _imageStorage.SaveImageAsync(maskStream, "tilted-cameramask.png");
    }

    private static HeroModelDto ToDto(HeroModel model) => new()
    {
        Id = model.Id,
        PhoneName = model.PhoneName,
        CaseType = model.CaseType,
        PoseType = model.PoseType,
        BaseImageUrl = model.BaseImageUrl,
        DesignMaskImageUrl = model.DesignMaskImageUrl,
        CameraMaskImageUrl = model.CameraMaskImageUrl,
        OverlayImageUrl = model.OverlayImageUrl,
        DesignQuadJson = model.DesignQuadJson,
        CaseBboxJson = model.CaseBboxJson,
        CameraOutlinePointsJson = model.CameraOutlinePointsJson,
        CameraSpotsJson = model.CameraSpotsJson,
        CreatedAt = model.CreatedAt,
    };
}
