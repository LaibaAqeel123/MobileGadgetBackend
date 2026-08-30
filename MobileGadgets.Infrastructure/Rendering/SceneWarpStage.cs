using MobileGadgets.Domain;

namespace MobileGadgets.Infrastructure.Rendering;

/// <summary>Stage B: places the flat composite into a studio scene — wall, floor, a near-upright
/// resting pose, grounded shadow, and a light "photographed" pass (background depth-of-field
/// blur, a real key light + edge rim-light on the case instead of a flat brightness ramp, a
/// corner vignette, film grain, brand watermark). Ported from the `room_polish_v1`/`room_straight_
/// slight` prototypes in phone-case-mockup-studio/tools — an earlier, more dramatic diagonal-tilt
/// pose was tried and explicitly rejected (read as fake/disgusting); centred, mostly-upright, and
/// "photographed" reads far better for social use than a forced 3D lean. No floor reflection —
/// dropped along with the tilt experiment, not worth its cost for this look.</summary>
public static class SceneWarpStage
{
    // TODO: make this an admin-configurable brand name (planned settings feature) instead of a
    // hardcoded string once that admin panel work happens.
    private const string WatermarkText = "MOBILE GADGETS";

    public static RgbaImage RenderScene(RgbaImage flat, Scene scene, int outW = 1500, int outH = 1500)
    {
        var w = flat.Width;
        var h = flat.Height;

        var cam = new SceneCamera(camY: scene.CamY, camZ: scene.CamZ, pitch: scene.PitchDegrees * Math.PI / 180, focal: scene.Focal);

        // World units: phone is 1.0 unit wide, aspect-correct height.
        const double phoneWorldW = 1.0;
        var phoneWorldH = (double)h / w * phoneWorldW;
        var baseCenter = (x: -0.05, z: 0.55);
        var lean = scene.LeanDegrees * Math.PI / 180; // near-upright by default — see Scene seed data
        var yaw = scene.YawDegrees * Math.PI / 180;

        var rightVec = (x: Math.Cos(yaw), z: Math.Sin(yaw));
        var upVec = (y: Math.Cos(lean), z: Math.Sin(lean));

        var baseLeft = (x: baseCenter.x - rightVec.x * (phoneWorldW / 2), y: 0.0, z: baseCenter.z - rightVec.z * (phoneWorldW / 2));
        var baseRight = (x: baseCenter.x + rightVec.x * (phoneWorldW / 2), y: 0.0, z: baseCenter.z + rightVec.z * (phoneWorldW / 2));
        var topLeft = (x: baseLeft.x, y: baseLeft.y + upVec.y * phoneWorldH, z: baseLeft.z + upVec.z * phoneWorldH);
        var topRight = (x: baseRight.x, y: baseRight.y + upVec.y * phoneWorldH, z: baseRight.z + upVec.z * phoneWorldH);

        var rawCorners = new[] { topLeft, topRight, baseRight, baseLeft }
            .Select(p => cam.Project(p.x, p.y, p.z) ?? throw new InvalidOperationException("Phone corner projected behind camera — check scene constants."))
            .ToArray();
        var rxs = rawCorners.Select(p => p.x).ToArray();
        var rys = rawCorners.Select(p => p.y).ToArray();
        var phonePxW = rxs.Max() - rxs.Min();
        var phonePxH = rys.Max() - rys.Min();

        // Bigger and bottom-anchored (via the max, not the midpoint) so the phone dominates the
        // frame and stands near the bottom like a real product shot, instead of floating small
        // and centered with a lot of unused space.
        var scale = Math.Min(outW * 0.56 / phonePxW, outH * 0.74 / phonePxH);
        var offX = outW * 0.46 - (rxs.Max() + rxs.Min()) / 2 * scale;
        var offY = outH * 0.58 - (rys.Max() + rys.Min()) / 2 * scale;

        (double x, double y)? ToScreen((double x, double y, double z) p)
        {
            var proj = cam.Project(p.x, p.y, p.z);
            if (proj is null) return null;
            return (proj.Value.x * scale + offX, proj.Value.y * scale + offY);
        }

        // ---- background (gradient + floor + wall + floor redrawn on top), built in its own
        // buffer so it can be blurred BEFORE the sharp phone is composited on top — real
        // depth-of-field is what actually sells "photographed," a crisp gradient does not. ----
        var sceneImg = new RgbaImage(outW, outH);
        var bgTop = ColorUtils.ParseHex(scene.BackgroundTopColor);
        var bgBottom = ColorUtils.ParseHex(scene.BackgroundBottomColor);
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

        const int floorTexSize = 900;
        var floorTex = SceneTextures.FloorTexture(floorTexSize, floorTexSize, ColorUtils.ParseHex(scene.FloorTopColor), ColorUtils.ParseHex(scene.FloorBottomColor));
        const double floorHalfW = 7, floorNearZ = -0.6, floorFarZ = 7;
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

        const int wallTexW = 40, wallTexH = 900;
        var wallTex = SceneTextures.WallTexture(wallTexW, wallTexH, ColorUtils.ParseHex(scene.WallTopColor), ColorUtils.ParseHex(scene.WallBottomColor));
        var wallZ = floorFarZ * 0.6;
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
        if (floorScreen.All(p => p is not null))
            PixelOps.WarpInto(sceneImg, floorTex, floorScreen.Select(p => p!.Value).ToArray());

        sceneImg.GaussianBlur(9);

        // ---- two-layer shadow (soft cast + tight contact), anchored at the phone's real base
        // corners — grounding follows the actual geometry, never used to paper over a bad pose. ----
        var baseScreenL = ToScreen(baseLeft);
        var baseScreenR = ToScreen(baseRight);
        if (baseScreenL is not null && baseScreenR is not null)
        {
            var midX = (baseScreenL.Value.x + baseScreenR.Value.x) / 2;
            var midY = (baseScreenL.Value.y + baseScreenR.Value.y) / 2;
            var shadowW = Math.Sqrt(Math.Pow(baseScreenR.Value.x - baseScreenL.Value.x, 2) + Math.Pow(baseScreenR.Value.y - baseScreenL.Value.y, 2));

            var cast = new RgbaImage(outW, outH);
            PixelOps.FillEllipse(cast, midX + shadowW * 0.1, midY + 9, shadowW * 0.72, shadowW * 0.13, 0, 0, 0, 0.36);
            cast.GaussianBlur(20);
            PixelOps.CompositeOver(sceneImg, cast);

            var contact = new RgbaImage(outW, outH);
            PixelOps.FillEllipse(contact, midX, midY + 1, shadowW * 0.48, shadowW * 0.055, 0, 0, 0, 0.85);
            contact.GaussianBlur(3);
            PixelOps.CompositeOver(sceneImg, contact);
        }

        // ---- the phone: a real radial key light + base ambient-occlusion + an edge rim-light
        // (computed from the case's own alpha silhouette, not a hardcoded shape) — replaces the
        // old flat left-to-right brightness ramp, which is what made every earlier render read
        // as a flat image with a filter on it instead of something actually lit. ----
        var phoneScreen = new[] { topLeft, topRight, baseRight, baseLeft }.Select(ToScreen).ToArray();
        if (phoneScreen.All(p => p is not null))
        {
            var alphaGray = flat.ExtractAlphaAsGray();
            var rimSigma = Math.Max(6, w * 0.018);
            alphaGray.GaussianBlur(rimSigma);

            var shaded = flat.Clone();
            const double lightCx = 0.32, lightCy = 0.16; // upper-left softbox, normalized to case bounds
            for (var i = 0; i < w * h; i++)
            {
                var x = i % w;
                var y = i / w;
                var tx = (double)x / w;
                var ty = (double)y / h;

                var dist = Math.Sqrt(Math.Pow((tx - lightCx) * 0.85, 2) + Math.Pow(ty - lightCy, 2));
                var keyMult = 1.16 - 0.32 * Math.Min(1, dist / 0.95);

                var mult = keyMult;
                const double aoStart = 0.9;
                if (ty > aoStart) mult *= 1 - 0.14 * ((ty - aoStart) / (1 - aoStart));

                var idx = i * 4;
                double r = shaded.Data[idx] * mult, g = shaded.Data[idx + 1] * mult, b = shaded.Data[idx + 2] * mult;

                var origA = flat.A(i) / 255.0;
                var blurA = alphaGray.R(i) / 255.0;
                var rim = Math.Max(0, origA * (1 - blurA));
                var bias = 0.25 + 0.75 * Math.Max(0, 1 - (tx * 0.55 + ty * 0.5));
                var rimAdd = rim * bias * 70;
                r += rimAdd; g += rimAdd; b += rimAdd;

                shaded.Data[idx] = (byte)Math.Clamp(Math.Round(r), 0, 255);
                shaded.Data[idx + 1] = (byte)Math.Clamp(Math.Round(g), 0, 255);
                shaded.Data[idx + 2] = (byte)Math.Clamp(Math.Round(b), 0, 255);
            }

            PixelOps.WarpInto(sceneImg, shaded, phoneScreen.Select(p => p!.Value).ToArray());
        }

        // ---- corner vignette: draws the eye to the product, present in nearly every real
        // studio product photo whether the photographer added it on purpose or the lens just
        // does it naturally. ----
        PixelOps.ApplyVignette(sceneImg, cxFrac: 0.5, cyFrac: 0.46, innerFrac: 0.55, maxOpacity: 0.42);

        // ---- film grain: kills the "flat digital gradient" look far more cheaply than any
        // amount of extra shading detail would. ----
        PixelOps.ApplyFilmGrain(sceneImg, amplitude: 12, new Random());

        // ---- brand watermark, bottom-left. ----
        sceneImg.DrawText(WatermarkText, 46, outH - 46, 30, 255, 255, 255, 0.78);

        return sceneImg;
    }
}
