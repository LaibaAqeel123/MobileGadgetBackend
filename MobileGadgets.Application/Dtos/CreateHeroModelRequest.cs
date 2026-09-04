using MobileGadgets.Domain;

namespace MobileGadgets.Application.Dtos;

public class CreateHeroModelRequest
{
    public string PhoneName { get; set; } = string.Empty;
    public string CaseType { get; set; } = string.Empty;
    public HeroModelPoseType PoseType { get; set; } = HeroModelPoseType.Straight;
    public string BaseImageUrl { get; set; } = string.Empty;
    public string DesignMaskImageUrl { get; set; } = string.Empty;
    public string CameraMaskImageUrl { get; set; } = string.Empty;
    public string OverlayImageUrl { get; set; } = string.Empty;

    /// <summary>Tilted only — the quad detected by the auto-detect preview step, carried
    /// through to save. Null/ignored for Straight.</summary>
    public string? DesignQuadJson { get; set; }
}
