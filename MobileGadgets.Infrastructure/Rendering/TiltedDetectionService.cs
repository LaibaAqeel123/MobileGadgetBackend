using MobileGadgets.Application.Dtos;
using MobileGadgets.Application.Interfaces;
using static MobileGadgets.Infrastructure.Rendering.Geometry2D;

namespace MobileGadgets.Infrastructure.Rendering;

/// <summary>Detects a phone case's silhouette, true corners, and camera-hole cluster from a
/// single admin-uploaded tilted photo — v2, "line-fit" approach. No GrabCut, no admin seed box,
/// no admin corner-dragging in the normal case.
///
/// Why this replaced the v1 (GrabCut + admin-dragged-corner + big axis-aligned camera box)
/// approach: v1 asked the admin to drag a point onto the case's own rounded corner, which has no
/// single unambiguous position (it's a curve, not a point) — any two people would place it
/// slightly differently, and that placement error showed up directly as either a bare case sliver
/// or a design bleed onto the background. v2 never looks at the corner at all: it fits a straight
/// line to each of the case's 4 SIDES (unambiguous, high-contrast, nothing curved about them) and
/// computes the corners as where those lines meet. Proven on 5 real client photos — 2 single-
/// window styles, 2 multi-hole styles, 1 case-only (no phone inside) — with zero admin
/// interaction needed to get a clean result on any of them; see TiltedHeroImageRenderer.cs for the
/// render-time half of this (the hard silhouette backstop that keeps the idealized line-fit quad
/// from ever painting onto real background).</summary>
public class TiltedDetectionService : ITiltedDetectionService
{
    /// <summary>Below this brightness, a pixel counts as background/hole rather than case body.
    /// One global threshold is a real limitation (see TiltedDetectionService's Otsu-based
    /// predecessor's own note on this) but every photo tested so far has had a near-pure-black
    /// backdrop and a light case, giving a wide safe margin either side of 70-80.</summary>
    private const double BrightnessThreshold = 75;

    /// <summary>How far past the detected brightness edge to grow the traced case outline.
    /// Confirmed on a real photo: without this, a thin sliver of the real (unprinted) case color
    /// shows just outside the design, because a threshold-based edge always lands a couple px
    /// inside the true physical edge (anti-aliasing blends case color toward the background before
    /// a pixel reads as fully "dark"). TiltedHeroImageRenderer's silhouette backstop is what makes
    /// this safe to be a little generous with — any overshoot past the REAL edge gets clamped back
    /// at render time, so this only ever fixes undershoot, never causes overshoot.</summary>
    private const double EdgeBleedPx = 4;

    /// <summary>Minimum pixel count for a dark-enclosed region to be considered a real hole
    /// (lens/flash/mic) rather than noise (a dust speck, a stray specular catch-light) — confirmed
    /// necessary on a real photo where a single such pixel otherwise dragged a fitted exclusion
    /// rectangle out toward empty space nowhere near the actual camera module.</summary>
    private const int MinHoleComponentArea = 40;

    /// <summary>Below this fraction of the photo's area, a detected body is treated as a failure
    /// rather than a genuine tiny phone.</summary>
    private const double MinPlausibleBodyAreaFrac = 0.03;

