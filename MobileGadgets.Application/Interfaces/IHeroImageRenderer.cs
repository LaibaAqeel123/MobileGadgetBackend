using MobileGadgets.Domain;

namespace MobileGadgets.Application.Interfaces;

public interface IHeroImageRenderer
{
    /// <summary>Renders a full-resolution hero photo: flattens the phone's layers with the
    /// customer's design (Stage A), then places the result in a 3D studio scene (Stage B),
    /// using the given Scene's camera/lighting/colour setup. The printable area is derived from
    /// the base photo's own alpha silhouette minus the camera mask's actual hardware openings —
    /// no separate design-mask image is used. Returns PNG bytes, uncompressed/lossless.</summary>
    Task<byte[]> RenderAsync(Stream baseImage, Stream cameraMask, Stream overlay, Stream design, Scene scene);
}
