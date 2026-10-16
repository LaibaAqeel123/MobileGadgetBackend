namespace MobileGadgets.Application.Dtos;

/// <summary>Result of the admin's "upload a 3D model" step. Nothing is saved to the database yet —
/// this only stores the file and reports on it. The admin reviews the report (and, if needed,
/// picks a different printable material), then saves with Create/Update using these same values.</summary>
public class GlbUploadResultDto
{
    /// <summary>Public URL of the stored .glb, used both to preview it and to save the model.</summary>
    public string GlbUrl { get; set; } = string.Empty;

    public GlbInspectionReport Report { get; set; } = new();

    /// <summary>The printable material the report was run against; null when none was resolved.</summary>
    public string? PrintMaterialName { get; set; }

    /// <summary>Measured UV rectangle as JSON {"u0","u1","v0","v1"}; null when the report has errors.</summary>
    public string? PrintLayoutJson { get; set; }
}
