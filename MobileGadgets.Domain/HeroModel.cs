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

    public DateTime CreatedAt { get; set; }
}
