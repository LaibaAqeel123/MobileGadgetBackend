namespace MobileGadgets.Application.Interfaces;

public interface IHeroImageRenderer
{
    /// <summary>Renders a full-resolution hero photo: flattens the phone's 4 layers with the
    /// customer's design (Stage A), then places the result in a 3D studio scene (Stage B).
    /// Returns PNG bytes, uncompressed/lossless.</summary>
    Task<byte[]> RenderAsync(Stream baseImage, Stream designMask, Stream cameraMask, Stream overlay, Stream design);
}
