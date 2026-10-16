namespace MobileGadgets.Application.Dtos;

public class CreateHeroModelRequest
{
    public string PhoneName { get; set; } = string.Empty;
    public string CaseType { get; set; } = string.Empty;
    public string BaseImageUrl { get; set; } = string.Empty;
    public string DesignMaskImageUrl { get; set; } = string.Empty;
    public string CameraMaskImageUrl { get; set; } = string.Empty;
    public string OverlayImageUrl { get; set; } = string.Empty;

    /// <summary>3D models only: the values the 3D upload check (inspect-glb) returned, carried through to
    /// save. Setting GlbUrl is what makes this a 3D model; leave all four empty for a photo-based model.</summary>
    public string? GlbUrl { get; set; }
    public string? PrintMaterialName { get; set; }
    public string? PrintLayoutJson { get; set; }
    public string? ValidationReportJson { get; set; }
}
