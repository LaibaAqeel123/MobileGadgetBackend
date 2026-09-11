using MobileGadgets.Application.Dtos;
using MobileGadgets.Application.Interfaces;

namespace MobileGadgets.Infrastructure.Rendering;

/// <summary>Renders a Tilted HeroModel's hero photo (v2 — see TiltedDetectionService.cs for the
/// matching detection half). Warps the customer's design onto the quad TiltedDetectionService
/// fitted from the case's own straight edges, then clamps it to the REAL detected silhouette mask
/// — not a second idealized shape. This matters: the silhouette mask is real detected pixels, so
/// it is already naturally rounded at the corners exactly the way the real photographed case is.
/// The quad's own corners (where the straight edges would meet if extended) sit slightly outside
/// that true rounded corner on purpose — clamping the warp down to the real silhouette there is
/// what makes the rendered corner match the real corner, with no separate rounding math needed at
/// render time.</summary>
public class TiltedHeroImageRenderer : ITiltedHeroImageRenderer
{
    private const int OutW = 1200, OutH = 1600;

    public async Task<byte[]> RenderAsync(
        Stream basePhoto,
        Stream silhouetteMask,
        Stream cameraMask,
        Stream overlay,
        Stream design,
        TiltedQuad designQuad,
        TiltedBbox caseBbox,
        Stream? swapBackground = null)
    {
        var baseImg = await RgbaImage.LoadAsync(basePhoto);
        var w = baseImg.Width;
        var h = baseImg.Height;

        var silhouetteImg = (await RgbaImage.LoadAsync(silhouetteMask)).ResizeExact(w, h);
        var cameraMaskImg = (await RgbaImage.LoadAsync(cameraMask)).ResizeExact(w, h);
        var overlayImg = (await RgbaImage.LoadAsync(overlay)).ResizeExact(w, h);

        // Combine as hard/boolean masks FIRST, then blur the COMBINED result exactly once. Two
        // independently soft-edged masks (the silhouette's own antialiased edge and the camera
        // hole's own antialiased edge) multiplied together let their blend bands interact
        // unpredictably wherever the camera module sits close to the case's own edge — re-
        // threshold each hard, combine with a clean boolean AND-NOT, feather the result once.
        var silhouetteHard = new byte[w * h];
        var holeHard = new byte[w * h];
        for (var i = 0; i < w * h; i++)
        {
            silhouetteHard[i] = (byte)(silhouetteImg.R(i) >= 128 ? 1 : 0);
            holeHard[i] = (byte)(cameraMaskImg.R(i) >= 128 ? 1 : 0);
        }

        var printableHard = new byte[w * h];
        var holeWithinSilhouetteHard = new byte[w * h];
        for (var i = 0; i < w * h; i++)
        {
            printableHard[i] = (byte)(silhouetteHard[i] == 1 && holeHard[i] == 0 ? 1 : 0);
            holeWithinSilhouetteHard[i] = (byte)(silhouetteHard[i] == 1 && holeHard[i] == 1 ? 1 : 0);
        }

        const double edgeFeather = 1.2;
        var printableSoft = ToGrayImage(printableHard, w, h);
        printableSoft.GaussianBlur(edgeFeather);
        var hwAlphaSoft = ToGrayImage(holeWithinSilhouetteHard, w, h);
        hwAlphaSoft.GaussianBlur(edgeFeather);

        var (tl, tr, br, bl) = (designQuad.Tl, designQuad.Tr, designQuad.Br, designQuad.Bl);
        var quadW = (Dist(tl, tr) + Dist(bl, br)) / 2;
        var quadH = (Dist(tl, bl) + Dist(tr, br)) / 2;
        const int designBaseW = 1000;
        var designBaseH = Math.Max(1, (int)Math.Round(quadH / quadW * designBaseW));
        var designImg = await RgbaImage.LoadCoverFitAsync(design, designBaseW, designBaseH);

        var designLayer = new RgbaImage(w, h);
        PixelOps.WarpInto(designLayer, designImg, ToCorners(designQuad));

        // Clamp to whatever the warp actually painted AND the real silhouette — never let the
        // design claim a pixel the real photo's own detected edge doesn't back up.
        for (var i = 0; i < w * h; i++)
        {
            var idx = i * 4 + 3;
            designLayer.Data[idx] = Math.Min(designLayer.Data[idx], (byte)Math.Round(printableSoft.R(i) / 255.0 * 255));
        }

        PixelOps.OverlayBlendColorOnly(designLayer, overlayImg);

        // Hardware-safety patch: nothing ever covers the real camera lenses/flash/mic — re-expose
        // the real photographed pixels there regardless of anything above.
        var hwPatch = new RgbaImage(w, h);
        for (var i = 0; i < w * h; i++)
        {
            var idx = i * 4;
            hwPatch.Data[idx] = baseImg.Data[idx];
            hwPatch.Data[idx + 1] = baseImg.Data[idx + 1];
            hwPatch.Data[idx + 2] = baseImg.Data[idx + 2];
            hwPatch.Data[idx + 3] = hwAlphaSoft.R(i);
        }

        if (swapBackground is null)
        {
            var result = baseImg.Clone();
            PixelOps.CompositeOver(result, designLayer);
            PixelOps.CompositeOver(result, hwPatch);
            return await result.ToPngBytesAsync();
        }

        return await RenderWithSwappedBackground(designLayer, hwPatch, designQuad, caseBbox, w, h, swapBackground);
    }

