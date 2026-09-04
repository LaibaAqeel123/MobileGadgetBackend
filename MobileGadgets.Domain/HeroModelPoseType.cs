namespace MobileGadgets.Domain;

/// <summary>How this model's BaseImageUrl was photographed, and therefore how it must be
/// rendered. Straight = flat, front-on photo; the renderer builds a synthetic 3D scene around
/// it (FlattenStage + SceneWarpStage). Tilted = a real posed/angled photo; the renderer warps
/// the design straight into the photo's own detected geometry and keeps the real backdrop/
/// lighting as-is (no synthetic scene).</summary>
public enum HeroModelPoseType
{
    Straight,
    Tilted,
}
