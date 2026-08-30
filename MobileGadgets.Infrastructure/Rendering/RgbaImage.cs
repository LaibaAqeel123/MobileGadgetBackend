using ImageMagick;
using ImageMagick.Drawing;

namespace MobileGadgets.Infrastructure.Rendering;

/// <summary>Raw RGBA pixel buffer (0-255 per channel), row-major, 4 bytes/pixel.</summary>
public class RgbaImage
{
    public byte[] Data { get; }
    public int Width { get; }
    public int Height { get; }

    public RgbaImage(int width, int height)
    {
        Width = width;
        Height = height;
        Data = new byte[width * height * 4];
    }

    private RgbaImage(byte[] data, int width, int height)
    {
        Data = data;
        Width = width;
        Height = height;
    }

    public static async Task<RgbaImage> LoadAsync(Stream stream)
    {
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms);
        using var image = new MagickImage(ms.ToArray());
        return FromMagickImage(image);
    }

    /// <summary>Loads and resizes to (width, height) using cover-fit (scale to cover, crop excess) — same as sharp's fit:"cover".</summary>
    public static async Task<RgbaImage> LoadCoverFitAsync(Stream stream, int width, int height)
    {
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms);
        using var image = new MagickImage(ms.ToArray());
        CoverFit(image, width, height);
        return FromMagickImage(image);
    }

    private static void CoverFit(MagickImage image, int width, int height)
    {
        var scale = Math.Max((double)width / image.Width, (double)height / image.Height);
        var newW = (uint)Math.Ceiling(image.Width * scale);
        var newH = (uint)Math.Ceiling(image.Height * scale);
        image.Resize(new MagickGeometry(newW, newH) { IgnoreAspectRatio = true });

        var x = ((int)newW - width) / 2;
        var y = ((int)newH - height) / 2;
        image.Crop(new MagickGeometry((uint)width, (uint)height) { X = x, Y = y });
    }

    private static RgbaImage FromMagickImage(MagickImage image)
    {
        image.Alpha(AlphaOption.On);
        image.Depth = 8;
        using var pixels = image.GetPixels();
        var data = pixels.ToByteArray(PixelMapping.RGBA)
            ?? throw new InvalidOperationException("Failed to read pixel data.");
        return new RgbaImage(data, (int)image.Width, (int)image.Height);
    }

    private MagickImage ToMagickImage()
    {
        var settings = new PixelReadSettings((uint)Width, (uint)Height, StorageType.Char, PixelMapping.RGBA);
        return new MagickImage(Data, settings);
    }

    /// <summary>Stretches to an exact size (no cropping) — used to defensively align mask/overlay
    /// layers that should already match the base photo's canvas but might not exactly.</summary>
    public RgbaImage ResizeExact(int width, int height)
    {
        if (width == Width && height == Height) return this;
        using var image = ToMagickImage();
        image.Resize(new MagickGeometry((uint)width, (uint)height) { IgnoreAspectRatio = true });
        return FromMagickImage(image);
    }

    public RgbaImage Clone()
    {
        var copy = new byte[Data.Length];
        Array.Copy(Data, copy, Data.Length);
        return new RgbaImage(copy, Width, Height);
    }

    public byte R(int i) => Data[i * 4];
    public byte G(int i) => Data[i * 4 + 1];
    public byte B(int i) => Data[i * 4 + 2];
    public byte A(int i) => Data[i * 4 + 3];

    public Task<byte[]> ToPngBytesAsync()
    {
        using var image = ToMagickImage();
        image.Format = MagickFormat.Png;
        return Task.FromResult(image.ToByteArray());
    }

    public void GaussianBlur(double sigma) => BoxBlur.Apply(this, sigma);

    /// <summary>A grayscale (opaque, alpha=255) copy where R=G=B=this image's own alpha channel —
    /// blurring this and comparing back against the original alpha gives a soft "how close to the
    /// silhouette edge" band for any shape, with no hardcoded geometry (used for the case's rim
    /// light).</summary>
    public RgbaImage ExtractAlphaAsGray()
    {
        var img = new RgbaImage(Width, Height);
        for (var i = 0; i < Width * Height; i++)
        {
            var a = A(i);
            var idx = i * 4;
            img.Data[idx] = a;
            img.Data[idx + 1] = a;
            img.Data[idx + 2] = a;
            img.Data[idx + 3] = 255;
        }
        return img;
    }

    /// <summary>Draws opaque/translucent text directly onto this image (bottom-left anchored at
    /// (x, y), matching MagickImage's default text gravity). Round-trips through Magick.NET since
    /// this class has no native font rasterizer of its own.
    ///
    /// Confirmed by direct testing: MagickImage.Draw() silently no-ops (no exception, nothing
    /// rendered) on an image built via ToMagickImage()'s raw-pixel-import constructor — but works
    /// normally once that image has been round-tripped through an actual image format (PNG here).
    /// So this re-decodes from PNG bytes rather than drawing directly on the raw-imported image.</summary>
    public void DrawText(string text, double x, double y, double pointSize, byte r, byte g, byte b, double opacity)
    {
        using var raw = ToMagickImage();
        raw.Format = MagickFormat.Png;
        var pngBytes = raw.ToByteArray();

        using var image = new MagickImage(pngBytes);
        image.Alpha(AlphaOption.On);

        // MagickColor's byte-component constructor does NOT scale into this build's Q16 (0-65535)
        // quantum range — it stores the raw byte value as-is, so (255,255,255,255) becomes
        // ~0.4% brightness/opacity (255/65535), not opaque white. Confirmed by direct comparison
        // against MagickColors.White (R=G=B=A=65535). Scale byte -> ushort (x257) explicitly.
        static ushort ScaleToQuantum(byte v) => (ushort)(v * 257);
        var alphaByte = (byte)Math.Round(Math.Clamp(opacity, 0, 1) * 255);
        var color = new MagickColor(ScaleToQuantum(r), ScaleToQuantum(g), ScaleToQuantum(b), ScaleToQuantum(alphaByte));

        var drawables = new Drawables()
            .FontPointSize(pointSize)
            .FillColor(color)
            .Text(x, y, text);
        image.Draw(drawables);

        image.Depth = 8;
        using var pixels = image.GetPixels();
        var data = pixels.ToByteArray(PixelMapping.RGBA)
            ?? throw new InvalidOperationException("Failed to read pixel data after drawing text.");
        Array.Copy(data, Data, Data.Length);
    }
}
