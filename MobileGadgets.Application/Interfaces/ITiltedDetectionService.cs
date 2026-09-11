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
    /// model).</summary>
    Task<TiltedDetectionResult> DetectAsync(Stream basePhoto, double cameraRoundR = 18, double cameraPadPx = 20);
}
