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

    /// <summary>Same padded/rounded-rect construction as <see cref="BuildRoundedExclusion"/>, but
    /// returns the actual boundary as an ordered list of points instead of a membership test —
    /// used to SEED the admin's editable outline with a real starting shape (walks the 4 straight
    /// edges and 4 corner arcs, emitting evenly spaced points) so they're nudging a already-close
    /// guess, never drawing from nothing.</summary>
    public static List<Point2> SampleRoundedQuadBoundary(List<int> members, int w, double marginPx, double roundnessPercent, int totalPoints = 20)
    {
        var hull = ConvexHull(members.Select(idx => new Point2(idx % w, idx / w)).ToList());
        var (c0, c1, c2, c3) = MinAreaRect(hull);
        var corners = new[] { c0, c1, c2, c3 };
        var cx = corners.Average(c => c.X);
        var cy = corners.Average(c => c.Y);
        var centroid = new Point2(cx, cy);

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
        var expanded = edgeLines.Select(l => ExpandLineOutward(l, centroid, marginPx)).ToArray();
        var padded = new Point2[4];
        for (var i = 0; i < 4; i++) padded[i] = LineIntersect(expanded[(i + 3) % 4], expanded[i]);

        var sideA = Math.Sqrt(Math.Pow(padded[1].X - padded[0].X, 2) + Math.Pow(padded[1].Y - padded[0].Y, 2));
        var sideB = Math.Sqrt(Math.Pow(padded[3].X - padded[0].X, 2) + Math.Pow(padded[3].Y - padded[0].Y, 2));
        var roundR = Math.Clamp(roundnessPercent, 0, 100) / 100.0 * (Math.Min(sideA, sideB) / 2);

        // Walk the shape's perimeter (straight edge, corner arc, straight edge, ...) and drop
        // `totalPoints` evenly spaced along it by arc length, so a heavily-rounded corner gets a
        // fair share of points too, not just the straight runs. Per-corner tangent points (R back
        // along the incoming edge, R forward along the outgoing edge) and arc center (corner +
        // both adjacent inward normals * R -- exact for a rectangle's corners, which this padded
        // quad always is) are the same construction RoundedQuadTest already uses successfully;
        // this just walks that same geometry instead of testing membership against it.
        Point2 InwardNormal(Point2 a, Point2 b)
        {
            var ex = b.X - a.X; var ey = b.Y - a.Y;
            var len = Math.Sqrt(ex * ex + ey * ey);
            var nx = -ey / len; var ny = ex / len;
            return ((cx - a.X) * nx + (cy - a.Y) * ny) >= 0 ? new Point2(nx, ny) : new Point2(-nx, -ny);
        }

        var tIn = new Point2[4];
        var tOut = new Point2[4];
        var arcCenter = new Point2[4];
        for (var c = 0; c < 4; c++)
        {
            var prev = padded[(c + 3) % 4];
            var here = padded[c];
            var next = padded[(c + 1) % 4];
            var uPrev = new Point2((here.X - prev.X), (here.Y - prev.Y));
            var lenPrev = Math.Sqrt(uPrev.X * uPrev.X + uPrev.Y * uPrev.Y);
            uPrev = new Point2(uPrev.X / lenPrev, uPrev.Y / lenPrev);
            var uNext = new Point2((next.X - here.X), (next.Y - here.Y));
            var lenNext = Math.Sqrt(uNext.X * uNext.X + uNext.Y * uNext.Y);
            uNext = new Point2(uNext.X / lenNext, uNext.Y / lenNext);

            tIn[c] = new Point2(here.X - uPrev.X * roundR, here.Y - uPrev.Y * roundR);
            tOut[c] = new Point2(here.X + uNext.X * roundR, here.Y + uNext.Y * roundR);
            var nPrev = InwardNormal(prev, here);
            var nNext = InwardNormal(here, next);
            arcCenter[c] = new Point2(here.X + nPrev.X * roundR + nNext.X * roundR, here.Y + nPrev.Y * roundR + nNext.Y * roundR);
        }

        var segments = new List<(Point2 Start, Point2 End, double Length, bool IsArc, Point2 ArcCenter, double A0, double A1)>();
        for (var c = 0; c < 4; c++)
        {
            var straightStart = tOut[c];
            var straightEnd = tIn[(c + 1) % 4];
            var dx = straightEnd.X - straightStart.X;
            var dy = straightEnd.Y - straightStart.Y;
            segments.Add((straightStart, straightEnd, Math.Sqrt(dx * dx + dy * dy), false, default, 0, 0));

            if (roundR > 0.01)
            {
                var next = (c + 1) % 4;
                var a0 = Math.Atan2(tIn[next].Y - arcCenter[next].Y, tIn[next].X - arcCenter[next].X);
                var a1 = Math.Atan2(tOut[next].Y - arcCenter[next].Y, tOut[next].X - arcCenter[next].X);
                var delta = a1 - a0;
                while (delta <= -Math.PI) delta += 2 * Math.PI;
                while (delta > Math.PI) delta -= 2 * Math.PI;
                segments.Add((tIn[next], tOut[next], Math.Abs(delta) * roundR, true, arcCenter[next], a0, a0 + delta));
            }
        }

        var totalLen = segments.Sum(s => s.Length);
        if (totalLen < 1e-6) return [padded[0], padded[1], padded[2], padded[3]];

        var result = new List<Point2>();
        var step = totalLen / totalPoints;
        for (var i = 0; i < totalPoints; i++)
        {
            var target = i * step;
            var acc = 0.0;
            var seg = segments[^1];
            var localT = 1.0;
            foreach (var s in segments)
            {
                if (target <= acc + s.Length || s.Length < 1e-9)
                {
                    seg = s;
                    localT = s.Length > 1e-9 ? Math.Clamp((target - acc) / s.Length, 0, 1) : 0;
                    break;
                }
                acc += s.Length;
            }
            result.Add(seg.IsArc
                ? new Point2(seg.ArcCenter.X + roundR * Math.Cos(seg.A0 + (seg.A1 - seg.A0) * localT), seg.ArcCenter.Y + roundR * Math.Sin(seg.A0 + (seg.A1 - seg.A0) * localT))
                : new Point2(seg.Start.X + (seg.End.X - seg.Start.X) * localT, seg.Start.Y + (seg.End.Y - seg.Start.Y) * localT));
        }
        return result;
    }

    /// <summary>Evaluates a closed Catmull-Rom spline through `points` (wraps around, so the
    /// curve passes smoothly through every point with no start/end seam) at samplesPerSegment
    /// resolution — the admin only places a handful of points, this is what turns them into a
    /// smooth, natural-looking curve instead of a faceted straight-line polygon (the old test-
    /// branch approach's own visible flaw).</summary>
    public static List<Point2> CatmullRomClosed(List<Point2> points, int samplesPerSegment = 16)
    {
        var n = points.Count;
        if (n < 3) return points;
        var result = new List<Point2>();
        for (var i = 0; i < n; i++)
        {
            var p0 = points[(i - 1 + n) % n];
            var p1 = points[i];
            var p2 = points[(i + 1) % n];
            var p3 = points[(i + 2) % n];
            for (var s = 0; s < samplesPerSegment; s++)
            {
                var t = (double)s / samplesPerSegment;
                var t2 = t * t;
                var t3 = t2 * t;
                var x = 0.5 * (2 * p1.X + (-p0.X + p2.X) * t + (2 * p0.X - 5 * p1.X + 4 * p2.X - p3.X) * t2 + (-p0.X + 3 * p1.X - 3 * p2.X + p3.X) * t3);
                var y = 0.5 * (2 * p1.Y + (-p0.Y + p2.Y) * t + (2 * p0.Y - 5 * p1.Y + 4 * p2.Y - p3.Y) * t2 + (-p0.Y + 3 * p1.Y - 3 * p2.Y + p3.Y) * t3);
                result.Add(new Point2(x, y));
            }
        }
        return result;
    }

    /// <summary>Rasterizes a filled mask (0/255 per pixel) from a smooth closed outline (already
    /// densely sampled — see <see cref="CatmullRomClosed"/>) via horizontal scanline fill
    /// (even-odd rule) — much faster than a per-pixel point-in-polygon test for a several-hundred-
    /// point curve on a multi-megapixel image. Then unions/subtracts each spot circle: `Protect`
    /// spots ADD to the excluded area (art must never appear there, even inside an otherwise-
    /// printable region); non-protect ("allow") spots SUBTRACT from it (art is forced to appear
    /// there, even inside an otherwise-excluded region) — the two local-override tools an admin
    /// needs beyond just reshaping the main outline (a stray uncaught hole in the middle of the
    /// printable area; a piece of the real body wrongly swallowed by the outline).</summary>
    public static byte[] RasterizeSmoothShapeMask(int w, int h, List<Point2> denseOutline, List<(Point2 Center, double Radius, bool Protect)> spots)
    {
        var mask = new byte[w * h];
        if (denseOutline.Count >= 3)
        {
            var minY = Math.Max(0, (int)Math.Floor(denseOutline.Min(p => p.Y)));
            var maxY = Math.Min(h - 1, (int)Math.Ceiling(denseOutline.Max(p => p.Y)));
            for (var y = minY; y <= maxY; y++)
            {
                var yc = y + 0.5;
                var xs = new List<double>();
                for (var i = 0; i < denseOutline.Count; i++)
                {
                    var a = denseOutline[i];
                    var b = denseOutline[(i + 1) % denseOutline.Count];
                    if ((a.Y <= yc && b.Y > yc) || (b.Y <= yc && a.Y > yc))
                    {
                        var t = (yc - a.Y) / (b.Y - a.Y);
                        xs.Add(a.X + t * (b.X - a.X));
                    }
                }
                xs.Sort();
                for (var i = 0; i + 1 < xs.Count; i += 2)
                {
                    var x0 = Math.Max(0, (int)Math.Round(xs[i]));
                    var x1 = Math.Min(w - 1, (int)Math.Round(xs[i + 1]));
                    for (var x = x0; x <= x1; x++) mask[y * w + x] = 255;
                }
            }
        }

        foreach (var (center, radius, protect) in spots)
        {
            var x0 = Math.Max(0, (int)Math.Floor(center.X - radius));
            var x1 = Math.Min(w - 1, (int)Math.Ceiling(center.X + radius));
            var y0 = Math.Max(0, (int)Math.Floor(center.Y - radius));
            var y1 = Math.Min(h - 1, (int)Math.Ceiling(center.Y + radius));
            for (var y = y0; y <= y1; y++)
            {
                for (var x = x0; x <= x1; x++)
                {
                    var dx = x - center.X;
                    var dy = y - center.Y;
                    if (dx * dx + dy * dy > radius * radius) continue;
                    mask[y * w + x] = protect ? (byte)255 : (byte)0;
                }
            }
        }
        return mask;
    }
}
