namespace MobileGadgets.Infrastructure.Rendering;

/// <summary>Stage A: combine a phone's 4 stored layers + the customer's design into one flat,
/// correctly-lit, camera-safe composite. Port of prototype_flat_warp.js's flattening steps —
/// generic over any HeroModel's images, no phone-specific code.</summary>
public static class FlattenStage
{
    public static RgbaImage Flatten(RgbaImage baseImg, RgbaImage designMask, RgbaImage cameraMask, RgbaImage overlay, RgbaImage design)
    {
        var w = baseImg.Width;
        var h = baseImg.Height;

        designMask = designMask.ResizeExact(w, h);
        cameraMask = cameraMask.ResizeExact(w, h);
        overlay = overlay.ResizeExact(w, h);
        // `design` is expected to already be cover-fit to (w,h) by the caller.

        // ---- design layer: sample the customer's design, alpha = design mask coverage ----
        var designLayer = new RgbaImage(w, h);
        for (var i = 0; i < w * h; i++)
        {
            var idx = i * 4;
            designLayer.Data[idx] = design.Data[idx];
            designLayer.Data[idx + 1] = design.Data[idx + 1];
            designLayer.Data[idx + 2] = design.Data[idx + 2];
            designLayer.Data[idx + 3] = (byte)Math.Round(PixelOps.MaskCoverage(designMask, i) * 255);
        }

        // ---- blend the real lighting-map overlay onto the design layer ("overlay" blend, same
        // as CaseCanvas.jsx's globalCompositeOperation="overlay"), then restore the design mask's
        // alpha (the blend only touches colour, but we reset alpha defensively to be exact) ----
        PixelOps.OverlayBlendColorOnly(designLayer, overlay);
        for (var i = 0; i < w * h; i++)
            designLayer.Data[i * 4 + 3] = (byte)Math.Round(PixelOps.MaskCoverage(designMask, i) * 255);

        // ---- flat composite: base (bottom) + shaded design (clipped, on top) ----
        var flat = baseImg.Clone();
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
