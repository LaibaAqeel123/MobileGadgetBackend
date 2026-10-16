namespace MobileGadgets.Domain;

public class HeroModel
{
    public int Id { get; set; }
    public string PhoneName { get; set; } = string.Empty;
    public string CaseType { get; set; } = string.Empty;
    public string BaseImageUrl { get; set; } = string.Empty;
    public string DesignMaskImageUrl { get; set; } = string.Empty;
    public string CameraMaskImageUrl { get; set; } = string.Empty;
    public string OverlayImageUrl { get; set; } = string.Empty;
    /// <summary>3D models only: public URL of the uploaded .glb file (the phone wearing its case). A model
    /// is a 3D model exactly when this is set; null for the older photo-based models.</summary>
    public string? GlbUrl { get; set; }

    /// <summary>3D models only: name of the material inside the .glb that the customer's design is printed
    /// onto (normally "PRINT_AREA"; the admin can pick another one when the file was made without that name).</summary>
    public string? PrintMaterialName { get; set; }

    /// <summary>3D models only: where the printable surface's UV layout sits inside the 0..1 texture
    /// square, as JSON {"u0":..,"u1":..,"v0":..,"v1":..}. The customer's design is cover-fit into exactly
    /// this rectangle. Measured from the file at upload time, never typed in by hand.</summary>
    public string? PrintLayoutJson { get; set; }

    /// <summary>3D models only: the validation report from the upload check (see GlbInspectionReport), kept
    /// so the admin can see later why a model was accepted and what it warned about.</summary>
    public string? ValidationReportJson { get; set; }

    public DateTime CreatedAt { get; set; }
}
