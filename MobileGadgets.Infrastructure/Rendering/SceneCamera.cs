namespace MobileGadgets.Infrastructure.Rendering;

/// <summary>A single virtual camera every scene object (phone, floor, wall) projects through,
/// so they all agree on the same 3D world instead of looking like pasted layers.
/// World axes: X = right, Y = up, Z = depth (increasing away from camera).</summary>
public class SceneCamera
{
    private readonly double _camY, _camZ, _focal, _cp, _sp;

    public SceneCamera(double camY, double camZ, double pitch, double focal)
    {
        _camY = camY;
        _camZ = camZ;
        _focal = focal;
        _cp = Math.Cos(-pitch);
        _sp = Math.Sin(-pitch);
    }

    public (double x, double y)? Project(double x, double y, double z)
    {
        var relY = y - _camY;
        var relZ = z - _camZ;
        var y2 = relY * _cp - relZ * _sp;
        var z2 = relY * _sp + relZ * _cp;
        if (z2 <= 0.05) return null; // behind camera
        return (_focal * x / z2, -_focal * y2 / z2);
    }
}
