namespace MobileGadgets.Infrastructure.Rendering;

/// <summary>2D geometry + connected-component helpers for the Tilted "line-fit" pipeline
/// (v2 — see TiltedDetectionService.cs). Ground rule this whole file follows, learned from a
/// failed v1 approach (an admin dragging 4 points onto the case's own ambiguous ROUNDED corner,
/// and a camera-hole box built from every dark pixel found anywhere in the photo regardless of
/// distance): never determine geometry from an ambiguous or unrelated feature when an
/// unambiguous, correctly-scoped one is available instead. Corners here are never read directly
/// off the curved corner — they're computed as the intersection of the case's own straight edges,
/// which have no ambiguity. Camera exclusion is never built from every dark pixel in the photo —
/// only from pixels that are close together AND compact in shape (see
/// <see cref="ClusterCompactComponents"/>), which is what actually distinguishes "one camera
/// module" from "a camera module plus an unrelated button slot 40px away."</summary>
public static class Geometry2D
{
    public readonly record struct Point2(double X, double Y);

    /// <summary>A point on a line + its unit direction — the line's own parametrization, not tied
    /// to any particular pair of endpoints, so it survives being re-fit from a different point
    /// cloud each time (see <see cref="FitLineTls"/>) without needing to track "which end is
    /// which."</summary>
    public readonly record struct Line2(Point2 P, Point2 D);

    // ---------- connected components ----------

    /// <summary>4-connected flood fill of the single largest connected region of `mask`. Used for
    /// the case's own body silhouette — a real photo, once thresholded, is occasionally freckled
    /// with a few disconnected stray bright/dark pixels (floor reflections, compression noise);
    /// taking only the largest connected blob throws those away without needing a separate
    /// denoise pass.</summary>
    public static byte[] LargestComponent(byte[] mask, int w, int h)
    {
        var visited = new byte[w * h];
        List<int>? best = null;
        for (var start = 0; start < w * h; start++)
        {
            if (mask[start] == 0 || visited[start] != 0) continue;
            var members = FloodFill(mask, visited, w, h, start);
            if (best is null || members.Count > best.Count) best = members;
        }
        var outMask = new byte[w * h];
        if (best is not null) foreach (var i in best) outMask[i] = 1;
        return outMask;
    }

    /// <summary>Every connected component of `mask` with at least `minArea` pixels — used to find
    /// candidate camera-hole features while throwing out single-pixel noise (a dust speck, a
    /// stray specular catch-light) that would otherwise blow up a convex-hull/min-area-rect fit,
    /// since a hull has to stretch out to cover every last outlier pixel no matter how small
    /// (confirmed directly: one such 1px speck on a real client photo dragged a fitted rectangle
    /// out to include empty space nowhere near the actual camera module).</summary>
    public static List<List<int>> ComponentsAboveArea(byte[] mask, int w, int h, int minArea)
    {
        var visited = new byte[w * h];
        var result = new List<List<int>>();
        for (var start = 0; start < w * h; start++)
        {
            if (mask[start] == 0 || visited[start] != 0) continue;
            var members = FloodFill(mask, visited, w, h, start);
            if (members.Count >= minArea) result.Add(members);
        }
        return result;
    }

    private static List<int> FloodFill(byte[] mask, byte[] visited, int w, int h, int start)
    {
        var stack = new Stack<int>();
        stack.Push(start);
        visited[start] = 1;
        var members = new List<int> { start };
        while (stack.Count > 0)
        {
            var idx = stack.Pop();
            var x = idx % w;
            var y = idx / w;
            if (x > 0) TryVisit(idx - 1);
            if (x < w - 1) TryVisit(idx + 1);
            if (y > 0) TryVisit(idx - w);
            if (y < h - 1) TryVisit(idx + w);
            continue;

            void TryVisit(int n)
            {
                if (mask[n] == 0 || visited[n] != 0) return;
                visited[n] = 1;
                stack.Push(n);
                members.Add(n);
            }
        }
        return members;
    }

