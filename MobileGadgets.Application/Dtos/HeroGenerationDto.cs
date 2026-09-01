namespace MobileGadgets.Application.Dtos;

public class HeroGenerationDto
{
    public int Id { get; set; }
    public int HeroModelId { get; set; }
    public int SceneId { get; set; }
    public string DesignImageUrl { get; set; } = string.Empty;
    public string? CustomBackgroundImageUrl { get; set; }
    public string OutputImageUrl { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
