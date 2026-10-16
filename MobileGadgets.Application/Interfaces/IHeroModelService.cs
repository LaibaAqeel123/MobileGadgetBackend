using MobileGadgets.Application.Dtos;

namespace MobileGadgets.Application.Interfaces;

public interface IHeroModelService
{
    Task<List<HeroModelDto>> GetAllAsync();
    Task<HeroModelDto?> GetByIdAsync(int id);
    Task<HeroModelDto> CreateAsync(CreateHeroModelRequest request);
    Task<HeroModelDto?> UpdateAsync(int id, UpdateHeroModelRequest request);
    Task<bool> DeleteAsync(int id);

    /// <summary>3D models, step 1 of 2: stores an uploaded .glb and runs the upload check on it. Saves nothing
    /// to the database — the admin reviews the report, then calls Create/Update with the returned
    /// GlbUrl/PrintMaterialName. The file is kept even when the report has errors, so the admin can re-check it
    /// with a different printable material (see <see cref="ReinspectGlbAsync"/>) without uploading it again.</summary>
    Task<GlbUploadResultDto> InspectGlbAsync(Stream glb, string? printMaterialName);

    /// <summary>Re-runs the upload check on an already-stored .glb, optionally against a different
    /// printable material (the admin's pick from the material list).</summary>
    Task<GlbUploadResultDto> ReinspectGlbAsync(string glbUrl, string? printMaterialName);
}