    /// <summary>Which pixels of `mask` can be reached from the image border by walking only
    /// through other `mask` pixels — the general "flood-fill from the edges" trick that turns
    /// "is this pixel part of the background" into "is this pixel connected to the border,"
    /// which works for an arbitrarily-shaped background instead of assuming it's a simple
    /// rectangle. A dark pixel that ISN'T reachable this way is walled off by something brighter
    /// on every side — i.e. a real hole in the middle of the case, not the background peeking
    /// through.</summary>
    public static byte[] ReachableFromBorder(byte[] mask, int w, int h)
    {
        var visited = new byte[w * h];
        var stack = new Stack<int>();
        for (var x = 0; x < w; x++)
        {
            Seed(x);
            Seed((h - 1) * w + x);
        }
        for (var y = 0; y < h; y++)
        {
            Seed(y * w);
            Seed(y * w + w - 1);
        }
        while (stack.Count > 0)
        {
            var idx = stack.Pop();
            var x = idx % w;
            var y = idx / w;
            if (x > 0) TryVisit(idx - 1);
            if (x < w - 1) TryVisit(idx + 1);
            if (y > 0) TryVisit(idx - w);
            if (y < h - 1) TryVisit(idx + w);
        }
        return visited;

        void Seed(int idx)
        {
            if (mask[idx] == 0 || visited[idx] != 0) return;
            visited[idx] = 1;
            stack.Push(idx);
        }
        void TryVisit(int n)
        {
            if (mask[n] == 0 || visited[n] != 0) return;
            visited[n] = 1;
            stack.Push(n);
        }
    }

    // ---------- hull / min-area-rect ----------

    /// <summary>Standard monotone-chain convex hull, counter-clockwise, no duplicate closing
    /// point.</summary>
    public static List<Point2> ConvexHull(List<Point2> points)
    {
        var pts = points.Distinct().OrderBy(p => p.X).ThenBy(p => p.Y).ToList();
        if (pts.Count < 3) return pts;

        static double Cross(Point2 o, Point2 a, Point2 b) => (a.X - o.X) * (b.Y - o.Y) - (a.Y - o.Y) * (b.X - o.X);

        var lower = new List<Point2>();
        foreach (var p in pts)
        {
            while (lower.Count >= 2 && Cross(lower[^2], lower[^1], p) <= 0) lower.RemoveAt(lower.Count - 1);
            lower.Add(p);
        }
        var upper = new List<Point2>();
        for (var i = pts.Count - 1; i >= 0; i--)
        {
            var p = pts[i];
            while (upper.Count >= 2 && Cross(upper[^2], upper[^1], p) <= 0) upper.RemoveAt(upper.Count - 1);
            upper.Add(p);
        }
        lower.RemoveAt(lower.Count - 1);
        upper.RemoveAt(upper.Count - 1);
        lower.AddRange(upper);
        return lower;
    }

    /// <summary>Rotating-calipers minimum-area bounding rectangle of a convex hull — tries every
    /// hull edge as a candidate orientation (the true minimum-area rect always shares an edge
    /// with the hull) and keeps the smallest. Returns corners in winding order matching the hull's
    /// own orientation, NOT labelled TL/TR/BR/BL (see <see cref="LabelCorners"/> for that).</summary>
    public static (Point2 C0, Point2 C1, Point2 C2, Point2 C3) MinAreaRect(List<Point2> hull)
    {
        (double Ux, double Uy, double Vx, double Vy, double MinU, double MaxU, double MinV, double MaxV, double Area)? best = null;
        for (var i = 0; i < hull.Count; i++)
        {
            var a = hull[i];
            var b = hull[(i + 1) % hull.Count];
            var ex = b.X - a.X;
            var ey = b.Y - a.Y;
            var len = Math.Sqrt(ex * ex + ey * ey);
            if (len < 1e-9) continue;
            var ux = ex / len;
            var uy = ey / len;
            var vx = -uy;
            var vy = ux;

            double minU = double.MaxValue, maxU = double.MinValue, minV = double.MaxValue, maxV = double.MinValue;
            foreach (var p in hull)
            {
                var u = p.X * ux + p.Y * uy;
                var v = p.X * vx + p.Y * vy;
                if (u < minU) minU = u;
                if (u > maxU) maxU = u;
                if (v < minV) minV = v;
                if (v > maxV) maxV = v;
            }
            var area = (maxU - minU) * (maxV - minV);
            if (best is null || area < best.Value.Area) best = (ux, uy, vx, vy, minU, maxU, minV, maxV, area);
        }
        var r = best!.Value;
        Point2 Corner(double u, double v) => new(u * r.Ux + v * r.Vx, u * r.Uy + v * r.Vy);
        return (Corner(r.MinU, r.MinV), Corner(r.MaxU, r.MinV), Corner(r.MaxU, r.MaxV), Corner(r.MinU, r.MaxV));
    }

