using MobileGadgets.Application.Interfaces;
using MobileGadgets.Domain;

namespace MobileGadgets.Infrastructure.Rendering;

public class HeroImageRenderer : IHeroImageRenderer
{
    public async Task<byte[]> RenderAsync(Stream baseImage, Stream cameraMask, Stream overlay, Stream design, Scene scene, Stream? backgroundImage = null)
    {
        var baseImg = await RgbaImage.LoadAsync(baseImage);
        var cameraMaskImg = await RgbaImage.LoadAsync(cameraMask);
        var overlayImg = await RgbaImage.LoadAsync(overlay);
        var designImg = await RgbaImage.LoadCoverFitAsync(design, baseImg.Width, baseImg.Height);

        var flat = FlattenStage.Flatten(baseImg, cameraMaskImg, overlayImg, designImg);

        const int outW = 1500, outH = 1500;
        var backgroundImg = backgroundImage is null ? null : await RgbaImage.LoadCoverFitAsync(backgroundImage, outW, outH);

        var sceneImage = SceneWarpStage.RenderScene(flat, scene, backgroundImg, outW, outH);

        return await sceneImage.ToPngBytesAsync();
    }
}
