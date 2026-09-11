namespace MobileGadgets.Domain;

public class HeroModel
{
    public int Id { get; set; }
    public string PhoneName { get; set; } = string.Empty;
    public string CaseType { get; set; } = string.Empty;
    public HeroModelPoseType PoseType { get; set; } = HeroModelPoseType.Straight;

    /// <summary>Straight: the flat, front-on photo. Tilted: the real posed/angled photo, used
    /// unmodified both as the render source and as the model's default backdrop.</summary>
    public string BaseImageUrl { get; set; } = string.Empty;

    /// <summary>Straight: unused (see HERO_MODEL_ASSET_GUIDE.md — printable area is derived from
    /// BaseImageUrl's own alpha instead). Tilted: repurposed to hold the detected SILHOUETTE mask
    /// (white = case, black = background) — the equivalent of "BaseImageUrl's alpha" for a photo
    /// that has no real alpha channel (source is a JPEG).</summary>
    public string DesignMaskImageUrl { get; set; } = string.Empty;

    public string CameraMaskImageUrl { get; set; } = string.Empty;
    public string OverlayImageUrl { get; set; } = string.Empty;

    /// <summary>Tilted only: the 4 detected corners (TL/TR/BR/BL, pixel coordinates in
    /// BaseImageUrl's own space) that the customer's design gets homography-warped into.
    /// Serialized JSON, e.g. {"tl":{"x":1,"y":2},"tr":{...},"br":{...},"bl":{...}}. Null for
    /// Straight models.</summary>
    public string? DesignQuadJson { get; set; }

    /// <summary>Tilted only: the detected case body's bounding box in BaseImageUrl's own pixel
    /// space (JSON-serialized TiltedBbox). Needed at generation time only for the background-swap
    /// render tier, to size/position the cut-out case against the new backdrop. Null for Straight.</summary>
    public string? CaseBboxJson { get; set; }

    /// <summary>Tilted only: the camera area's outline as admin-editable points (superseded a
    /// roundness%/margin% slider pair — confirmed on real photos that no 2-number parametric
    /// family covers every real camera-bump shape across phone brands, e.g. a Samsung's tall
    /// vertical pill vs. an iPhone's rounded square). Auto-detection seeds this with an
    /// already-close guess; kept here (not just baked into CameraMaskImageUrl) so a future re-edit
    /// has the real points to continue from, not just a raster to re-trace. Serialized
    /// List&lt;TiltedPoint&gt;. Null for Straight.</summary>
    public string? CameraOutlinePointsJson { get; set; }

    /// <summary>Tilted only: admin-placed local protect/allow overrides beyond the main outline
    /// (see CameraSpot) — for a specific wrong spot that reshaping the whole outline can't fix
    /// cleanly (a stray uncaught hole; a piece of real body wrongly swallowed by the outline).
    /// Serialized List&lt;CameraSpot&gt;. Null/empty for Straight or when none are placed.</summary>
    public string? CameraSpotsJson { get; set; }

    public DateTime CreatedAt { get; set; }
}
