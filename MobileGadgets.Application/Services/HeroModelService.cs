using System.Text.Json;
using MobileGadgets.Application.Dtos;
using MobileGadgets.Application.Interfaces;
using MobileGadgets.Domain;

namespace MobileGadgets.Application.Services;

public class HeroModelService : IHeroModelService
{
    private readonly IHeroModelRepository _repository;
    private readonly IImageStorageService _imageStorage;
    private readonly IGlbInspector _glbInspector;

    public HeroModelService(IHeroModelRepository repository, IImageStorageService imageStorage, IGlbInspector glbInspector)
    {
        _repository = repository;
        _imageStorage = imageStorage;
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
            BaseImageUrl = request.BaseImageUrl,
            DesignMaskImageUrl = request.DesignMaskImageUrl,
            CameraMaskImageUrl = request.CameraMaskImageUrl,
            OverlayImageUrl = request.OverlayImageUrl,
            CreatedAt = DateTime.UtcNow,
        };
        // a model with a .glb is a 3D model: its file is checked again here and the layout comes from that check
        if (!string.IsNullOrWhiteSpace(request.GlbUrl))
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
        model.BaseImageUrl = request.BaseImageUrl;
        model.DesignMaskImageUrl = request.DesignMaskImageUrl;
        model.CameraMaskImageUrl = request.CameraMaskImageUrl;
        model.OverlayImageUrl = request.OverlayImageUrl;
        if (!string.IsNullOrWhiteSpace(request.GlbUrl))
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
        BaseImageUrl = model.BaseImageUrl,
        DesignMaskImageUrl = model.DesignMaskImageUrl,
        CameraMaskImageUrl = model.CameraMaskImageUrl,
        OverlayImageUrl = model.OverlayImageUrl,
        GlbUrl = model.GlbUrl,
        PrintMaterialName = model.PrintMaterialName,
        PrintLayoutJson = model.PrintLayoutJson,
        ValidationReportJson = model.ValidationReportJson,
        CreatedAt = model.CreatedAt,
    };
}
