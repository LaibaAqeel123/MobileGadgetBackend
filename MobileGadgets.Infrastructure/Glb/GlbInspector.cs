using System.Numerics;
using System.Text.Json;
using MobileGadgets.Application.Dtos;
using MobileGadgets.Application.Interfaces;

namespace MobileGadgets.Infrastructure.Glb;

/// <summary>Reads a binary glTF (.glb) directly — no 3D library — and checks whether it can be used
/// as a printable phone-case model. Only the parts of the format the checks need are read: the JSON
/// description, and the vertex/index/UV data of the printable material's triangles. Everything is
/// defensive: a malformed file produces a report with errors, never an exception, so the admin sees
/// a reason on screen instead of a server error.</summary>
public class GlbInspector : IGlbInspector
{
    public const string DefaultPrintMaterialName = "PRINT_AREA";

    private const uint GlbMagic = 0x46546C67;      // "glTF"
    private const uint ChunkJson = 0x4E4F534A;     // "JSON"
    private const uint ChunkBin = 0x004E4942;      // "BIN\0"
    private const int MaxTriangles = 400_000;
    private const double UvMargin = 0.001;

    public GlbInspectionReport Inspect(Stream glb, string? printMaterialName = null)
    {
        var report = new GlbInspectionReport();
        try
        {
            using var ms = new MemoryStream();
            glb.CopyTo(ms);
            InspectBytes(ms.ToArray(), printMaterialName, report);
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException or ArgumentException or IndexOutOfRangeException or KeyNotFoundException or InvalidOperationException or OverflowException or FormatException)
        {
            report.Errors.Add("The file could not be read as a 3D model (" + ex.Message + "). Export it again as a plain .glb.");
        }
        return report;
    }

