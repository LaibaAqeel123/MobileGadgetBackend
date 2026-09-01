namespace MobileGadgets.Application.Dtos;

/// <summary>Admin adds a new background preset by name + an uploaded photo (via /uploads/image,
/// same as HeroModel layer uploads). Camera/pose is fixed to the one approved default pose —
/// that's a geometry decision, not something admin needs to tune per background.</summary>
public class CreateSceneRequest
{
    public string Name { get; set; } = string.Empty;
    public string BackgroundImageUrl { get; set; } = string.Empty;
}
