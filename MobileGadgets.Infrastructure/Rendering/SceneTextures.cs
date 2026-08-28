namespace MobileGadgets.Infrastructure.Rendering;

/// <summary>Floor/wall textures — a gradient + a light grid, not hand-painted. Warped into the
/// 3D scene through the same camera as the phone, so the grid lines correctly converge into
/// the distance instead of being a fake flat gradient. Colours come from the active Scene.</summary>
public static class SceneTextures
{
    public static RgbaImage FloorTexture(int w, int h, (byte r, byte g, byte b) top, (byte r, byte g, byte b) bottom)
    {
        var img = new RgbaImage(w, h);

        for (var y = 0; y < h; y++)
        {
            var t = (double)y / h;
            byte r = (byte)Math.Round(top.r + (bottom.r - top.r) * t);
            byte g = (byte)Math.Round(top.g + (bottom.g - top.g) * t);
            byte b = (byte)Math.Round(top.b + (bottom.b - top.b) * t);
            for (var x = 0; x < w; x++)
            {
                var idx = (y * w + x) * 4;
                img.Data[idx] = r;
                img.Data[idx + 1] = g;
                img.Data[idx + 2] = b;
                img.Data[idx + 3] = 255;
            }
        }

        // Vertical grid lines (9, every 1/8 of width), faint dark-on-light or light-on-dark
        // depending on which reads better against this scene's floor tone.
        var lineIsLight = (top.r + top.g + top.b) < 384; // dark floor -> light grid lines
        for (var i = 0; i <= 8; i++)
        {
            var x = (int)Math.Round(i * w / 8.0);
            DrawVerticalLine(img, x, 0.05, lineIsLight);
        }
        for (var i = 0; i <= 8; i++)
        {
            var y = (int)Math.Round(i * h / 8.0);
            DrawHorizontalLine(img, y, 0.04, lineIsLight);
        }

        return img;
    }

    public static RgbaImage WallTexture(int w, int h, (byte r, byte g, byte b) top, (byte r, byte g, byte b) bottom)
    {
        var img = new RgbaImage(w, h);

        for (var y = 0; y < h; y++)
        {
            var t = (double)y / h;
            byte r = (byte)Math.Round(top.r + (bottom.r - top.r) * t);
            byte g = (byte)Math.Round(top.g + (bottom.g - top.g) * t);
            byte b = (byte)Math.Round(top.b + (bottom.b - top.b) * t);
            for (var x = 0; x < w; x++)
            {
                var idx = (y * w + x) * 4;
                img.Data[idx] = r;
                img.Data[idx + 1] = g;
                img.Data[idx + 2] = b;
                img.Data[idx + 3] = 255;
            }
        }

        return img;
    }

    private static void DrawVerticalLine(RgbaImage img, int x, double opacity, bool lineIsLight)
    {
        for (var t = -1; t <= 1; t++)
        {
            var xx = x + t;
            if (xx < 0 || xx >= img.Width) continue;
            for (var y = 0; y < img.Height; y++)
                BlendLine(img, (y * img.Width + xx) * 4, opacity, lineIsLight);
        }
    }

    private static void DrawHorizontalLine(RgbaImage img, int y, double opacity, bool lineIsLight)
    {
        if (y < 0 || y >= img.Height) return;
        for (var x = 0; x < img.Width; x++)
            BlendLine(img, (y * img.Width + x) * 4, opacity, lineIsLight);
    }

    private static void BlendLine(RgbaImage img, int idx, double opacity, bool lineIsLight)
    {
        var target = lineIsLight ? 255 : 0;
        img.Data[idx] = (byte)Math.Round(target * opacity + img.Data[idx] * (1 - opacity));
        img.Data[idx + 1] = (byte)Math.Round(target * opacity + img.Data[idx + 1] * (1 - opacity));
        img.Data[idx + 2] = (byte)Math.Round(target * opacity + img.Data[idx + 2] * (1 - opacity));
    }
}