    private static void InspectBytes(byte[] data, string? requestedMaterial, GlbInspectionReport report)
    {
        // ---- container ----
        if (data.Length < 20 || BitConverter.ToUInt32(data, 0) != GlbMagic)
        {
            report.Errors.Add("This is not a .glb file. Export the model as \"glTF Binary (.glb)\".");
            return;
        }
        if (BitConverter.ToUInt32(data, 4) != 2)
        {
            report.Errors.Add("Only glTF version 2 is supported.");
            return;
        }

        JsonDocument? json = null;
        ReadOnlyMemory<byte> bin = default;
        var pos = 12;
        while (pos + 8 <= data.Length)
        {
            var len = (int)BitConverter.ToUInt32(data, pos);
            var type = BitConverter.ToUInt32(data, pos + 4);
            pos += 8;
            if (len < 0 || pos + len > data.Length) throw new InvalidDataException("the file is cut off");
            if (type == ChunkJson) json = JsonDocument.Parse(data.AsMemory(pos, len));
            else if (type == ChunkBin && bin.IsEmpty) bin = data.AsMemory(pos, len);
            pos += len;
        }
        if (json is null) throw new InvalidDataException("no model description inside the file");

        using (json)
        {
            var root = json.RootElement;

            // ---- things we cannot read or that make the file not self-contained ----
            if (root.TryGetProperty("extensionsRequired", out var req))
            {
                foreach (var e in req.EnumerateArray())
                {
                    var name = e.GetString() ?? "";
                    if (name is "KHR_draco_mesh_compression" or "EXT_meshopt_compression")
                        report.Errors.Add("The model uses mesh compression (" + name + "). Export it again with compression turned off.");
                }
            }
            if (root.TryGetProperty("buffers", out var buffers))
            {
                foreach (var b in buffers.EnumerateArray())
                {
                    if (b.TryGetProperty("uri", out var uri) && !(uri.GetString() ?? "").StartsWith("data:"))
                        report.Errors.Add("The model points to a separate file (" + uri.GetString() + "). It must be one self-contained .glb.");
                }
            }
            if (root.TryGetProperty("images", out var images))
            {
                foreach (var im in images.EnumerateArray())
                {
                    if (im.TryGetProperty("uri", out var uri) && !(uri.GetString() ?? "").StartsWith("data:"))
                        report.Errors.Add("The model points to a separate image file (" + uri.GetString() + "). Embed textures in the .glb.");
                }
            }
            if (report.Errors.Count > 0) return;

            var meshes = root.TryGetProperty("meshes", out var m) ? m : default;
            var materials = root.TryGetProperty("materials", out var mats) ? mats : default;
            var accessors = root.TryGetProperty("accessors", out var acc) ? acc : default;
            var bufferViews = root.TryGetProperty("bufferViews", out var bv) ? bv : default;
            var nodes = root.TryGetProperty("nodes", out var n) ? n : default;

            report.NodeCount = nodes.ValueKind == JsonValueKind.Array ? nodes.GetArrayLength() : 0;
            report.MeshCount = meshes.ValueKind == JsonValueKind.Array ? meshes.GetArrayLength() : 0;
            if (report.MeshCount == 0)
            {
                report.Errors.Add("The file contains no 3D geometry.");
                return;
            }

            var materialNames = new List<string>();
            if (materials.ValueKind == JsonValueKind.Array)
                foreach (var mat in materials.EnumerateArray())
                    materialNames.Add(mat.TryGetProperty("name", out var nm) ? nm.GetString() ?? "" : "");

            // ---- walk every primitive: count triangles per material, remember the printable ones ----
            var triPerMaterial = new int[Math.Max(materialNames.Count, 1)];
            var uvPerMaterial = new bool[triPerMaterial.Length];
            var untexturedTris = 0;
            var totalTris = 0;
            var prims = new List<(JsonElement Prim, int Material)>();
            foreach (var mesh in meshes.EnumerateArray())
            {
                foreach (var p in mesh.GetProperty("primitives").EnumerateArray())
                {
                    var mode = p.TryGetProperty("mode", out var mo) ? mo.GetInt32() : 4;
                    var attrs = p.GetProperty("attributes");
                    var posAcc = attrs.GetProperty("POSITION").GetInt32();
                    var tris = mode == 4 ? TriangleCount(p, accessors, posAcc) : 0;
                    totalTris += tris;
                    var mi = p.TryGetProperty("material", out var mp) ? mp.GetInt32() : -1;
                    if (mi >= 0 && mi < triPerMaterial.Length)
                    {
                        triPerMaterial[mi] += tris;
                        if (attrs.TryGetProperty("TEXCOORD_0", out _)) uvPerMaterial[mi] = true;
                        prims.Add((p, mi));
                    }
                    else untexturedTris += tris;
                }
            }
            report.TriangleCount = totalTris;
            if (totalTris > MaxTriangles)
                report.Errors.Add($"The model has {totalTris:N0} triangles, which is too heavy for a web page (limit {MaxTriangles:N0}). Simplify it or export a lighter version.");

            for (var i = 0; i < materialNames.Count; i++)
                report.Materials.Add(new GlbMaterialInfo { Name = materialNames[i], TriangleCount = triPerMaterial[i], HasUvs = uvPerMaterial[i] });

            report.LongestSide = ModelLongestSide(root, accessors);
            CheckSize(report);

            // ---- resolve the printable material ----
            var wanted = string.IsNullOrWhiteSpace(requestedMaterial) ? DefaultPrintMaterialName : requestedMaterial!;
            var printIdx = materialNames.Select((nm, i) => (nm, i)).Where(t => t.nm == wanted).Select(t => t.i).ToList();
            if (printIdx.Count == 0)
                printIdx = materialNames.Select((nm, i) => (nm, i)).Where(t => string.Equals(t.nm, wanted, StringComparison.OrdinalIgnoreCase)).Select(t => t.i).ToList();
            if (printIdx.Count == 0)
            {
                var candidates = report.Materials.Where(x => x.TriangleCount > 0).Select(x => "\"" + x.Name + "\"");
                report.Errors.Add($"No material called \"{wanted}\" was found, so we cannot tell where the design should be printed. Pick the printable material from the list: {string.Join(", ", candidates)}.");
                return;
            }
            report.PrintMaterialName = materialNames[printIdx[0]];

            var printTris = printIdx.Sum(i => triPerMaterial[i]);
            if (printTris == 0)
            {
                report.Errors.Add($"The material \"{report.PrintMaterialName}\" is not used by any surface of the model.");
                return;
            }
            if (report.Materials.Count(x => x.TriangleCount > 0) == 1 && untexturedTris == 0)
                report.Warnings.Add("Only one material is used for the entire model, so the design would also cover the phone, buttons and lenses. Give the case surface its own material.");
            else if (report.MeshCount < 2 && totalTris == printTris)
                report.Warnings.Add("The whole model is the printable surface. A phone inside the case was not found; the lenses and buttons will not appear.");

            // ---- UV layout + stretch of the printable triangles ----
            double u0 = double.MaxValue, u1 = double.MinValue, v0 = double.MaxValue, v1 = double.MinValue;
            var ratios = new List<double>();
            var anyUv = false;
            foreach (var (p, mi) in prims)
            {
                if (!printIdx.Contains(mi)) continue;
                var attrs = p.GetProperty("attributes");
                if (!attrs.TryGetProperty("TEXCOORD_0", out var uvAccEl))
                {
                    report.Errors.Add($"The printable surface has no UV map (no texture coordinates), so a design cannot be placed on it. Unwrap \"{report.PrintMaterialName}\" in your 3D program and export again.");
                    return;
                }
                var pos3 = ReadVec(accessors, bufferViews, bin, attrs.GetProperty("POSITION").GetInt32(), 3);
                var uv2 = ReadVec(accessors, bufferViews, bin, uvAccEl.GetInt32(), 2);
                if (pos3 is null || uv2 is null)
                {
                    report.Errors.Add("The model uses a vertex format that is not supported (positions must be 32-bit floats). Export it again with default settings.");
                    return;
                }
                anyUv = true;
                for (var i = 0; i < uv2.Length; i += 2)
                {
                    u0 = Math.Min(u0, uv2[i]); u1 = Math.Max(u1, uv2[i]);
                    v0 = Math.Min(v0, uv2[i + 1]); v1 = Math.Max(v1, uv2[i + 1]);
                }
                var idx = p.TryGetProperty("indices", out var ie) ? ReadIndices(accessors, bufferViews, bin, ie.GetInt32()) : null;
                var triCount = idx is null ? pos3.Length / 9 : idx.Length / 3;
                for (var t = 0; t < triCount; t++)
                {
                    var a = idx is null ? t * 3 : (int)idx[t * 3];
                    var b = idx is null ? t * 3 + 1 : (int)idx[t * 3 + 1];
                    var c = idx is null ? t * 3 + 2 : (int)idx[t * 3 + 2];
                    var pa = new Vector3(pos3[a * 3], pos3[a * 3 + 1], pos3[a * 3 + 2]);
                    var pb = new Vector3(pos3[b * 3], pos3[b * 3 + 1], pos3[b * 3 + 2]);
                    var pc = new Vector3(pos3[c * 3], pos3[c * 3 + 1], pos3[c * 3 + 2]);
                    var area3 = Vector3.Cross(pb - pa, pc - pa).Length() / 2;
                    var ua = new Vector2(uv2[a * 2], uv2[a * 2 + 1]);
                    var ub = new Vector2(uv2[b * 2], uv2[b * 2 + 1]);
                    var uc = new Vector2(uv2[c * 2], uv2[c * 2 + 1]);
                    var areaUv = Math.Abs((ub.X - ua.X) * (uc.Y - ua.Y) - (ub.Y - ua.Y) * (uc.X - ua.X)) / 2;
                    if (area3 > 1e-12 && areaUv > 1e-12) ratios.Add(area3 / areaUv);
                }
            }
            if (!anyUv) return;

            if (u0 < -UvMargin || v0 < -UvMargin || u1 > 1 + UvMargin || v1 > 1 + UvMargin)
            {
                report.Errors.Add($"The printable surface's UV map goes outside the image (u {u0:0.###}..{u1:0.###}, v {v0:0.###}..{v1:0.###}; it must stay within 0..1). Re-pack the UVs into the 0..1 square.");
                return;
            }

            var layout = new GlbUvLayout { U0 = u0, U1 = u1, V0 = v0, V1 = v1 };
            if (ratios.Count >= 20)
            {
                ratios.Sort();
                double Pct(double q) => ratios[(int)Math.Round(q * (ratios.Count - 1))];
                layout.StretchSpread = Pct(0.95) / Math.Max(Pct(0.05), 1e-18);
            }
            report.PrintLayout = layout;

            if (layout.StretchSpread > 2.5)
                report.Warnings.Add($"A design would stretch unevenly over the printable surface (spread {layout.StretchSpread:0.0}x; under 2.5x is good). Check the corners and edges in the preview.");
            if ((u1 - u0) * (v1 - v0) < 0.25)
                report.Warnings.Add("The printable surface only uses a small part of the image area, so printed designs will look softer than they could. Scale the UV layout up to fill the 0..1 square.");
        }
    }