    // ---------- line fitting ----------

    /// <summary>Total-least-squares line fit (principal axis via 2x2 covariance eigenvector) —
    /// unlike ordinary least-squares (y = mx + b), this has no trouble with a near-vertical line,
    /// which a case's left/right edge often is.</summary>
    public static Line2 FitLineTls(List<Point2> points)
    {
        var mx = points.Average(p => p.X);
        var my = points.Average(p => p.Y);
        double sxx = 0, sxy = 0, syy = 0;
        foreach (var p in points)
        {
            var dx = p.X - mx;
            var dy = p.Y - my;
            sxx += dx * dx;
            sxy += dx * dy;
            syy += dy * dy;
        }
        var theta = 0.5 * Math.Atan2(2 * sxy, sxx - syy);
        return new Line2(new Point2(mx, my), new Point2(Math.Cos(theta), Math.Sin(theta)));
    }

    public static Point2 LineIntersect(Line2 l1, Line2 l2)
    {
        var denom = l1.D.X * l2.D.Y - l1.D.Y * l2.D.X;
        var t = ((l2.P.X - l1.P.X) * l2.D.Y - (l2.P.Y - l1.P.Y) * l2.D.X) / denom;
        return new Point2(l1.P.X + l1.D.X * t, l1.P.Y + l1.D.Y * t);
    }

    /// <summary>Shifts a line outward (away from `centroid`) by `amount` px along its own normal.
    /// Used two ways: (1) grow the whole traced case outline a few px past the detected
    /// brightness edge, because a threshold-based edge always lands slightly inside the true
    /// physical edge (anti-aliasing blends case color toward the background before a pixel reads
    /// as fully "dark"), which otherwise leaves a thin bare sliver of unprinted case outside the
    /// design; (2) pad a camera exclusion box outward evenly on every side (via all 4 of its
    /// edges, not by pushing each corner toward/away from the center, which under-delivers on the
    /// middle of a long edge).</summary>
    public static Line2 ExpandLineOutward(Line2 line, Point2 centroid, double amount)
    {
        var n = new Point2(-line.D.Y, line.D.X);
        var toLine = new Point2(line.P.X - centroid.X, line.P.Y - centroid.Y);
        if (n.X * toLine.X + n.Y * toLine.Y < 0) n = new Point2(-n.X, -n.Y);
        return new Line2(new Point2(line.P.X + n.X * amount, line.P.Y + n.Y * amount), line.D);
    }

    // ---------- rounded quad membership ----------

    /// <summary>A quad (4 corners, any consistent winding) with rounded corners, as a fast
    /// point-membership test — the shared shape test for BOTH the outer case silhouette (as an
    /// "include" region) and the camera exclusion box (as an "exclude" region). Corner rounding
    /// uses the standard rounded-rectangle construction (inset both adjacent edges by the radius,
    /// corner is cut wherever the pixel falls in the corner's dual-edge zone AND outside the
    /// resulting arc), generalized to a non-axis-aligned, slightly non-rectangular quad by working
    /// in each edge's own local normal rather than assuming a global up/down/left/right.</summary>
    public sealed class RoundedQuadTest
    {
        private readonly Point2[] _corners;
        private readonly (Point2 A, Point2 B)[] _edges;
        private readonly int[] _insideSign;
        private readonly Point2[] _normals;
        private readonly double _roundR;

        public RoundedQuadTest(Point2[] corners, double roundR)
        {
            if (corners.Length != 4) throw new ArgumentException("Exactly 4 corners required.", nameof(corners));
            _corners = corners;
            _roundR = roundR;
            var cx = corners.Average(c => c.X);
            var cy = corners.Average(c => c.Y);
            _edges = new (Point2, Point2)[4];
            for (var i = 0; i < 4; i++) _edges[i] = (corners[i], corners[(i + 1) % 4]);

            _insideSign = new int[4];
            _normals = new Point2[4];
            for (var s = 0; s < 4; s++)
            {
                var sign = Math.Sign(EdgeSignedDist(new Point2(cx, cy), _edges[s].Item1, _edges[s].Item2));
                _insideSign[s] = sign == 0 ? 1 : sign;
                var (a, b) = _edges[s];
                var ex = b.X - a.X;
                var ey = b.Y - a.Y;
                var len = Math.Sqrt(ex * ex + ey * ey);
                _normals[s] = new Point2(-ey / len * _insideSign[s], ex / len * _insideSign[s]);
            }
        }

