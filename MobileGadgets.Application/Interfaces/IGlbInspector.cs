using MobileGadgets.Application.Dtos;

namespace MobileGadgets.Application.Interfaces;

public interface IGlbInspector
{
    /// <summary>Opens a .glb and reports whether it can be used as a printable 3D case model:
    /// is it a real self-contained GLB, which material is the printable one, do that material's
    /// triangles have clean UVs inside 0..1, how evenly would a design stretch over them, and is the
    /// model a sensible phone size. Never throws for a bad file — a bad file is a report with
    /// errors, so the admin sees a reason instead of a server error.</summary>
    /// <param name="printMaterialName">Material to treat as printable; null means "PRINT_AREA".</param>
    GlbInspectionReport Inspect(Stream glb, string? printMaterialName = null);
}
