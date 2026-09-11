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

    /// <summary>Tilted only — the case bbox from the same preview step. Null/ignored for Straight.</summary>
    public string? CaseBboxJson { get; set; }

    /// <summary>Tilted only — admin overrides carried through from the preview step, if the admin
    /// nudged them. Null uses TiltedDetectionService's own defaults on every future render.</summary>
    public double? CameraRoundR { get; set; }
    public double? CameraPadPx { get; set; }
    public bool CoverCameraPlate { get; set; }
}
