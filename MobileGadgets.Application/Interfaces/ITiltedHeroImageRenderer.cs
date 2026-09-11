using MobileGadgets.Application.Dtos;

namespace MobileGadgets.Application.Interfaces;

public interface ITiltedHeroImageRenderer
{
    /// <summary>Warps the customer's design onto a saved TiltedDetectionResult's geometry and
    /// composites it back onto the real base photo. `swapBackground` null keeps the base photo's
    /// own real background (the simplest, most photographically convincing tier); provided, the
    /// case is cut out and placed onto the new backdrop with a matching-angle shadow so it still
    /// reads as "standing" rather than floating.
    ///
    /// <paramref name="cameraSpotsJson"/> (a serialized List&lt;CameraSpot&gt;) is NOT limited to
    /// the camera area despite the name — an admin can drop a Protect or Allow spot anywhere on the
    /// case. Protect spots were already fully general (they just add to the exclusion mask, which
    /// covers the whole photo). Allow spots needed real render-time support to be fully general
    /// too: an Allow spot forces its area onto the silhouette AND clears any exclusion there (fixes
    /// "real body wrongly excluded" anywhere, not just near the camera), and if the design warp
    /// itself never painted that area at all (e.g. right at the case's own outer edge, outside the
    /// fitted quad's footprint), extends the nearest already-painted design pixels into it — bounded
    /// strictly to that spot's own small area, so it can never bleed further than the admin marked.</summary>
    Task<byte[]> RenderAsync(
        Stream basePhoto,
        Stream silhouetteMask,
        Stream cameraMask,
        Stream overlay,
        Stream design,
        TiltedQuad designQuad,
        TiltedBbox caseBbox,
        string cameraSpotsJson,
        Stream? swapBackground = null);
}
