using MobileGadgets.Application.Dtos;

namespace MobileGadgets.Application.Interfaces;

public interface IHeroModelService
{
    Task<List<HeroModelDto>> GetAllAsync();
    Task<HeroModelDto?> GetByIdAsync(int id);
    Task<HeroModelDto> CreateAsync(CreateHeroModelRequest request);
    Task<HeroModelDto?> UpdateAsync(int id, UpdateHeroModelRequest request);
    Task<bool> DeleteAsync(int id);

    /// <summary>Runs TiltedDetectionService on an admin-uploaded photo and saves the 3 resulting
    /// mask images, returning their URLs plus the geometry — for the admin preview screen only.
    /// Does NOT create or modify a HeroModel; the admin still has to review the preview and call
    /// Create/Update (with PoseType=Tilted and these same values) to actually save it.
    /// cameraRoundR/cameraPadPx let the admin re-run detection with a nudged camera-area
    /// roundness/margin after seeing a first preview come out slightly off.</summary>
    Task<TiltedDetectionPreviewDto> DetectTiltedAsync(Stream photo, double? cameraRoundR = null, double? cameraPadPx = null);
}
