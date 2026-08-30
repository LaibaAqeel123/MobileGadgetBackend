namespace MobileGadgets.Infrastructure.Rendering;

public static class PixelOps
{
    /// <summary>Bilinear sample at (x,y); null if out of bounds. Reads directly from the pixel
    /// buffer (no per-sample delegate allocation) — this runs millions of times per render.</summary>
    public static (double r, double g, double b, double a)? Bilinear(RgbaImage img, double x, double y)
    {
        int w = img.Width, h = img.Height;
        if (x < 0 || y < 0 || x > w - 1 || y > h - 1) return null;

        var x0 = (int)x;
        var y0 = (int)y;
        var x1 = Math.Min(x0 + 1, w - 1);
        var y1 = Math.Min(y0 + 1, h - 1);
        var fx = x - x0;
        var fy = y - y0;
        var data = img.Data;

        var i00 = (y0 * w + x0) * 4;
        var i10 = (y0 * w + x1) * 4;
        var i01 = (y1 * w + x0) * 4;
        var i11 = (y1 * w + x1) * 4;

        var w00 = (1 - fx) * (1 - fy);
        var w10 = fx * (1 - fy);
        var w01 = (1 - fx) * fy;
        var w11 = fx * fy;

        double Sample(int channel) =>
            data[i00 + channel] * w00 + data[i10 + channel] * w10 + data[i01 + channel] * w01 + data[i11 + channel] * w11;

        return (Sample(0), Sample(1), Sample(2), Sample(3));
    }

    /// <summary>Mask "coverage" (0-1) at pixel i: alpha * luminance/255. Reduces to the alpha
    /// channel for silhouette-shaped PNGs with transparency, and to plain black/white for
    /// opaque masks painted by hand (Photoshop method) — supports both mask conventions.</summary>
    public static double MaskCoverage(RgbaImage mask, int i) =>
        (mask.A(i) / 255.0) * (mask.R(i) / 255.0);

    /// <summary>Standard Porter-Duff "source over destination" alpha composite, in place onto dst.</summary>
    public static void CompositeOver(RgbaImage dst, RgbaImage src)
    {
        for (var i = 0; i < dst.Width * dst.Height; i++)
        {
            var a = src.A(i) / 255.0;
            if (a <= 0) continue;
            var idx = i * 4;
            dst.Data[idx] = (byte)Math.Round(src.Data[idx] * a + dst.Data[idx] * (1 - a));
            dst.Data[idx + 1] = (byte)Math.Round(src.Data[idx + 1] * a + dst.Data[idx + 1] * (1 - a));
            dst.Data[idx + 2] = (byte)Math.Round(src.Data[idx + 2] * a + dst.Data[idx + 2] * (1 - a));
            dst.Data[idx + 3] = (byte)Math.Min(255, src.Data[idx + 3] + dst.Data[idx + 3] * (1 - a));
        }
    }

    /// <summary>Photoshop/CSS "overlay" blend: backdrop (dst) picks the branch, source (src) is blended in.
    /// Colour only — alpha is left untouched by the caller (matches the prototype's approach of
    /// re-applying the mask alpha afterward, since an opaque overlay layer would otherwise stomp it).</summary>
    public static void OverlayBlendColorOnly(RgbaImage dst, RgbaImage src)
    {
        for (var i = 0; i < dst.Width * dst.Height; i++)
        {
            var idx = i * 4;
            for (var c = 0; c < 3; c++)
            {
                double cb = dst.Data[idx + c] / 255.0;
                double cs = src.Data[idx + c] / 255.0;
                var blended = cb <= 0.5 ? 2 * cb * cs : 1 - 2 * (1 - cb) * (1 - cs);
                dst.Data[idx + c] = (byte)Math.Round(Math.Clamp(blended, 0, 1) * 255);
            }
        }
    }

    /// <summary>Warps src (its 4 corners TL,TR,BR,BL) into dstCorners on the shared canvas `dst`,
    /// alpha-compositing over whatever is already there. Only iterates the destination bounding
    /// box, so large floor/wall quads don't cost a full canvas scan.</summary>
    public static void WarpInto(RgbaImage dst, RgbaImage src, (double x, double y)[] dstCorners)
    {
        (double x, double y)[] srcCorners =
        [
            (0, 0),
            (src.Width, 0),
            (src.Width, src.Height),
            (0, src.Height),
        ];
        var h = Homography.Solve(srcCorners, dstCorners);
        var hInv = Homography.Invert3x3(h);

        var xs = dstCorners.Select(p => p.x).ToArray();
        var ys = dstCorners.Select(p => p.y).ToArray();
        var x0 = Math.Max(0, (int)Math.Floor(xs.Min()));
        var x1 = Math.Min(dst.Width - 1, (int)Math.Ceiling(xs.Max()));
        var y0 = Math.Max(0, (int)Math.Floor(ys.Min()));
        var y1 = Math.Min(dst.Height - 1, (int)Math.Ceiling(ys.Max()));

        for (var y = y0; y <= y1; y++)
        {
            for (var x = x0; x <= x1; x++)
            {
                var (sx, sy) = Homography.Apply(hInv, x, y);
                var px = Bilinear(src, sx, sy);
                if (px is null) continue;
                var (r, g, b, av) = px.Value;
                var a = av / 255.0;
                if (a <= 0) continue;
                var idx = (y * dst.Width + x) * 4;
                dst.Data[idx] = (byte)Math.Round(r * a + dst.Data[idx] * (1 - a));
                dst.Data[idx + 1] = (byte)Math.Round(g * a + dst.Data[idx + 1] * (1 - a));
                dst.Data[idx + 2] = (byte)Math.Round(b * a + dst.Data[idx + 2] * (1 - a));
                dst.Data[idx + 3] = (byte)Math.Min(255, Math.Round(av + dst.Data[idx + 3] * (1 - a)));
            }
        }
    }