        private static double EdgeSignedDist(Point2 p, Point2 a, Point2 b)
        {
            var ex = b.X - a.X;
            var ey = b.Y - a.Y;
            var len = Math.Sqrt(ex * ex + ey * ey);
            var nx = -ey / len;
            var ny = ex / len;
            return (p.X - a.X) * nx + (p.Y - a.Y) * ny;
        }

        public bool Contains(double x, double y)
        {
            Span<double> dists = stackalloc double[4];
            for (var s = 0; s < 4; s++)
            {
                dists[s] = EdgeSignedDist(new Point2(x, y), _edges[s].Item1, _edges[s].Item2) * _insideSign[s];
                if (dists[s] < 0) return false;
            }
            for (var c = 0; c < 4; c++)
            {
                var dPrev = dists[(c + 3) % 4];
                var dNext = dists[c];
                if (dPrev >= _roundR || dNext >= _roundR) continue;
                var corner = _corners[c];
                var nPrev = _normals[(c + 3) % 4];
                var nNext = _normals[c];
                var arcCx = corner.X + nPrev.X * _roundR + nNext.X * _roundR;
                var arcCy = corner.Y + nPrev.Y * _roundR + nNext.Y * _roundR;
                if (Math.Sqrt((x - arcCx) * (x - arcCx) + (y - arcCy) * (y - arcCy)) > _roundR) return false;
            }
            return true;
        }
    }

    // ---------- proximity + shape clustering for camera-hole components ----------

    public readonly record struct Bbox(int X0, int Y0, int X1, int Y1)
    {
        public int Width => X1 - X0 + 1;
        public int Height => Y1 - Y0 + 1;
    }

    public static Bbox ComputeBbox(List<int> members, int w)
    {
        int x0 = int.MaxValue, x1 = int.MinValue, y0 = int.MaxValue, y1 = int.MinValue;
        foreach (var idx in members)
        {
            var x = idx % w;
            var y = idx / w;
            if (x < x0) x0 = x;
            if (x > x1) x1 = x;
            if (y < y0) y0 = y;
            if (y > y1) y1 = y;
        }
        return new Bbox(x0, y0, x1, y1);
    }

    /// <summary>Groups dark-enclosed components into clusters using BOTH proximity and shape —
    /// two real bugs, confirmed on real client photos, drove this: (1) two components far apart
    /// (a camera module and an unrelated speaker hole near the bottom of the case) must never
    /// merge just because they're both "enclosed dark," or the resulting exclusion stretches
    /// across the whole gap between them, wiping out real front-face area; (2) a physical side
    /// button slot (long and thin) sitting CLOSE to the camera module — well within any reasonable
    /// proximity threshold — must still never merge with it, because a lens/module cluster is
    /// always close to square while a button slot never is. Only "compact" (aspect ratio &lt;=
    /// <paramref name="aspectLimit"/>) components are allowed to merge with each other by
    /// proximity; every other component becomes its own single-component cluster, still excluded
    /// individually (real hardware — never printed on — just never merged into the big shape).
    /// Returned largest-cluster-first.</summary>
    public static List<List<int>> ClusterCompactComponents(
        List<List<int>> components, int w, double gapPx = 140, double aspectLimit = 2.2)
    {
        var boxes = components.Select(c => ComputeBbox(c, w)).ToList();
        var isCompact = boxes.Select(b => Math.Max(b.Width, b.Height) / (double)Math.Min(b.Width, b.Height) <= aspectLimit).ToList();

        var parent = Enumerable.Range(0, components.Count).ToArray();
        int Find(int i) { while (parent[i] != i) { parent[i] = parent[parent[i]]; i = parent[i]; } return i; }
        void Union(int a, int b) { var ra = Find(a); var rb = Find(b); if (ra != rb) parent[ra] = rb; }

        double BboxGap(Bbox a, Bbox b)
        {
            var dx = Math.Max(Math.Max(a.X0 - b.X1, b.X0 - a.X1), 0);
            var dy = Math.Max(Math.Max(a.Y0 - b.Y1, b.Y0 - a.Y1), 0);
            return Math.Sqrt(dx * dx + dy * dy);
        }

        for (var i = 0; i < components.Count; i++)
            for (var j = i + 1; j < components.Count; j++)
                if (isCompact[i] && isCompact[j] && BboxGap(boxes[i], boxes[j]) < gapPx) Union(i, j);

        var clusters = new Dictionary<int, List<int>>();
        for (var i = 0; i < components.Count; i++)
        {
            var root = Find(i);
            if (!clusters.TryGetValue(root, out var bucket)) clusters[root] = bucket = [];
            bucket.AddRange(components[i]);
        }
        return [.. clusters.Values.OrderByDescending(c => c.Count)];
    }

