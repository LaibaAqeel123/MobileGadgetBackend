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

    /// <summary>Tilted only — the camera outline points/spots as last edited in the preview step.
    /// Null/ignored for Straight.</summary>
    public string? CameraOutlinePointsJson { get; set; }
    public string? CameraSpotsJson { get; set; }

    /// <summary>Model3D only — the values the 3D upload check (inspect-glb) returned, carried
    /// through to save. Null/ignored for Straight and Tilted.</summary>
    public string? GlbUrl { get; set; }
    public string? PrintMaterialName { get; set; }
    public string? PrintLayoutJson { get; set; }
    public string? ValidationReportJson { get; set; }
}
