using MobileGadgets.Application.Dtos;

namespace MobileGadgets.Application.Interfaces;

public interface IHeroModelService
{
    Task<List<HeroModelDto>> GetAllAsync();
    Task<HeroModelDto?> GetByIdAsync(int id);
    Task<HeroModelDto> CreateAsync(CreateHeroModelRequest request);
    Task<HeroModelDto?> UpdateAsync(int id, UpdateHeroModelRequest request);
    Task<bool> DeleteAsync(int id);

    /// <summary>Model3D, step 1 of 2: stores an uploaded .glb and runs the upload check on it.
    /// Saves nothing to the database — the admin reviews the report, then calls Create/Update with
    /// PoseType=Model3D and the returned GlbUrl/PrintMaterialName. The file is kept even when the
    /// report has errors, so the admin can re-check it with a different printable material
    /// (see <see cref="ReinspectGlbAsync"/>) without uploading it again.</summary>
    Task<GlbUploadResultDto> InspectGlbAsync(Stream glb, string? printMaterialName);

    /// <summary>Re-runs the upload check on an already-stored .glb, optionally against a different
    /// printable material (the admin's pick from the material list).</summary>
    Task<GlbUploadResultDto> ReinspectGlbAsync(string glbUrl, string? printMaterialName);

    /// <summary>Runs TiltedDetectionService on an admin-uploaded photo and saves the resulting
    /// mask images plus a starting camera OUTLINE (real editable points, not just a raster —
    /// see TiltedDetectionResult.CameraOutlinePoints), for the admin preview screen only. Does NOT
    /// create or modify a HeroModel; the admin still has to review the preview and call
    /// Create/Update (with PoseType=Tilted and these same values) to actually save it.</summary>
    Task<TiltedDetectionPreviewDto> DetectTiltedAsync(Stream photo);

    /// <summary>Renders one test composite from an in-progress (not-yet-saved) Tilted detection
    /// preview, so the admin can see an actual "design applied to the case" image — not just the
    /// raw mask thumbnails — before deciding whether to save. Reads the already-saved base photo +
    /// masks by URL (from a prior DetectTiltedAsync or BakeCameraMaskAsync call); saves nothing new
    /// itself except the rendered preview image.</summary>
    Task<byte[]> PreviewTiltedRenderAsync(TiltedDetectionPreviewDto preview, Stream sampleDesign);

    /// <summary>Rasterizes a fresh camera-mask PNG directly from admin-edited outline points +
    /// spot overrides (see Geometry2D.RasterizeSmoothShapeMask) and saves it — the FAST path the
    /// point editor calls after every edit, deliberately skipping TiltedDetectionService entirely
    /// (no silhouette detection, no corner fitting, no lighting map — just rasterize the shape the
    /// admin just drew) so adjusting the camera area is a couple seconds, not the ~30s a full
    /// re-detect costs. Returns the new mask's URL, which the caller then passes to
    /// PreviewTiltedRenderAsync to actually see the design applied, and is what finally gets saved
    /// as the HeroModel's CameraMaskImageUrl.</summary>
    Task<string> BakeCameraMaskAsync(string baseImageUrl, string cameraOutlinePointsJson, string cameraSpotsJson);
}
