namespace MobileGadgets.Infrastructure.Rendering;

public static class ColorUtils
{
    public static (byte r, byte g, byte b) ParseHex(string hex)
    {
        hex = hex.TrimStart('#');
        return (
            Convert.ToByte(hex[..2], 16),
            Convert.ToByte(hex[2..4], 16),
            Convert.ToByte(hex[4..6], 16)
        );
    }
}