    private static void CheckSize(GlbInspectionReport report)
    {
        var s = report.LongestSide;
        if (s <= 0) return;
        if (s is >= 0.05 and <= 0.5) return;
        if (s is >= 50 and <= 500)
            report.Warnings.Add($"The model looks like it was exported in millimetres (longest side {s:0.#}); glTF expects metres. It will still display correctly because the viewer rescales it.");
        else
            report.Warnings.Add($"The model has an unusual size (longest side {s:0.###} units). A phone is normally about 0.17 (metres). The viewer will rescale it, but check the preview.");
    }

    // ---- accessor helpers ----

    private static int TriangleCount(JsonElement prim, JsonElement accessors, int posAccessor)
    {
        if (prim.TryGetProperty("indices", out var ie))
            return accessors[ie.GetInt32()].GetProperty("count").GetInt32() / 3;
        return accessors[posAccessor].GetProperty("count").GetInt32() / 3;
    }

    /// <summary>Reads a float VEC2/VEC3 accessor (also normalised ubyte/ushort for UVs) into a flat
    /// array. Returns null for anything unsupported (sparse, wrong type, non-float positions).</summary>
    private static float[]? ReadVec(JsonElement accessors, JsonElement bufferViews, ReadOnlyMemory<byte> bin, int accIdx, int comps)
    {
        var a = accessors[accIdx];
        if (a.TryGetProperty("sparse", out _)) return null;
        if (!a.TryGetProperty("bufferView", out var bvEl)) return null;
        var type = a.GetProperty("type").GetString();
        if (type != (comps == 3 ? "VEC3" : "VEC2")) return null;
        var ct = a.GetProperty("componentType").GetInt32();
        var normalized = a.TryGetProperty("normalized", out var nz) && nz.GetBoolean();
        var size = ct switch { 5126 => 4, 5123 => 2, 5121 => 1, _ => 0 };
        if (size == 0 || (ct != 5126 && !(normalized && comps == 2))) return null;

        var count = a.GetProperty("count").GetInt32();
        var view = bufferViews[bvEl.GetInt32()];
        var offset = (view.TryGetProperty("byteOffset", out var vo) ? vo.GetInt32() : 0) + (a.TryGetProperty("byteOffset", out var ao) ? ao.GetInt32() : 0);
        var stride = view.TryGetProperty("byteStride", out var st) ? st.GetInt32() : comps * size;
        var span = bin.Span;
        var result = new float[count * comps];
        for (var i = 0; i < count; i++)
        {
            for (var c = 0; c < comps; c++)
            {
                var p = offset + i * stride + c * size;
                result[i * comps + c] = ct switch
                {
                    5126 => BitConverter.ToSingle(span.Slice(p, 4)),
                    5123 => BitConverter.ToUInt16(span.Slice(p, 2)) / 65535f,
                    _ => span[p] / 255f,
                };
            }
        }
        return result;
    }

