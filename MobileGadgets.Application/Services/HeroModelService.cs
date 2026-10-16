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
    private readonly IGlbInspector _glbInspector;

    public HeroModelService(
        IHeroModelRepository repository,
        IImageStorageService imageStorage,
        ITiltedDetectionService tiltedDetection,
        ITiltedHeroImageRenderer tiltedRenderer,
        IGlbInspector glbInspector)
    {
        _repository = repository;
        _imageStorage = imageStorage;
        _tiltedDetection = tiltedDetection;
        _tiltedRenderer = tiltedRenderer;
        _glbInspector = glbInspector;
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
        if (request.PoseType == HeroModelPoseType.Model3D)
            ApplyModel3D(heroModel, request.GlbUrl, request.PrintMaterialName);

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
        if (request.PoseType == HeroModelPoseType.Model3D)
            ApplyModel3D(model, request.GlbUrl, request.PrintMaterialName);

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

        return await _tiltedRenderer.RenderAsync(
            baseStream, silhouetteStream, cameraMaskStream, overlayStream, sampleDesign, quad, bbox, preview.CameraSpotsJson);
    }

    public async Task<string> BakeCameraMaskAsync(string baseImageUrl, string cameraOutlinePointsJson, string cameraSpotsJson)
    {
        using var baseStream = _imageStorage.OpenRead(baseImageUrl);
        var maskPng = await _tiltedDetection.BakeCameraMaskAsync(baseStream, cameraOutlinePointsJson, cameraSpotsJson);
        await using var maskStream = new MemoryStream(maskPng);
        return await _imageStorage.SaveImageAsync(maskStream, "tilted-cameramask.png");
    }

    public async Task<GlbUploadResultDto> InspectGlbAsync(Stream glb, string? printMaterialName)
    {
        // Buffer once: the check reads the whole file, and it is also stored for previewing/saving.
        var buffer = new MemoryStream();
        await glb.CopyToAsync(buffer);
        buffer.Position = 0;
        // Always stored as .glb regardless of the uploaded name, so a mislabelled file cannot be
        // served back to browsers under an unexpected type.
        var url = await _imageStorage.SaveImageAsync(buffer, "model.glb");
        buffer.Position = 0;
        return ToUploadResult(url, _glbInspector.Inspect(buffer, printMaterialName));
    }

    public Task<GlbUploadResultDto> ReinspectGlbAsync(string glbUrl, string? printMaterialName)
    {
        RequireStoredGlbUrl(glbUrl);
        using var stream = _imageStorage.OpenRead(glbUrl);
        return Task.FromResult(ToUploadResult(glbUrl, _glbInspector.Inspect(stream, printMaterialName)));
    }

    /// <summary>The 3D fields are never trusted from the client: the stored file is checked again
    /// here and the layout/report are taken from that fresh check, so a hand-edited request cannot
    /// save a broken model or a made-up print layout.</summary>
    private void ApplyModel3D(HeroModel model, string? glbUrl, string? printMaterialName)
    {
        if (string.IsNullOrWhiteSpace(glbUrl))
            throw new InvalidOperationException("A 3D model needs an uploaded .glb file.");
        RequireStoredGlbUrl(glbUrl);

        GlbInspectionReport report;
        using (var stream = _imageStorage.OpenRead(glbUrl))
            report = _glbInspector.Inspect(stream, printMaterialName);
        if (!report.Ok)
            throw new InvalidOperationException("This 3D model cannot be saved: " + string.Join(" ", report.Errors));

        model.GlbUrl = glbUrl;
        model.PrintMaterialName = report.PrintMaterialName;
        model.PrintLayoutJson = LayoutJson(report);
        model.ValidationReportJson = JsonSerializer.Serialize(report, ReportJsonOptions);
    }

    // camelCase, the same as every API response, so the admin page can read a stored report exactly
    // like a fresh one
    private static readonly JsonSerializerOptions ReportJsonOptions = new(JsonSerializerDefaults.Web);

    private static GlbUploadResultDto ToUploadResult(string url, GlbInspectionReport report) => new()
    {
        GlbUrl = url,
        Report = report,
        PrintMaterialName = report.PrintMaterialName,
        PrintLayoutJson = report.Ok ? LayoutJson(report) : null,
    };

    private static string? LayoutJson(GlbInspectionReport report) =>
        report.PrintLayout is { } l
            ? JsonSerializer.Serialize(new { u0 = l.U0, u1 = l.U1, v0 = l.V0, v1 = l.V1 })
            : null;

    /// <summary>Only a file this system stored itself may be opened: a "/uploads/" path ending in a
    /// GUID and ".glb". Stops a request from pointing the reader at some other path on the server.</summary>
    private static void RequireStoredGlbUrl(string url)
    {
        var ok = System.Text.RegularExpressions.Regex.IsMatch(
            url, @"^/uploads/[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}.glb$");
        if (!ok) throw new InvalidOperationException("Not a stored 3D model file.");
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
        GlbUrl = model.GlbUrl,
        PrintMaterialName = model.PrintMaterialName,
        PrintLayoutJson = model.PrintLayoutJson,
        ValidationReportJson = model.ValidationReportJson,
        CreatedAt = model.CreatedAt,
    };
}
