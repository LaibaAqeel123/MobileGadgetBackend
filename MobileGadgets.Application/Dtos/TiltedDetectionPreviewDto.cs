namespace MobileGadgets.Application.Dtos;

/// <summary>What POST /heromodels/detect-tilted returns: the 3 detected mask images (already
/// saved, as URLs — same storage the final HeroModel will reference) plus the geometry, so the
/// admin screen can render an instant preview with a test design before deciding to save anything.
/// Saving is a separate step (Create/Update with PoseType=Tilted and these same values) — calling
/// this endpoint alone never creates or changes a HeroModel.</summary>
public class TiltedDetectionPreviewDto
{
    /// <summary>The admin's original uploaded photo, saved once here so the frontend never has to
    /// upload it a second time to save the model or to request a rendered preview.</summary>
    public string BaseImageUrl { get; set; } = string.Empty;
    public string SilhouetteImageUrl { get; set; } = string.Empty;
    public string CameraMaskImageUrl { get; set; } = string.Empty;
    public string OverlayImageUrl { get; set; } = string.Empty;
    public string DesignQuadJson { get; set; } = string.Empty;
    public string CaseBboxJson { get; set; } = string.Empty;

    /// <summary>The camera area's editable outline, as real points — the admin's point editor
    /// works directly on this (drag/add/remove), not on CameraMaskImageUrl. Auto-detection seeds
    /// it with an already-close guess. Serialized List&lt;TiltedPoint&gt;.</summary>
    public string CameraOutlinePointsJson { get; set; } = string.Empty;

    /// <summary>Admin-placed local overrides beyond the main outline (protect/allow spots — see
    /// CameraSpot). Empty array ("[]") until the admin adds one. Serialized List&lt;CameraSpot&gt;.</summary>
    public string CameraSpotsJson { get; set; } = "[]";
}
