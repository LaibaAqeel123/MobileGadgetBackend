namespace MobileGadgets.Domain;

/// <summary>The wall/floor/camera setup for a hero shot. Not tied to any phone — one Scene
/// can be reused by every HeroModel, and the customer can pick between them at generate time.</summary>
public class Scene
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsDefault { get; set; }

    public double CamY { get; set; }
    public double CamZ { get; set; }
    public double PitchDegrees { get; set; }
    public double Focal { get; set; }
    public double LeanDegrees { get; set; }
    public double YawDegrees { get; set; }

    public string BackgroundTopColor { get; set; } = "#2c2c2f";
    public string BackgroundBottomColor { get; set; } = "#141416";
    public string FloorTopColor { get; set; } = "#333336";
    public string FloorBottomColor { get; set; } = "#111113";
    public string WallTopColor { get; set; } = "#3d3d40";
    public string WallBottomColor { get; set; } = "#28282b";

    /// <summary>When set, this Scene uses a real background photo instead of the gradient
    /// colours above: the renderer skips the synthetic floor/wall entirely (their perspective
    /// grid would clash with an arbitrary photo) and cover-fits this image as the backdrop,
    /// keeping the shadow/lighting/vignette/grain pass unchanged.</summary>
    public string? BackgroundImageUrl { get; set; }
}
