using MobileGadgets.Application.Interfaces;

namespace MobileGadgets.Infrastructure.Rendering;

public class HeroImageRenderer : IHeroImageRenderer
{
    public async Task<byte[]> RenderAsync(Stream baseImage, Stream designMask, Stream cameraMask, Stream overlay, Stream design)
    {
        var baseImg = await RgbaImage.LoadAsync(baseImage);
        var designMaskImg = await RgbaImage.LoadAsync(designMask);
        var cameraMaskImg = await RgbaImage.LoadAsync(cameraMask);
        var overlayImg = await RgbaImage.LoadAsync(overlay);
        var designImg = await RgbaImage.LoadCoverFitAsync(design, baseImg.Width, baseImg.Height);

        var flat = FlattenStage.Flatten(baseImg, designMaskImg, cameraMaskImg, overlayImg, designImg);
        var scene = SceneWarpStage.RenderScene(flat);

        return await scene.ToPngBytesAsync();
    }
}