    /// <summary>Darkens toward the four corners only (a radial falloff from (cxFrac, cyFrac) as a
    /// fraction of width/height) — draws the eye to the product, present in nearly every real
    /// studio product photo. `innerFrac` is how far out (0-1, as a fraction of the distance to the
    /// farthest corner) the darkening starts; `maxOpacity` is the darkening strength at the very
    /// corners.</summary>
    public static void ApplyVignette(RgbaImage img, double cxFrac, double cyFrac, double innerFrac, double maxOpacity)
    {
        int w = img.Width, h = img.Height;
        var cx = w * cxFrac;
        var cy = h * cyFrac;
        var maxDist = Math.Sqrt(Math.Pow(Math.Max(cx, w - cx), 2) + Math.Pow(Math.Max(cy, h - cy), 2));

        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                var dist = Math.Sqrt(Math.Pow(x - cx, 2) + Math.Pow(y - cy, 2)) / maxDist;
                if (dist <= innerFrac) continue;
                var opacity = Math.Min(1, (dist - innerFrac) / (1 - innerFrac)) * maxOpacity;
                var idx = (y * w + x) * 4;
                img.Data[idx] = (byte)Math.Round(img.Data[idx] * (1 - opacity));
                img.Data[idx + 1] = (byte)Math.Round(img.Data[idx + 1] * (1 - opacity));
                img.Data[idx + 2] = (byte)Math.Round(img.Data[idx + 2] * (1 - opacity));
            }
        }
    }

    /// <summary>Low-amplitude luminance noise, blended with Photoshop/CSS "overlay" math (same
    /// blend used for the case's lighting map) — kills the "flat digital gradient" look far more
    /// cheaply than any amount of extra shading detail.</summary>
    public static void ApplyFilmGrain(RgbaImage img, double amplitude, Random rng)
    {
        for (var i = 0; i < img.Width * img.Height; i++)
        {
            var grain = Math.Clamp(128 + (rng.NextDouble() - 0.5) * 2 * amplitude, 0, 255);
            var cs = grain / 255.0;
            var idx = i * 4;
            for (var c = 0; c < 3; c++)
            {
                var cb = img.Data[idx + c] / 255.0;
                var blended = cb <= 0.5 ? 2 * cb * cs : 1 - 2 * (1 - cb) * (1 - cs);
                img.Data[idx + c] = (byte)Math.Round(Math.Clamp(blended, 0, 1) * 255);
            }
        }
    }

    /// <summary>Fills a soft-edged (antialiased) ellipse of the given color/opacity directly into an RGBA buffer.</summary>
    public static void FillEllipse(RgbaImage img, double cx, double cy, double rx, double ry, byte r, byte g, byte b, double opacity)
    {
        var x0 = Math.Max(0, (int)(cx - rx - 1));
        var x1 = Math.Min(img.Width - 1, (int)(cx + rx + 1));
        var y0 = Math.Max(0, (int)(cy - ry - 1));
        var y1 = Math.Min(img.Height - 1, (int)(cy + ry + 1));

        for (var y = y0; y <= y1; y++)
        {
            for (var x = x0; x <= x1; x++)
            {
                var dx = (x - cx) / rx;
                var dy = (y - cy) / ry;
                var dist = Math.Sqrt(dx * dx + dy * dy);
                if (dist > 1.05) continue;
                var edgeAlpha = dist <= 1.0 ? 1.0 : Math.Max(0, 1 - (dist - 1.0) / 0.05);
                var a = edgeAlpha * opacity;
                if (a <= 0) continue;
                var idx = (y * img.Width + x) * 4;
                img.Data[idx] = (byte)Math.Round(r * a + img.Data[idx] * (1 - a));
                img.Data[idx + 1] = (byte)Math.Round(g * a + img.Data[idx + 1] * (1 - a));
                img.Data[idx + 2] = (byte)Math.Round(b * a + img.Data[idx + 2] * (1 - a));
                img.Data[idx + 3] = (byte)Math.Min(255, Math.Round(a * 255 + img.Data[idx + 3] * (1 - a)));
            }
        }
    }
}
