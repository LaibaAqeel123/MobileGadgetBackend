using ImageMagick;

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
}
