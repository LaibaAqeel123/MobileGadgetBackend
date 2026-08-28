namespace MobileGadgets.Infrastructure.Rendering;

/// <summary>3x3 perspective matrix, row-major (m[0..2]=row0, m[3..5]=row1, m[6..8]=row2).
/// Direct port of the prototype's DLT solve + Gaussian elimination — same math real camera
/// lenses and 3D engines use to map 4 source corners to 4 destination corners.</summary>
public static class Homography
{
    public static double[] Solve((double x, double y)[] src, (double x, double y)[] dst)
    {
        var a = new double[8][];
        var b = new double[8];
        for (var i = 0; i < 4; i++)
        {
            var (x, y) = src[i];
            var (u, v) = dst[i];
            a[i * 2] = [x, y, 1, 0, 0, 0, -x * u, -y * u];
            b[i * 2] = u;
            a[i * 2 + 1] = [0, 0, 0, x, y, 1, -x * v, -y * v];
            b[i * 2 + 1] = v;
        }

        // Augment and solve via Gaussian elimination with partial pivoting.
        var aug = new double[8][];
        for (var i = 0; i < 8; i++)
        {
            aug[i] = new double[9];
            Array.Copy(a[i], aug[i], 8);
            aug[i][8] = b[i];
        }

        for (var col = 0; col < 8; col++)
        {
            var pivot = col;
            for (var r = col + 1; r < 8; r++)
                if (Math.Abs(aug[r][col]) > Math.Abs(aug[pivot][col])) pivot = r;
            (aug[col], aug[pivot]) = (aug[pivot], aug[col]);

            for (var r = 0; r < 8; r++)
            {
                if (r == col) continue;
                var factor = aug[r][col] / aug[col][col];
                for (var c = col; c < 9; c++) aug[r][c] -= factor * aug[col][c];
            }
        }

        var h = new double[9];
        for (var i = 0; i < 8; i++) h[i] = aug[i][8] / aug[i][i];
        h[8] = 1;
        return h;
    }

    public static double[] Invert3x3(double[] m)
    {
        double a = m[0], b = m[1], c = m[2], d = m[3], e = m[4], f = m[5], g = m[6], h = m[7], i = m[8];
        var A = e * i - f * h;
        var B = -(d * i - f * g);
        var C = d * h - e * g;
        var D = -(b * i - c * h);
        var E = a * i - c * g;
        var F = -(a * h - b * g);
        var G = b * f - c * e;
        var H = -(a * f - c * d);
        var I = a * e - b * d;
        var det = a * A + b * B + c * C;
        return [A / det, D / det, G / det, B / det, E / det, H / det, C / det, F / det, I / det];
    }

    public static (double x, double y) Apply(double[] m, double x, double y)
    {
        var w = m[6] * x + m[7] * y + m[8];
        return ((m[0] * x + m[1] * y + m[2]) / w, (m[3] * x + m[4] * y + m[5]) / w);
    }
}
