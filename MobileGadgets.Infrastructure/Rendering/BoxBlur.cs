namespace MobileGadgets.Infrastructure.Rendering;

/// <summary>Fast Gaussian-approximating blur: 3 passes of a sliding-window box blur (constant
/// cost per pixel regardless of radius), operating in premultiplied-alpha space so it doesn't
/// bleed black into partially-transparent edges. Used instead of Magick.NET's native blur,
/// which round-trips through native marshaling per call and was the actual render bottleneck.</summary>
public static class BoxBlur
{
    public static void Apply(RgbaImage img, double sigma)
    {
        if (sigma <= 0) return;
        var radius = Math.Max(1, (int)Math.Round(sigma));
        int w = img.Width, h = img.Height;
        var data = img.Data;

        // Premultiply alpha into RGB so blurring near a transparent edge doesn't drag in
        // whatever garbage colour sits in fully-transparent pixels.
        var buf = new double[w * h * 4];
        for (var i = 0; i < w * h; i++)
        {
            var idx = i * 4;
            var a = data[idx + 3] / 255.0;
            buf[idx] = data[idx] * a;
            buf[idx + 1] = data[idx + 1] * a;
            buf[idx + 2] = data[idx + 2] * a;
            buf[idx + 3] = data[idx + 3];
        }

        for (var pass = 0; pass < 3; pass++)
        {
            BoxBlurHorizontal(buf, w, h, radius);
            BoxBlurVertical(buf, w, h, radius);
        }

        for (var i = 0; i < w * h; i++)
        {
            var idx = i * 4;
            var a = buf[idx + 3] / 255.0;
            data[idx] = (byte)Math.Clamp(Math.Round(a > 0.0001 ? buf[idx] / a : 0), 0, 255);
            data[idx + 1] = (byte)Math.Clamp(Math.Round(a > 0.0001 ? buf[idx + 1] / a : 0), 0, 255);
            data[idx + 2] = (byte)Math.Clamp(Math.Round(a > 0.0001 ? buf[idx + 2] / a : 0), 0, 255);
            data[idx + 3] = (byte)Math.Clamp(Math.Round(buf[idx + 3]), 0, 255);
        }
    }

    private static void BoxBlurHorizontal(double[] data, int w, int h, int radius)
    {
        var windowSize = 2 * radius + 1;
        var temp = new double[w * 4];
        for (var y = 0; y < h; y++)
        {
            var rowStart = y * w * 4;
            Span<double> sum = stackalloc double[4];
            for (var x = -radius; x <= radius; x++)
            {
                var xx = Math.Clamp(x, 0, w - 1);
                var idx = rowStart + xx * 4;
                sum[0] += data[idx]; sum[1] += data[idx + 1]; sum[2] += data[idx + 2]; sum[3] += data[idx + 3];
            }
            for (var x = 0; x < w; x++)
            {
                temp[x * 4] = sum[0] / windowSize;
                temp[x * 4 + 1] = sum[1] / windowSize;
                temp[x * 4 + 2] = sum[2] / windowSize;
                temp[x * 4 + 3] = sum[3] / windowSize;

                var addIdx = rowStart + Math.Clamp(x + radius + 1, 0, w - 1) * 4;
                var subIdx = rowStart + Math.Clamp(x - radius, 0, w - 1) * 4;
                sum[0] += data[addIdx] - data[subIdx];
                sum[1] += data[addIdx + 1] - data[subIdx + 1];
                sum[2] += data[addIdx + 2] - data[subIdx + 2];
                sum[3] += data[addIdx + 3] - data[subIdx + 3];
            }
            Array.Copy(temp, 0, data, rowStart, w * 4);
        }
    }

    private static void BoxBlurVertical(double[] data, int w, int h, int radius)
    {
        var windowSize = 2 * radius + 1;
        var temp = new double[h * 4];
        for (var x = 0; x < w; x++)
        {
            Span<double> sum = stackalloc double[4];
            for (var y = -radius; y <= radius; y++)
            {
                var yy = Math.Clamp(y, 0, h - 1);
                var idx = (yy * w + x) * 4;
                sum[0] += data[idx]; sum[1] += data[idx + 1]; sum[2] += data[idx + 2]; sum[3] += data[idx + 3];
            }
            for (var y = 0; y < h; y++)
            {
                temp[y * 4] = sum[0] / windowSize;
                temp[y * 4 + 1] = sum[1] / windowSize;
                temp[y * 4 + 2] = sum[2] / windowSize;
                temp[y * 4 + 3] = sum[3] / windowSize;

                var addIdx = (Math.Clamp(y + radius + 1, 0, h - 1) * w + x) * 4;
                var subIdx = (Math.Clamp(y - radius, 0, h - 1) * w + x) * 4;
                sum[0] += data[addIdx] - data[subIdx];
                sum[1] += data[addIdx + 1] - data[subIdx + 1];
                sum[2] += data[addIdx + 2] - data[subIdx + 2];
                sum[3] += data[addIdx + 3] - data[subIdx + 3];
            }
            for (var y = 0; y < h; y++)
            {
                var idx = (y * w + x) * 4;
                data[idx] = temp[y * 4];
                data[idx + 1] = temp[y * 4 + 1];
                data[idx + 2] = temp[y * 4 + 2];
                data[idx + 3] = temp[y * 4 + 3];
            }
        }
    }
}