    private static uint[]? ReadIndices(JsonElement accessors, JsonElement bufferViews, ReadOnlyMemory<byte> bin, int accIdx)
    {
        var a = accessors[accIdx];
        if (a.TryGetProperty("sparse", out _) || !a.TryGetProperty("bufferView", out var bvEl)) return null;
        var ct = a.GetProperty("componentType").GetInt32();
        var size = ct switch { 5125 => 4, 5123 => 2, 5121 => 1, _ => 0 };
        if (size == 0) return null;
        var count = a.GetProperty("count").GetInt32();
        var view = bufferViews[bvEl.GetInt32()];
        var offset = (view.TryGetProperty("byteOffset", out var vo) ? vo.GetInt32() : 0) + (a.TryGetProperty("byteOffset", out var ao) ? ao.GetInt32() : 0);
        var stride = view.TryGetProperty("byteStride", out var st) ? st.GetInt32() : size;
        var span = bin.Span;
        var result = new uint[count];
        for (var i = 0; i < count; i++)
        {
            var p = offset + i * stride;
            result[i] = ct switch { 5125 => BitConverter.ToUInt32(span.Slice(p, 4)), 5123 => BitConverter.ToUInt16(span.Slice(p, 2)), _ => span[p] };
        }
        return result;
    }

    // ---- model size: bounding box of every mesh instance in the scene, using node transforms ----