    private static async Task<byte[]> RenderWithSwappedBackground(
        RgbaImage designLayer, RgbaImage hwPatch, TiltedQuad designQuad, TiltedBbox caseBbox,
        int w, int h, Stream swapBackground)
    {
        var cutout = new RgbaImage(w, h);
        PixelOps.CompositeOver(cutout, designLayer);
        PixelOps.CompositeOver(cutout, hwPatch);

        var bboxW = caseBbox.X1 - caseBbox.X0;
        var bboxH = caseBbox.Y1 - caseBbox.Y0;
        const double floorYFrac = 0.94;
        var placeScale = Math.Min(OutW * 0.56 / bboxW, OutH * 0.74 / bboxH);
        var offX = OutW * 0.46 - (caseBbox.X0 + caseBbox.X1) / 2 * placeScale;
        var offY = OutH * floorYFrac - caseBbox.Y1 * placeScale;

        // The phone's bottom edge is a slanted line because the phone is tilted — rotate the
        // shadow to that real angle so it hugs both bottom corners the way the phone's own edge
        // does, instead of only touching the lower of the two.
        var bl = (X: designQuad.Bl.X * placeScale + offX, Y: designQuad.Bl.Y * placeScale + offY);
        var br = (X: designQuad.Br.X * placeScale + offX, Y: designQuad.Br.Y * placeScale + offY);
        var shadowMidX = (bl.X + br.X) / 2;
        var shadowMidY = (bl.Y + br.Y) / 2;
        var shadowW = bboxW * placeScale;

        var backdrop = await RgbaImage.LoadCoverFitAsync(swapBackground, OutW, OutH);
        backdrop.GaussianBlur(4); // depth-of-field sells "photographed" far more than a crisp backdrop

        var shadow = new RgbaImage(OutW, OutH);
        PixelOps.FillEllipse(shadow, shadowMidX + shadowW * 0.04, shadowMidY + shadowW * 0.035, shadowW * 0.62, shadowW * 0.16, 10, 10, 10, 0.35);
        PixelOps.FillEllipse(shadow, shadowMidX, shadowMidY, shadowW * 0.4, shadowW * 0.07, 5, 5, 5, 0.55);
        shadow.GaussianBlur(10);
        PixelOps.CompositeOver(backdrop, shadow);

        (double x, double y)[] dstRect =
        [
            (offX, offY),
            (w * placeScale + offX, offY),
            (w * placeScale + offX, h * placeScale + offY),
            (offX, h * placeScale + offY),
        ];
        var warpedCutout = new RgbaImage(OutW, OutH);
        PixelOps.WarpInto(warpedCutout, cutout, dstRect);
        PixelOps.CompositeOver(backdrop, warpedCutout);

        PixelOps.ApplyVignette(backdrop, cxFrac: 0.5, cyFrac: 0.46, innerFrac: 0.55, maxOpacity: 0.35);
        PixelOps.ApplyFilmGrain(backdrop, amplitude: 10, new Random());

        return await backdrop.ToPngBytesAsync();
    }

    private static RgbaImage ToGrayImage(byte[] binaryMask, int w, int h)
    {
        var img = new RgbaImage(w, h);
        for (var i = 0; i < w * h; i++)
        {
            var v = (byte)(binaryMask[i] != 0 ? 255 : 0);
            var idx = i * 4;
            img.Data[idx] = v; img.Data[idx + 1] = v; img.Data[idx + 2] = v; img.Data[idx + 3] = 255;
        }
        return img;
    }

    private static double Dist(TiltedPoint a, TiltedPoint b) => Math.Sqrt(Math.Pow(b.X - a.X, 2) + Math.Pow(b.Y - a.Y, 2));

    private static (double x, double y)[] ToCorners(TiltedQuad q) =>
    [
        (q.Tl.X, q.Tl.Y),
        (q.Tr.X, q.Tr.Y),
        (q.Br.X, q.Br.Y),
        (q.Bl.X, q.Bl.Y),
    ];
}
