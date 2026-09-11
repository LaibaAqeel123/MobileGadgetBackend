using MobileGadgets.Domain;

namespace MobileGadgets.Application.Dtos;

public class HeroModelDto
{
    public int Id { get; set; }
    public string PhoneName { get; set; } = string.Empty;
    public string CaseType { get; set; } = string.Empty;
    public HeroModelPoseType PoseType { get; set; } = HeroModelPoseType.Straight;
    public string BaseImageUrl { get; set; } = string.Empty;
    public string DesignMaskImageUrl { get; set; } = string.Empty;
    public string CameraMaskImageUrl { get; set; } = string.Empty;
    public string OverlayImageUrl { get; set; } = string.Empty;
    public string? DesignQuadJson { get; set; }
    public string? CaseBboxJson { get; set; }
    public string? CameraOutlinePointsJson { get; set; }
    public string? CameraSpotsJson { get; set; }
    public DateTime CreatedAt { get; set; }
}
