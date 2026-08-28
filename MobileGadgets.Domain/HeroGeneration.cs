namespace MobileGadgets.Domain;

public class HeroGeneration
{
    public int Id { get; set; }
    public int HeroModelId { get; set; }
    public HeroModel HeroModel { get; set; } = null!;
    public int SceneId { get; set; }
    public Scene Scene { get; set; } = null!;
    public string DesignImageUrl { get; set; } = string.Empty;
    public string OutputImageUrl { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
