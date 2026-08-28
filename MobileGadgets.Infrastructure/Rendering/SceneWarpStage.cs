using MobileGadgets.Domain;

namespace MobileGadgets.Infrastructure.Rendering;

/// <summary>Stage B: places the flat composite into a 3D-looking studio scene — wall, floor,
/// correct lean/tilt, shadow, floor reflection. Port of prototype_scene_warp.js's shared-camera
/// approach: one virtual camera, everything (floor, wall, phone) projected through it, so
/// nothing looks like a pasted layer. Generic over any flat composite's size — no phone-specific
/// values; the phone's own aspect ratio is the only per-model input. Camera pose and all colours
/// come from the given Scene, so a different Scene changes the look with zero code changes.</summary>
public static class SceneWarpStage
{
    public static RgbaImage RenderScene(RgbaImage flat, Scene scene, int outW = 1500, int outH = 1500)
    {
        var w = flat.Width;
        var h = flat.Height;

        var cam = new SceneCamera(camY: scene.CamY, camZ: scene.CamZ, pitch: scene.PitchDegrees * Math.PI / 180, focal: scene.Focal);

        // World units: phone is 1.0 unit wide, aspect-correct height.
        const double phoneWorldW = 1.0;
        var phoneWorldH = (double)h / w * phoneWorldW;
        var baseCenter = (x: -0.05, z: 0.55);
        var lean = scene.LeanDegrees * Math.PI / 180; // leaning back toward the wall
        var yaw = scene.YawDegrees * Math.PI / 180; // turned for the 3/4 view

        var rightVec = (x: Math.Cos(yaw), z: Math.Sin(yaw));
        var upVec = (y: Math.Cos(lean), z: Math.Sin(lean));

        var baseLeft = (x: baseCenter.x - rightVec.x * (phoneWorldW / 2), y: 0.0, z: baseCenter.z - rightVec.z * (phoneWorldW / 2));
        var baseRight = (x: baseCenter.x + rightVec.x * (phoneWorldW / 2), y: 0.0, z: baseCenter.z + rightVec.z * (phoneWorldW / 2));
        var topLeft = (x: baseLeft.x, y: baseLeft.y + upVec.y * phoneWorldH, z: baseLeft.z + upVec.z * phoneWorldH);
        var topRight = (x: baseRight.x, y: baseRight.y + upVec.y * phoneWorldH, z: baseRight.z + upVec.z * phoneWorldH);

        // Calibrate screen-space scale/offset so the phone fills a good, centered portion of the canvas.
        var rawCorners = new[] { topLeft, topRight, baseRight, baseLeft }
            .Select(p => cam.Project(p.x, p.y, p.z) ?? throw new InvalidOperationException("Phone corner projected behind camera — check scene constants."))
            .ToArray();
        var rxs = rawCorners.Select(p => p.x).ToArray();
        var rys = rawCorners.Select(p => p.y).ToArray();
        var phonePxW = rxs.Max() - rxs.Min();
        var phonePxH = rys.Max() - rys.Min();
        var scale = Math.Min(outW * 0.5 / phonePxW, outH * 0.62 / phonePxH);
        var offX = outW * 0.42 - (rxs.Max() + rxs.Min()) / 2 * scale;
        var offY = outH * 0.42 - (rys.Max() + rys.Min()) / 2 * scale;

        (double x, double y)? ToScreen((double x, double y, double z) p)
        {
            var proj = cam.Project(p.x, p.y, p.z);
            if (proj is null) return null;
            return (proj.Value.x * scale + offX, proj.Value.y * scale + offY);
        }

        // ---- scene canvas, pre-filled with the scene's background gradient ----
        var bgTop = ColorUtils.ParseHex(scene.BackgroundTopColor);
        var bgBottom = ColorUtils.ParseHex(scene.BackgroundBottomColor);
        var sceneImg = new RgbaImage(outW, outH);
        for (var y = 0; y < outH; y++)
        {
            var t = (double)y / outH;
            byte r = (byte)Math.Round(bgTop.r + (bgBottom.r - bgTop.r) * t);
            byte g = (byte)Math.Round(bgTop.g + (bgBottom.g - bgTop.g) * t);
            byte b = (byte)Math.Round(bgTop.b + (bgBottom.b - bgTop.b) * t);
            for (var x = 0; x < outW; x++)
            {
                var idx = (y * outW + x) * 4;
                sceneImg.Data[idx] = r;
                sceneImg.Data[idx + 1] = g;
                sceneImg.Data[idx + 2] = b;
                sceneImg.Data[idx + 3] = 255;
            }
        }

        // ---- floor: near edge close to camera, far edge receding well past the wall ----
        const int floorTexSize = 900;
        var floorTex = SceneTextures.FloorTexture(floorTexSize, floorTexSize, ColorUtils.ParseHex(scene.FloorTopColor), ColorUtils.ParseHex(scene.FloorBottomColor));
        const double floorHalfW = 7, floorNearZ = -0.6, floorFarZ = 6;
        (double x, double y, double z)[] floorCorners3d =
        [
            (-floorHalfW, 0, floorNearZ),
            (floorHalfW, 0, floorNearZ),
            (floorHalfW, 0, floorFarZ),
            (-floorHalfW, 0, floorFarZ),
        ];
        var floorScreen = floorCorners3d.Select(ToScreen).ToArray();
        if (floorScreen.All(p => p is not null))
            PixelOps.WarpInto(sceneImg, floorTex, floorScreen.Select(p => p!.Value).ToArray());

        // ---- wall: simple vertical gradient ----
        const int wallTexW = 40, wallTexH = 900;
        var wallTex = SceneTextures.WallTexture(wallTexW, wallTexH, ColorUtils.ParseHex(scene.WallTopColor), ColorUtils.ParseHex(scene.WallBottomColor));
        var wallZ = floorFarZ * 0.62;
        const double wallHalfW = 7, wallH = 6;
        (double x, double y, double z)[] wallCorners3d =
        [
            (-wallHalfW, wallH, wallZ),
            (wallHalfW, wallH, wallZ),
            (wallHalfW, 0, wallZ),
            (-wallHalfW, 0, wallZ),
        ];
        var wallScreen = wallCorners3d.Select(ToScreen).ToArray();
        if (wallScreen.All(p => p is not null))
            PixelOps.WarpInto(sceneImg, wallTex, wallScreen.Select(p => p!.Value).ToArray());
        // Redraw the floor once more so it wins in front of the wall at the horizon seam.
        if (floorScreen.All(p => p is not null))
            PixelOps.WarpInto(sceneImg, floorTex, floorScreen.Select(p => p!.Value).ToArray());

        // ---- contact shadow, anchored at the phone's real base line ----
        var baseScreenL = ToScreen(baseLeft);
        var baseScreenR = ToScreen(baseRight);
        if (baseScreenL is not null && baseScreenR is not null)
        {
            var midX = (baseScreenL.Value.x + baseScreenR.Value.x) / 2;
            var midY = (baseScreenL.Value.y + baseScreenR.Value.y) / 2;
            var shadowW = Math.Sqrt(Math.Pow(baseScreenR.Value.x - baseScreenL.Value.x, 2) + Math.Pow(baseScreenR.Value.y - baseScreenL.Value.y, 2));

            var shadow = new RgbaImage(outW, outH);
            PixelOps.FillEllipse(shadow, midX, midY + 4, shadowW * 0.62, shadowW * 0.1, 0, 0, 0, 0.55);
            shadow.GaussianBlur(14);
            PixelOps.CompositeOver(sceneImg, shadow);
        }

        // ---- the phone itself, warped from the flat composite into its leaned quad ----
        var phoneScreen = new[] { topLeft, topRight, baseRight, baseLeft }.Select(ToScreen).ToArray();
        if (phoneScreen.All(p => p is not null))
        {
            var phoneCorners = phoneScreen.Select(p => p!.Value).ToArray();

            // Directional shading: darker toward the far/right edge (fakes the side surface's own light).
            var shaded = flat.Clone();
            for (var i = 0; i < w * h; i++)
            {
                var x = i % w;
                var t = (double)x / w;
                var mult = 1.06 - 0.22 * t;
                var idx = i * 4;
                shaded.Data[idx] = (byte)Math.Clamp(Math.Round(shaded.Data[idx] * mult), 0, 255);
                shaded.Data[idx + 1] = (byte)Math.Clamp(Math.Round(shaded.Data[idx + 1] * mult), 0, 255);
                shaded.Data[idx + 2] = (byte)Math.Clamp(Math.Round(shaded.Data[idx + 2] * mult), 0, 255);
            }

            PixelOps.WarpInto(sceneImg, shaded, phoneCorners);

            // ---- floor reflection: the phone mirrored across its own base line, faded + blurred ----
            var mirroredTopLeft = (x: baseLeft.x, y: -upVec.y * phoneWorldH * 0.9, z: baseLeft.z - upVec.z * phoneWorldH * 0.9);
            var mirroredTopRight = (x: baseRight.x, y: -upVec.y * phoneWorldH * 0.9, z: baseRight.z - upVec.z * phoneWorldH * 0.9);
            var reflectionScreen = new[] { ToScreen(mirroredTopLeft), ToScreen(mirroredTopRight), ToScreen(baseRight), ToScreen(baseLeft) };
            if (reflectionScreen.All(p => p is not null))
            {
                var faded = flat.Clone();
                for (var i = 0; i < w * h; i++)
                    faded.Data[i * 4 + 3] = (byte)Math.Round(faded.Data[i * 4 + 3] * 0.22);

                var reflBuf = new RgbaImage(outW, outH);
                PixelOps.WarpInto(reflBuf, faded, reflectionScreen.Select(p => p!.Value).ToArray());
                reflBuf.GaussianBlur(3);
                PixelOps.CompositeOver(sceneImg, reflBuf);

                // Redraw the phone on top of its own reflection edge for a clean contact line.
                PixelOps.WarpInto(sceneImg, shaded, phoneCorners);
            }
        }

        return sceneImg;
    }
}
