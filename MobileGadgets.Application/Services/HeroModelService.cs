using MobileGadgets.Application.Dtos;
using MobileGadgets.Application.Interfaces;
using MobileGadgets.Domain;

namespace MobileGadgets.Application.Services;

public class HeroModelService : IHeroModelService
{
    private readonly IHeroModelRepository _repository;
    private readonly IImageStorageService _imageStorage;
    private readonly ITiltedDetectionService _tiltedDetection;

    public HeroModelService(
        IHeroModelRepository repository,
        IImageStorageService imageStorage,
        ITiltedDetectionService tiltedDetection)
    {
        _repository = repository;
        _imageStorage = imageStorage;
        _tiltedDetection = tiltedDetection;
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
            CameraRoundR = request.CameraRoundR,
            CameraPadPx = request.CameraPadPx,
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
        model.CameraRoundR = request.CameraRoundR;
        model.CameraPadPx = request.CameraPadPx;

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

    public async Task<TiltedDetectionPreviewDto> DetectTiltedAsync(Stream photo, double? cameraRoundR = null, double? cameraPadPx = null)
    {
        var result = cameraRoundR is null && cameraPadPx is null
            ? await _tiltedDetection.DetectAsync(photo)
            : await _tiltedDetection.DetectAsync(photo, cameraRoundR ?? 18, cameraPadPx ?? 20);

        await using var silhouetteStream = new MemoryStream(result.SilhouettePng);
        await using var cameraMaskStream = new MemoryStream(result.CameraMaskPng);
        await using var overlayStream = new MemoryStream(result.OverlayPng);

        var silhouetteUrl = await _imageStorage.SaveImageAsync(silhouetteStream, "tilted-silhouette.png");
        var cameraMaskUrl = await _imageStorage.SaveImageAsync(cameraMaskStream, "tilted-cameramask.png");
        var overlayUrl = await _imageStorage.SaveImageAsync(overlayStream, "tilted-overlay.png");

        return new TiltedDetectionPreviewDto
        {
            SilhouetteImageUrl = silhouetteUrl,
            CameraMaskImageUrl = cameraMaskUrl,
            OverlayImageUrl = overlayUrl,
            DesignQuadJson = System.Text.Json.JsonSerializer.Serialize(result.Quad),
            CaseBboxJson = System.Text.Json.JsonSerializer.Serialize(result.Bbox),
        };
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
        CameraRoundR = model.CameraRoundR,
        CameraPadPx = model.CameraPadPx,
        CreatedAt = model.CreatedAt,
    };
}
