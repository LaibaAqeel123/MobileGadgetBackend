namespace MobileGadgets.Domain;

public class HeroGeneration
{
    public int Id { get; set; }
    public int HeroModelId { get; set; }
    public HeroModel HeroModel { get; set; } = null!;
    public int SceneId { get; set; }
    public Scene Scene { get; set; } = null!;
    public string DesignImageUrl { get; set; } = string.Empty;

    /// <summary>One-off background photo the customer uploaded for this generation, overriding
    /// the selected Scene's background (colour or preset photo) for this render only. Null when
    /// the customer used the Scene's own background instead.</summary>
    public string? CustomBackgroundImageUrl { get; set; }

    public string OutputImageUrl { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
