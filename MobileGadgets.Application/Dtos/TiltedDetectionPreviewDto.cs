namespace MobileGadgets.Application.Dtos;

/// <summary>What POST /heromodels/detect-tilted returns: the 3 detected mask images (already
/// saved, as URLs — same storage the final HeroModel will reference) plus the geometry, so the
/// admin screen can render an instant preview with a test design before deciding to save anything.
/// Saving is a separate step (Create/Update with PoseType=Tilted and these same values) — calling
/// this endpoint alone never creates or changes a HeroModel.</summary>
public class TiltedDetectionPreviewDto
{
    public string SilhouetteImageUrl { get; set; } = string.Empty;
    public string CameraMaskImageUrl { get; set; } = string.Empty;
    public string OverlayImageUrl { get; set; } = string.Empty;
    public string DesignQuadJson { get; set; } = string.Empty;
    public string CaseBboxJson { get; set; } = string.Empty;
}