    /// <summary>Builds a padded, rounded exclusion test around a raw point cloud: fits the
    /// tightest rotated rectangle (not axis-aligned — an axis-aligned box around a rotated cluster
    /// wastes a lot of area and its 90deg corners look like a foreign sticker on a case that has
    /// no square corners anywhere on it), then grows every edge outward evenly by a margin.
    ///
    /// <paramref name="roundnessPercent"/> is a PERCENTAGE (0-100) of the PADDED shape's own
    /// shorter side — confirmed necessary by direct A/B comparison: a fixed pixel value was
    /// imperceptible on a multi-thousand-pixel photo where the module itself is only ~300-400px
    /// across. 100 = fully rounded (a stadium/pill shape on the short axis), 0 = sharp rectangle.
    ///
    /// <paramref name="marginPx"/> is a PLAIN pixel value, deliberately NOT derived from this
    /// shape's own bounding box (that was tried and reverted: it scales wrong the moment a cluster
    /// is WIDE rather than compact — e.g. 3 spread-apart lenses vs. a tight triangle of the same
    /// size — because bounding-box size reflects how far apart the components sit, not how big any
    /// one of them is; confirmed by a real regression where margin ballooned to eat a large chunk
    /// of the case on a wide layout). Callers should derive marginPx from a single component's own
    /// size (see TiltedDetectionService.BuildCameraHoleMask), which stays representative regardless
    /// of how the components are arranged.</summary>
    public static RoundedQuadTest BuildRoundedExclusion(List<int> members, int w, double marginPx, double roundnessPercent)
    {
        var hull = ConvexHull(members.Select(idx => new Point2(idx % w, idx / w)).ToList());
        var (c0, c1, c2, c3) = MinAreaRect(hull);
        var corners = new[] { c0, c1, c2, c3 };
        var cx = corners.Average(c => c.X);
        var cy = corners.Average(c => c.Y);
        var centroid = new Point2(cx, cy);

        var padPx = marginPx;

        var edgeLines = new Line2[4];
        for (var s = 0; s < 4; s++)
        {
            var a = corners[s];
            var b = corners[(s + 1) % 4];
            var ex = b.X - a.X;
            var ey = b.Y - a.Y;
            var len = Math.Sqrt(ex * ex + ey * ey);
            edgeLines[s] = new Line2(a, new Point2(ex / len, ey / len));
        }
        var expanded = edgeLines.Select(l => ExpandLineOutward(l, centroid, padPx)).ToArray();
        var padded = new Point2[4];
        for (var i = 0; i < 4; i++) padded[i] = LineIntersect(expanded[(i + 3) % 4], expanded[i]);

        var sideA = Math.Sqrt(Math.Pow(padded[1].X - padded[0].X, 2) + Math.Pow(padded[1].Y - padded[0].Y, 2));
        var sideB = Math.Sqrt(Math.Pow(padded[3].X - padded[0].X, 2) + Math.Pow(padded[3].Y - padded[0].Y, 2));
        var shorterSide = Math.Min(sideA, sideB);
        var roundRpx = Math.Clamp(roundnessPercent, 0, 100) / 100.0 * (shorterSide / 2);

        return new RoundedQuadTest(padded, roundRpx);
    }
}
