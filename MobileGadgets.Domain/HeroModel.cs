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

    /// <summary>Tilted only, admin-tunable per model: the camera area's corner roundness and
    /// safety margin in pixels. Every real module's corner radius genuinely differs a little
    /// model to model — rather than require the automatic default to be pixel-perfect on every
    /// photo, the admin can nudge these two numbers after seeing the preview and re-run detection.
    /// Null uses TiltedDetectionService's own defaults.</summary>
    public double? CameraRoundR { get; set; }
    public double? CameraPadPx { get; set; }

    /// <summary>Tilted only: false (default) keeps the whole camera module area (lenses + the
    /// raised plate/trim around them) in its real photographed color — correct when that trim is
    /// a genuinely different material/color from the body. True prints the design across the
    /// plate too, excluding only the individual lens/flash holes — correct for a one-piece case
    /// molded in a single uniform color.</summary>
    public bool CoverCameraPlate { get; set; }

    public DateTime CreatedAt { get; set; }
}