    private static double ModelLongestSide(JsonElement root, JsonElement accessors)
    {
        if (!root.TryGetProperty("nodes", out var nodes) || nodes.ValueKind != JsonValueKind.Array) return 0;
        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);
        var any = false;

        var roots = new List<int>();
        if (root.TryGetProperty("scenes", out var scenes) && scenes.GetArrayLength() > 0 &&
            scenes[root.TryGetProperty("scene", out var si) ? si.GetInt32() : 0].TryGetProperty("nodes", out var sn))
            foreach (var r in sn.EnumerateArray()) roots.Add(r.GetInt32());
        else
            roots.AddRange(Enumerable.Range(0, nodes.GetArrayLength()));

        void Visit(int idx, Matrix4x4 parent, int depth)
        {
            if (depth > 64) return;
            var node = nodes[idx];
            var world = LocalMatrix(node) * parent;
            if (node.TryGetProperty("mesh", out var meshIdx))
            {
                foreach (var prim in root.GetProperty("meshes")[meshIdx.GetInt32()].GetProperty("primitives").EnumerateArray())
                {
                    var a = accessors[prim.GetProperty("attributes").GetProperty("POSITION").GetInt32()];
                    if (!a.TryGetProperty("min", out var amin) || !a.TryGetProperty("max", out var amax)) continue;
                    for (var corner = 0; corner < 8; corner++)
                    {
                        var v = new Vector3(
                            (float)((corner & 1) == 0 ? amin[0].GetDouble() : amax[0].GetDouble()),
                            (float)((corner & 2) == 0 ? amin[1].GetDouble() : amax[1].GetDouble()),
                            (float)((corner & 4) == 0 ? amin[2].GetDouble() : amax[2].GetDouble()));
                        var w = Vector3.Transform(v, world);
                        min = Vector3.Min(min, w); max = Vector3.Max(max, w); any = true;
                    }
                }
            }
            if (node.TryGetProperty("children", out var kids))
                foreach (var k in kids.EnumerateArray()) Visit(k.GetInt32(), world, depth + 1);
        }
        foreach (var r in roots) Visit(r, Matrix4x4.Identity, 0);
        if (!any) return 0;
        var size = max - min;
        return Math.Max(size.X, Math.Max(size.Y, size.Z));
    }

    private static Matrix4x4 LocalMatrix(JsonElement node)
    {
        if (node.TryGetProperty("matrix", out var m) && m.GetArrayLength() == 16)
        {
            float F(int i) => (float)m[i].GetDouble();
            return new Matrix4x4(F(0), F(1), F(2), F(3), F(4), F(5), F(6), F(7), F(8), F(9), F(10), F(11), F(12), F(13), F(14), F(15));
        }
        var t = node.TryGetProperty("translation", out var tr) ? new Vector3((float)tr[0].GetDouble(), (float)tr[1].GetDouble(), (float)tr[2].GetDouble()) : Vector3.Zero;
        var r = node.TryGetProperty("rotation", out var ro) ? new Quaternion((float)ro[0].GetDouble(), (float)ro[1].GetDouble(), (float)ro[2].GetDouble(), (float)ro[3].GetDouble()) : Quaternion.Identity;
        var s = node.TryGetProperty("scale", out var sc) ? new Vector3((float)sc[0].GetDouble(), (float)sc[1].GetDouble(), (float)sc[2].GetDouble()) : Vector3.One;
        return Matrix4x4.CreateScale(s) * Matrix4x4.CreateFromQuaternion(r) * Matrix4x4.CreateTranslation(t);
    }
}