    public async Task<TiltedDetectionResult> DetectAsync(
        Stream basePhoto, double cameraRoundR = 25, double cameraPadPx = 15, bool coverCameraPlate = false)
    {
        var full = await RgbaImage.LoadAsync(basePhoto);
        int w = full.Width, h = full.Height;

        var lum = new double[w * h];
        for (var i = 0; i < w * h; i++) lum[i] = Luminance(full.R(i), full.G(i), full.B(i));

        var fg = new byte[w * h];
        for (var i = 0; i < w * h; i++) fg[i] = (byte)(lum[i] > BrightnessThreshold ? 1 : 0);
        var body = LargestComponent(fg, w, h);

        var bodyArea = body.Count(b => b != 0);
        if (bodyArea < MinPlausibleBodyAreaFrac * w * h)
            throw new InvalidOperationException(
                "Could not find a plausible case body in this photo (too little bright, connected area against the background). " +
                "Try a photo with a cleaner, darker background and a well-lit case.");

        var quad = FitCaseQuad(body, w, h);
        var (holeMaskFull, clusters) = BuildCameraHoleMask(lum, w, h, cameraRoundR, cameraPadPx, coverCameraPlate);

        var silhouettePng = await BuildMaskImage(body, w, h).ToPngBytesAsync();
        var cameraMaskPng = await holeMaskFull.ToPngBytesAsync();

        var printable = new double[w * h];
        for (var i = 0; i < w * h; i++) printable[i] = body[i] != 0 && holeMaskFull.R(i) < 128 ? 1.0 : 0.0;
        var overlay = BuildOverlay(full, printable);
        var overlayPng = await overlay.ToPngBytesAsync();

        int bx0 = w, bx1 = 0, by0 = h, by1 = 0;
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                if (body[y * w + x] == 0) continue;
                if (x < bx0) bx0 = x;
                if (x > bx1) bx1 = x;
                if (y < by0) by0 = y;
                if (y > by1) by1 = y;
            }
        }

        return new TiltedDetectionResult
        {
            SilhouettePng = silhouettePng,
            CameraMaskPng = cameraMaskPng,
            OverlayPng = overlayPng,
            Quad = quad,
            Bbox = new TiltedBbox(bx0, by0, bx1, by1),
        };
    }

    /// <summary>The core of the v2 approach: fit the case's 4 corners from its straight edges,
    /// never from the ambiguous rounded corner itself. Step 1 gets an approximate orientation
    /// (MinAreaRect over the WHOLE silhouette, corner curve included — good enough to know which
    /// pixels belong to which of the 4 sides, not good enough to trust the corners it reports).
    /// Step 2 re-collects boundary points strictly from the middle ~55% of each side (well clear
    /// of the curved corners) and fits each side as its own straight line. Step 3 computes the
    /// real corners as where adjacent fitted lines intersect, then grows the whole outline a few
    /// px outward (see EdgeBleedPx).</summary>
    private static TiltedQuad FitCaseQuad(byte[] body, int w, int h)
    {
        var boundaryPts = new List<Point2>();
        for (var y = 0; y < h; y += 2)
        {
            int l = -1, r = -1;
            for (var x = 0; x < w; x++) { if (body[y * w + x] != 0) { l = x; break; } }
            for (var x = w - 1; x >= 0; x--) { if (body[y * w + x] != 0) { r = x; break; } }
            if (l < 0) continue;
            boundaryPts.Add(new Point2(l, y));
            boundaryPts.Add(new Point2(r, y));
        }

        var hull = ConvexHull(boundaryPts);
        var (c0, c1, c2, c3) = MinAreaRect(hull);
        var roughCorners = new[] { c0, c1, c2, c3 };

        var sideLines = new Line2[4];
        for (var s = 0; s < 4; s++)
        {
            var a = roughCorners[s];
            var b = roughCorners[(s + 1) % 4];
            var ex = b.X - a.X;
            var ey = b.Y - a.Y;
            var sideLen = Math.Sqrt(ex * ex + ey * ey);
            var ux = ex / sideLen;
            var uy = ey / sideLen;
            var nx = -uy;
            var ny = ux;

            var band = new List<Point2>();
            foreach (var p in boundaryPts)
            {
                var t = ((p.X - a.X) * ux + (p.Y - a.Y) * uy) / sideLen;
                if (t is < 0.225 or > 0.775) continue; // skip the curved corner zones
                var perp = (p.X - a.X) * nx + (p.Y - a.Y) * ny;
                if (Math.Abs(perp) < 30) band.Add(p); // reject the far/opposite side's points outright
            }
            sideLines[s] = FitLineTls(band.Count >= 2 ? band : boundaryPts);
        }

        var rawCorners = new Point2[4];
        for (var i = 0; i < 4; i++) rawCorners[i] = LineIntersect(sideLines[(i + 3) % 4], sideLines[i]);
        var rawCentroid = new Point2(rawCorners.Average(p => p.X), rawCorners.Average(p => p.Y));

        var expanded = sideLines.Select(l => ExpandLineOutward(l, rawCentroid, EdgeBleedPx)).ToArray();
        var refined = new Point2[4];
        for (var i = 0; i < 4; i++) refined[i] = LineIntersect(expanded[(i + 3) % 4], expanded[i]);

        var ordered = OrderTlTrBrBl(refined);
        return new TiltedQuad(
            new TiltedPoint(ordered[0].X, ordered[0].Y),
            new TiltedPoint(ordered[1].X, ordered[1].Y),
            new TiltedPoint(ordered[2].X, ordered[2].Y),
            new TiltedPoint(ordered[3].X, ordered[3].Y));
    }

    /// <summary>Orders 4 arbitrary corners as TL,TR,BR,BL: sorts by angle around the centroid (a
    /// consistent winding for any convex quad regardless of the photo's exact tilt), then rotates
    /// so index 0 is whichever corner has the smallest x+y — the top-left-most one for any modest
    /// rotation (every photo tested has been tilted well under 45deg, where this holds).</summary>
    private static Point2[] OrderTlTrBrBl(Point2[] corners)
    {
        var cx = corners.Average(p => p.X);
        var cy = corners.Average(p => p.Y);
        var byAngle = corners.OrderBy(p => Math.Atan2(p.Y - cy, p.X - cx)).ToArray();
        var startIdx = 0;
        var bestScore = double.MaxValue;
        for (var i = 0; i < byAngle.Length; i++)
        {
            var score = byAngle[i].X + byAngle[i].Y;
            if (score < bestScore) { bestScore = score; startIdx = i; }
        }
        return Enumerable.Range(0, 4).Select(i => byAngle[(startIdx + i) % 4]).ToArray();
    }

    /// <summary>Finds the camera-hole cluster(s) and rasterizes their padded/rounded exclusion
    /// shapes into one mask image. See Geometry2D.ClusterCompactComponents for why proximity alone
    /// isn't enough (a nearby button slot must never merge with the actual camera module).
    ///
    /// <paramref name="coverCameraPlate"/> decides what gets excluded around a real lens/flash: by
    /// default (false), the WHOLE module cluster — lenses plus the raised plate/trim around them —
    /// stays in its real photographed color, correct for a case whose camera trim is a genuinely
    /// separate material/color from the body. Confirmed wrong on a real one-piece silicone case,
    /// though (Model Manager screenshot, 2026-09-11): that whole case is molded from one uniform
    /// color, so leaving the plate in its original white read as a mismatched patch instead of part
    /// of the product. Set true for that case: only the individual lens/flash holes themselves stay
    /// excluded (each with its own small, tight margin), and the design flows across the rest of
    /// the plate exactly like the rest of the body.</summary>
    private static (RgbaImage Mask, List<List<int>> Clusters) BuildCameraHoleMask(
        double[] lum, int w, int h, double mainRoundR, double mainMarginPercent, bool coverCameraPlate)
    {
        var dark = new byte[w * h];
        for (var i = 0; i < w * h; i++) dark[i] = (byte)(lum[i] < BrightnessThreshold ? 1 : 0);
        var reach = ReachableFromBorder(dark, w, h);
        var enclosed = new byte[w * h];
        for (var i = 0; i < w * h; i++) enclosed[i] = (byte)(dark[i] != 0 && reach[i] == 0 ? 1 : 0);

        var components = ComponentsAboveArea(enclosed, w, h, MinHoleComponentArea);
        // Each raw component (one lens, the flash, etc.) becomes its own small exclusion instead
        // of merging into one big plate shape -- same components, just not clustered together.
        var clusters = coverCameraPlate ? components : ClusterCompactComponents(components, w);

        // Reference size for scaling margin: the MEDIAN individual component's own bbox diagonal
        // -- deliberately not the cluster's own bounding box (see BuildRoundedExclusion's own
        // note on why that regressed). A single lens is a stable, representative "how big is one
        // real feature here" measure regardless of how spread out the whole cluster is; median
        // (not mean) keeps one outlier -- the tiny mic-hole dot next to full-size lenses -- from
        // skewing it.
        var componentSizes = components
            .Select(c => { var b = ComputeBbox(c, w); return Math.Sqrt(b.Width * (double)b.Width + b.Height * (double)b.Height); })
            .OrderBy(s => s).ToList();
        var refSizePx = componentSizes.Count > 0 ? componentSizes[componentSizes.Count / 2] : 40;

        var mask = new RgbaImage(w, h);
        for (var ci = 0; ci < clusters.Count; ci++)
        {
            // Roundness is a PERCENT of the shape's own size (see BuildRoundedExclusion) -- a
            // tight individual lens/flash hole is close to circular, so it wants a HIGH roundness
            // percent (near 100, "fully rounded"). Margin is plain pixels, derived from refSizePx
            // (a stand-in for "how big is one real feature in this photo") rather than any
            // per-shape measurement.
            var marginPx = coverCameraPlate ? refSizePx * 0.15 : (ci == 0 ? refSizePx * mainMarginPercent / 100.0 : refSizePx * 0.4);
            var roundnessPercent = coverCameraPlate ? 90 : ci == 0 ? mainRoundR : 45;
            var test = BuildRoundedExclusion(clusters[ci], w, marginPx, roundnessPercent);
            for (var y = 0; y < h; y++)
            {
                for (var x = 0; x < w; x++)
                {
                    if (!test.Contains(x, y)) continue;
                    var idx = (y * w + x) * 4;
                    mask.Data[idx] = 255; mask.Data[idx + 1] = 255; mask.Data[idx + 2] = 255; mask.Data[idx + 3] = 255;
                }
            }
        }
        mask.GaussianBlur(2);
        return (mask, clusters);
    }

    private static RgbaImage BuildMaskImage(byte[] binaryMask, int w, int h)
    {
        var img = new RgbaImage(w, h);
        for (var i = 0; i < w * h; i++)
        {
            var v = (byte)(binaryMask[i] != 0 ? 255 : 0);
            var idx = i * 4;
            img.Data[idx] = v; img.Data[idx + 1] = v; img.Data[idx + 2] = v; img.Data[idx + 3] = 255;
        }
        img.GaussianBlur(2);
        return img;
    }

    /// <summary>Level-normalized grayscale lighting map: the printable area's own mean luminance
    /// is remapped to mid-gray so the "Overlay" blend at render time neither darkens nor brightens
    /// the design on average — it only carries the photo's real shading detail, damped so one
    /// strong real highlight doesn't dominate a saturated design's colors.</summary>
    private static RgbaImage BuildOverlay(RgbaImage full, double[] printable)
    {
        const double contrastDamping = 0.3;
        int w = full.Width, h = full.Height;
        double sum = 0;
        var count = 0;
        var gray = new double[w * h];
        for (var i = 0; i < w * h; i++)
        {
            gray[i] = Luminance(full.R(i), full.G(i), full.B(i));
            if (printable[i] > 0.5) { sum += gray[i]; count++; }
        }
        var levelFactor = 128.0 / (count > 0 ? sum / count : 128.0);

        var overlay = new RgbaImage(w, h);
        for (var i = 0; i < w * h; i++)
        {
            var levelG = gray[i] * levelFactor;
            var damped = 128.0 + (levelG - 128.0) * contrastDamping;
            var g = (byte)Math.Clamp(Math.Round(damped), 0, 255);
            var idx = i * 4;
            overlay.Data[idx] = g; overlay.Data[idx + 1] = g; overlay.Data[idx + 2] = g; overlay.Data[idx + 3] = 255;
        }
        return overlay;
    }

    private static double Luminance(byte r, byte g, byte b) => 0.299 * r + 0.587 * g + 0.114 * b;
}
