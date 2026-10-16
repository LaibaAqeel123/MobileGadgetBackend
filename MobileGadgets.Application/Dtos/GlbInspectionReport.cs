namespace MobileGadgets.Application.Dtos;

/// <summary>What the upload check found in a .glb. <see cref="Ok"/> is false whenever there is at
/// least one entry in <see cref="Errors"/>; warnings never block saving, they only tell the admin
/// what to look at in the preview. Every message is written for the admin, not a developer.</summary>
public class GlbInspectionReport
{
    public bool Ok => Errors.Count == 0;
    public List<string> Errors { get; set; } = [];
    public List<string> Warnings { get; set; } = [];

    public int NodeCount { get; set; }
    public int MeshCount { get; set; }
    public int TriangleCount { get; set; }

    /// <summary>Every material in the file with how many triangles use it, so the admin can pick
    /// the printable one when the file has no material called PRINT_AREA.</summary>
    public List<GlbMaterialInfo> Materials { get; set; } = [];

    /// <summary>The material the checks below were run against (the requested one, else
    /// PRINT_AREA). Null when none could be resolved.</summary>
    public string? PrintMaterialName { get; set; }

    /// <summary>Rectangle the printable surface's UVs occupy inside the 0..1 texture square.
    /// Null when the printable material has no usable UVs.</summary>
    public GlbUvLayout? PrintLayout { get; set; }

    /// <summary>Longest side of the whole model, in the file's own units (glTF says metres). A
    /// phone is about 0.17 here; ~170 means the file was exported in millimetres.</summary>
    public double LongestSide { get; set; }
}

public class GlbMaterialInfo
{
    public string Name { get; set; } = string.Empty;
    public int TriangleCount { get; set; }
    public bool HasUvs { get; set; }
}

/// <summary>UV rectangle of the printable surface. <see cref="Aspect"/> is width/height of that
/// rectangle in UV units, which equals the surface's real width/height only when the texture is
/// square and mapped at one uniform scale (as every model we generate is).</summary>
public class GlbUvLayout
{
    public double U0 { get; set; }
    public double U1 { get; set; }
    public double V0 { get; set; }
    public double V1 { get; set; }
    public double Aspect => (V1 - V0) > 1e-9 ? (U1 - U0) / (V1 - V0) : 0;

    /// <summary>How unevenly the design would be stretched across the printable surface: the
    /// 95th and 5th percentile of (3D area / UV area) per triangle, as a ratio of the two. 1.0 is
    /// perfectly even; above about 2.5 a customer's picture will visibly warp somewhere.</summary>
    public double StretchSpread { get; set; }
}
