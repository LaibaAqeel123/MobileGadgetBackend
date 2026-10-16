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

    /// <summary>A real 3D model (a .glb of the phone wearing its case). Not photographed at all:
    /// the browser renders it from any angle and prints the customer's design onto the model's
    /// printable material (see HeroModel.GlbUrl / PrintMaterialName). None of the 2D image fields
    /// on HeroModel are used.</summary>
    Model3D,
}
