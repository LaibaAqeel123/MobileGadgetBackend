namespace MobileGadgets.Application.Dtos;

public class SceneDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public string BackgroundTopColor { get; set; } = string.Empty;
    public string BackgroundBottomColor { get; set; } = string.Empty;
}
