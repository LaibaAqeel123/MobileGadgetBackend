using MobileGadgets.Application.Dtos;

namespace MobileGadgets.Application.Interfaces;

public interface ITiltedDetectionService
{
    /// <summary>Runs the full automatic detection pipeline on one admin-uploaded tilted photo:
    /// finds the case body, fits its true corners from its straight edges, finds the camera-hole
    /// cluster (separated from unrelated hardware like button slots), and builds the lighting
    /// overlay. No admin interaction required for a normal photo — <paramref name="cameraRoundR"/>
    /// and <paramref name="cameraPadPx"/> exist so an admin CAN nudge one model's camera-area
    /// roundness/margin after seeing the preview, for the real photos where the automatic default
    /// isn't quite right (every real module's corner radius genuinely differs a little model to
    /// model). BOTH are PERCENTAGES (0-100), not raw pixel counts — a fixed pixel value was tested
    /// for each and confirmed imperceptible / insufficient on a real multi-thousand-pixel photo
    /// where the module itself is only ~300-400px across; percentage scales correctly regardless
    /// of the source photo's resolution. <paramref name="cameraRoundR"/> is relative to the camera
    /// area's own shorter side (100 = fully rounded, 0 = sharp rectangle).
    /// <paramref name="cameraPadPx"/> is relative to the UNPADDED detected cluster's own shorter
    /// side — confirmed on a real photo: a thin sliver of real case color right where the module
    /// sits close to the case's own edge was invisible at a small margin and fully closed at a
    /// larger one on the SAME photo, i.e. the fix already existed, the default just wasn't scaled
    /// to the photo. <paramref name="coverCameraPlate"/>: false (default) keeps the whole camera
    /// module area — lenses plus the raised plate/trim around them — in its real photographed
    /// color, correct when that trim is a genuinely different material/color from the body; true
    /// prints the design across the plate too, excluding only the individual lens/flash holes
    /// themselves — correct for a one-piece case molded in a single uniform color.</summary>
    Task<TiltedDetectionResult> DetectAsync(
        Stream basePhoto, double cameraRoundR = 25, double cameraPadPx = 15, bool coverCameraPlate = false);
}
