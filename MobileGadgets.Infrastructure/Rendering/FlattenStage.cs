namespace MobileGadgets.Infrastructure.Rendering;

/// <summary>Stage A: combine a phone's 4 stored layers + the customer's design into one flat,
/// correctly-lit, camera-safe composite. Port of prototype_flat_warp.js's flattening steps —
/// generic over any HeroModel's images, no phone-specific code.</summary>
public static class FlattenStage
{
    /// <summary>The printable area is derived, never hand-authored: the base photo's own alpha
    /// silhouette (case body + the raised camera lip/plateau — whatever shape that model's case
    /// actually has) minus only the camera mask's actual hardware openings (lens glass, flash,
    /// mic, sensor). This is what makes the mask correct for any phone/case geometry with zero
    /// per-model tuning — a separately hand-drawn "design mask" was tried twice and got the
    /// printable region wrong both times (a plain rectangle bleeding past the case's silhouette,
    /// then the whole camera lip wrongly excluded instead of just its holes).</summary>
    private static double PrintableCoverage(RgbaImage baseImg, RgbaImage cameraMask, int i) =>
        (baseImg.A(i) / 255.0) * (1 - PixelOps.MaskCoverage(cameraMask, i));

    public static RgbaImage Flatten(RgbaImage baseImg, RgbaImage cameraMask, RgbaImage overlay, RgbaImage design)
    {
        var w = baseImg.Width;
        var h = baseImg.Height;

        cameraMask = cameraMask.ResizeExact(w, h);
        overlay = overlay.ResizeExact(w, h);
        // `design` is expected to already be cover-fit to (w,h) by the caller.

        // ---- design layer: sample the customer's design, alpha = printable-area coverage ----
        var designLayer = new RgbaImage(w, h);
        for (var i = 0; i < w * h; i++)
        {
            var idx = i * 4;
            designLayer.Data[idx] = design.Data[idx];
            designLayer.Data[idx + 1] = design.Data[idx + 1];
            designLayer.Data[idx + 2] = design.Data[idx + 2];
            designLayer.Data[idx + 3] = (byte)Math.Round(PrintableCoverage(baseImg, cameraMask, i) * 255);
        }

        // ---- blend the real lighting-map overlay onto the design layer ("overlay" blend, same
        // as CaseCanvas.jsx's globalCompositeOperation="overlay"), then restore the printable-area
        // alpha (the blend only touches colour, but we reset alpha defensively to be exact) ----
        PixelOps.OverlayBlendColorOnly(designLayer, overlay);
        for (var i = 0; i < w * h; i++)
            designLayer.Data[i * 4 + 3] = (byte)Math.Round(PrintableCoverage(baseImg, cameraMask, i) * 255);

        // ---- flat composite: shaded design painted onto a BLANK canvas (not a clone of the base
        // photo) — at the silhouette's anti-aliased edge, printable coverage is fractional, and
        // compositing design over the base's own already-partially-transparent edge pixel would
        // double-blend toward that pixel's original (pre-multiplied-toward-whatever-its-source-
        // canvas-was) colour, leaving a hairline sliver of the base photo's edge tone instead of
        // the design fading cleanly to transparent. Starting blank means that fringe fades toward
        // transparency (revealing the studio background later) instead of toward stray base colour. ----
        var flat = new RgbaImage(w, h);
        PixelOps.CompositeOver(flat, designLayer);

        // ---- final hardware-safety clip: re-expose the real photographed lens/flash pixels
        // from the base photo, using the camera mask — nothing can ever cover the real camera ----
        var hwPatch = new RgbaImage(w, h);
        for (var i = 0; i < w * h; i++)
        {
            var idx = i * 4;
            hwPatch.Data[idx] = baseImg.Data[idx];
            hwPatch.Data[idx + 1] = baseImg.Data[idx + 1];
            hwPatch.Data[idx + 2] = baseImg.Data[idx + 2];
            hwPatch.Data[idx + 3] = (byte)Math.Round(PixelOps.MaskCoverage(cameraMask, i) * 255);
        }
        PixelOps.CompositeOver(flat, hwPatch);

        return flat;
    }
}
