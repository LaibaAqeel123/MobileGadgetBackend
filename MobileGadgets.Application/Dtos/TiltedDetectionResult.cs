namespace MobileGadgets.Application.Dtos;

/// <summary>Everything TiltedDetectionService produces from one admin-uploaded base photo. Saved
/// once per HeroModel and reused for every customer render — detection never runs again after
/// admin approval, same principle as Straight's own asset model.</summary>
public class TiltedDetectionResult
{
    /// <summary>White = real case body (front face + visible side + rounded corners), black =
    /// background. This is the hard backstop the renderer clamps the design to — the design is
    /// allowed to extend past the detected quad on purpose (see TiltedDetectionService's
    /// EdgeBleedPx), but never past this real silhouette, so it can never paint onto real
    /// background no matter how the idealized quad and the true photographed edge disagree at any
    /// one spot.</summary>
    public required byte[] SilhouettePng { get; init; }

    /// <summary>White = excluded from printing (camera lenses/flash/mic and any other real
    /// hardware hole belonging to the same cluster). Never touched by the design regardless of
    /// what the silhouette or quad say.</summary>
    public required byte[] CameraMaskPng { get; init; }

    /// <summary>Level-normalized grayscale lighting map derived from the base photo's own real
    /// shading, blended "Overlay" onto the flat design at render time so it picks up the real
    /// photo's highlights/shadows instead of looking pasted-on-flat.</summary>
    public required byte[] OverlayPng { get; init; }

    public required TiltedQuad Quad { get; init; }
    public required TiltedBbox Bbox { get; init; }

    /// <summary>A starting outline for the camera area, as real editable points (not just a raster
    /// mask) — seeds the admin's point editor with an already-close guess so they're nudging a
    /// real shape, never drawing one from nothing. Empty if no camera hole was found.</summary>
    public required List<TiltedPoint> CameraOutlinePoints { get; init; }
}
