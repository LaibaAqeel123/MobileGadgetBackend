using MobileGadgets.Application.Dtos;

namespace MobileGadgets.Application.Interfaces;

public interface ITiltedHeroImageRenderer
{
    /// <summary>Warps the customer's design onto a saved TiltedDetectionResult's geometry and
    /// composites it back onto the real base photo. `swapBackground` null keeps the base photo's
    /// own real background (the simplest, most photographically convincing tier); provided, the
    /// case is cut out and placed onto the new backdrop with a matching-angle shadow so it still
    /// reads as "standing" rather than floating.</summary>
    Task<byte[]> RenderAsync(
        Stream basePhoto,
        Stream silhouetteMask,
        Stream cameraMask,
        Stream overlay,
        Stream design,
        TiltedQuad designQuad,
        TiltedBbox caseBbox,
        Stream? swapBackground = null);
}
