#!/usr/bin/env python3
"""Stress-test the random arena generator (Assets/Spheres/Scripts/Maps/MapGenerator.cs) outside Unity.

Builds a small .NET console harness that compiles MapGenerator.cs, MapData.cs, BlockTypes.cs and the
pure-data parts of Mechanisms.cs / Props.cs against a tiny UnityEngine stub, then:

  * generates N seeds and runs MapGenerator.Validate on each (all must pass),
  * checks determinism (same seed -> identical map),
  * prints stats (attempts per map, themes, layouts, sizes, moving parts, props, timing),
  * optionally renders isometric + top-down PNG previews (needs ffmpeg).

Usage:
  python3 Tools/test_map_generator.py [--count 500] [--start 1] [--teams 4]
                                      [--preview DIR] [--preview-seeds 1,2,3] [--work DIR]
Requires the .NET SDK (6+). Exits 1 if any map fails validation or determinism.
"""
import argparse
import os
import re
import shutil
import subprocess
import sys
import tempfile

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
MAPS = os.path.join(ROOT, "Assets", "Spheres", "Scripts", "Maps")

STUBS = r'''
using System;
namespace UnityEngine
{
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 one => new Vector3(1, 1, 1);
        public static Vector3 zero => new Vector3(0, 0, 0);
        public static Vector3 operator *(Vector3 a, float s) => new Vector3(a.x * s, a.y * s, a.z * s);
        public static Vector3 operator *(float s, Vector3 a) => a * s;
        public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3(a.x - b.x, a.y - b.y, a.z - b.z);
    }
    public struct Color
    {
        public float r, g, b, a;
        public Color(float r, float g, float b, float a = 1f) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public static Color gray => new Color(0.5f, 0.5f, 0.5f);
        public static Color white => new Color(1, 1, 1);
    }
    public struct Bounds { public Bounds(Vector3 c, Vector3 s) { } }
    public class PhysicMaterial { } public enum PhysicMaterialCombine { Average }
    public class PhysicsMaterial { } public enum PhysicsMaterialCombine { Average }
    public static class Mathf
    {
        public static int Max(int a, int b) => Math.Max(a, b);
        public static float Max(float a, float b) => Math.Max(a, b);
        public static int Min(int a, int b) => Math.Min(a, b);
        public static float Min(float a, float b) => Math.Min(a, b);
        public static int Clamp(int v, int a, int b) => v < a ? a : v > b ? b : v;
        public static float Clamp(float v, float a, float b) => v < a ? a : v > b ? b : v;
        public static float Abs(float v) => Math.Abs(v);
        public static int RoundToInt(float v) => (int)Math.Round(v);
    }
    public static class JsonUtility
    {
        public static string ToJson(object o, bool pretty = false) => throw new NotSupportedException();
        public static T FromJson<T>(string s) => throw new NotSupportedException();
    }
    public static class Debug { public static void Log(object o) { } public static void LogWarning(object o) { } }
}
'''

PROGRAM = r'''
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Spheres;

static class Program
{
    static int Main(string[] args)
    {
        int count = 500, start = 1, teams = 4;
        string preview = null; var previewSeeds = new List<int>();
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--count") count = int.Parse(args[++i]);
            else if (args[i] == "--start") start = int.Parse(args[++i]);
            else if (args[i] == "--teams") teams = int.Parse(args[++i]);
            else if (args[i] == "--preview") preview = args[++i];
            else if (args[i] == "--preview-seeds") previewSeeds.AddRange(args[++i].Split(',').Select(int.Parse));
        }
        int failures = 0, fallbacks = 0, nondet = 0;
        var attemptsHist = new SortedDictionary<int, int>();
        var themes = new SortedDictionary<string, int>();
        var layouts = new SortedDictionary<string, int>();
        var mechKinds = new SortedDictionary<string, int>();
        var propKinds = new SortedDictionary<string, int>();
        var names = new HashSet<string>();
        long blocks = 0, mechs = 0, props = 0, spawns = 0; int mapsWithMechs = 0, maxBlocks = 0;
        int minX = 999, maxX = 0, minY = 999, maxY = 0, minZ = 999, maxZ = 0;
        var sw = Stopwatch.StartNew();
        double worstMs = 0;
        var fb = MapGenerator.Validate(MapGenerator.Fallback(0));
        if (fb.Count > 0) { Console.WriteLine("FALLBACK MAP INVALID: " + string.Join("; ", fb)); failures++; }
        for (int s = start; s < start + count; s++)
        {
            var t0 = sw.Elapsed.TotalMilliseconds;
            var m = MapGenerator.Generate(s, teams, out int attempts);
            worstMs = Math.Max(worstMs, sw.Elapsed.TotalMilliseconds - t0);
            attemptsHist[attempts] = attemptsHist.TryGetValue(attempts, out var a) ? a + 1 : 1;
            if (attempts > MapGenerator.MaxAttempts) fallbacks++;
            var errs = MapGenerator.Validate(m);
            if (errs.Count > 0) { failures++; Console.WriteLine("seed " + s + " INVALID: " + string.Join("; ", errs.Take(5))); }
            var again = MapGenerator.Generate(s, teams);
            if (Fingerprint(again) != Fingerprint(m)) { nondet++; Console.WriteLine("seed " + s + " NOT DETERMINISTIC"); }
            var match = System.Text.RegularExpressions.Regex.Match(m.description ?? "", "Generated (.*) arena: (.*)\\.");
            string th = match.Success ? match.Groups[1].Value : "?", lay = match.Success ? match.Groups[2].Value : "fallback";
            themes[th] = themes.TryGetValue(th, out var c1) ? c1 + 1 : 1;
            layouts[lay] = layouts.TryGetValue(lay, out var c2) ? c2 + 1 : 1;
            foreach (var k in m.mechs) mechKinds[k.kind] = mechKinds.TryGetValue(k.kind, out var c3) ? c3 + 1 : 1;
            foreach (var p in m.props) propKinds[p.kind] = propKinds.TryGetValue(p.kind, out var c4) ? c4 + 1 : 1;
            names.Add(m.name);
            blocks += m.blocks.Count; mechs += m.mechs.Count; props += m.props.Count; spawns += m.spawns.Count;
            if (m.mechs.Count > 0) mapsWithMechs++;
            maxBlocks = Math.Max(maxBlocks, m.blocks.Count);
            minX = Math.Min(minX, m.sizeX); maxX = Math.Max(maxX, m.sizeX);
            minY = Math.Min(minY, m.sizeY); maxY = Math.Max(maxY, m.sizeY);
            minZ = Math.Min(minZ, m.sizeZ); maxZ = Math.Max(maxZ, m.sizeZ);
        }
        double total = sw.Elapsed.TotalMilliseconds;
        Console.WriteLine();
        Console.WriteLine("seeds " + start + ".." + (start + count - 1) + ", teams hint " + teams);
        Console.WriteLine("valid: " + (count - failures) + "/" + count + ", deterministic: " + (count - nondet) + "/" + count + ", fallback maps: " + fallbacks);
        Console.WriteLine("attempts per map: " + string.Join(", ", attemptsHist.Select(kv => kv.Key + "x" + kv.Value)));
        Console.WriteLine("themes: " + string.Join(", ", themes.Select(kv => kv.Key + " " + kv.Value)));
        Console.WriteLine("layouts: " + string.Join(", ", layouts.Select(kv => kv.Key + " " + kv.Value)));
        Console.WriteLine("moving parts: " + mechs + " total, on " + mapsWithMechs + " maps (" + string.Join(", ", mechKinds.Select(kv => kv.Key + " " + kv.Value)) + ")");
        Console.WriteLine("props: " + props + " total (" + string.Join(", ", propKinds.Select(kv => kv.Key + " " + kv.Value)) + ")");
        Console.WriteLine(string.Format("avg blocks {0:0.0} (max {1}), avg spawns {2:0.0}", blocks / (double)count, maxBlocks, spawns / (double)count));
        Console.WriteLine("sizes X " + minX + "-" + maxX + ", Y " + minY + "-" + maxY + ", Z " + minZ + "-" + maxZ + ", unique names " + names.Count);
        Console.WriteLine(string.Format("time: {0:0.0} ms per map incl. validation + determinism re-run, worst {1:0} ms", total / count, worstMs));
        if (MapGenerator.LastBuildError != null) Console.WriteLine("LAST BUILD EXCEPTION: " + MapGenerator.LastBuildError);

        // Why single attempts get rejected (first attempt of each seed).
        var reasons = new SortedDictionary<string, int>();
        int firstOk = 0;
        for (int s = start; s < start + count; s++)
        {
            var m = MapGenerator.GenerateOnce(s, 0, teams, out string why);
            if (m != null) { firstOk++; continue; }
            string key = System.Text.RegularExpressions.Regex.Replace(why, "[-0-9.,]+", "#");
            if (key.Length > 110) key = key.Substring(0, 110);
            reasons[key] = reasons.TryGetValue(key, out var n) ? n + 1 : 1;
        }
        Console.WriteLine("first attempt accepted: " + firstOk + "/" + count + "; rejection reasons:");
        foreach (var kv in reasons.OrderByDescending(kv => kv.Value).Take(15)) Console.WriteLine("  " + kv.Value + "x " + kv.Key);

        if (preview != null)
        {
            Directory.CreateDirectory(preview);
            foreach (int s in previewSeeds)
            {
                var m = MapGenerator.Generate(s, teams);
                string file = Path.Combine(preview, "seed_" + s + ".ppm");
                Render.Write(m, file);
                Console.WriteLine("preview " + file + "  " + m.name + " (" + m.sizeX + "x" + m.sizeY + "x" + m.sizeZ + ", " + m.mechs.Count + " moving parts, " + m.props.Count + " props) - " + m.description);
            }
        }
        return failures > 0 || nondet > 0 ? 1 : 0;
    }

    static string Fingerprint(MapData m)
    {
        var sb = new StringBuilder(m.name).Append('|').Append(m.sizeX).Append(',').Append(m.sizeY).Append(',').Append(m.sizeZ);
        foreach (var b in m.blocks) sb.Append('|').Append(b.x).Append(',').Append(b.y).Append(',').Append(b.z).Append(',').Append(b.w).Append(',').Append(b.h).Append(',').Append(b.d).Append(b.type);
        foreach (var s in m.spawns) sb.Append("|s").Append(s.x).Append(',').Append(s.y).Append(',').Append(s.z).Append(',').Append(s.team);
        foreach (var k in m.mechs) sb.Append("|m").Append(k.kind).Append(k.x).Append(',').Append(k.y).Append(',').Append(k.z).Append(',').Append(k.dx).Append(',').Append(k.dy).Append(',').Append(k.dz).Append(',').Append(k.phase);
        foreach (var p in m.props) sb.Append("|p").Append(p.kind).Append(p.x).Append(',').Append(p.y).Append(',').Append(p.z);
        return sb.ToString();
    }
}

/// <summary>Isometric voxel view (left) + top-down schematic (right) on a fixed 1200x640 canvas.</summary>
static class Render
{
    const int W = 1200, H = 640;
    static byte[] px;
    static float[] zbuf;

    static (float r, float g, float b) Hex(string hex, (float, float, float) fallback)
    {
        if (string.IsNullOrEmpty(hex) || hex.Length != 7) return fallback;
        return (Convert.ToInt32(hex.Substring(1, 2), 16) / 255f, Convert.ToInt32(hex.Substring(3, 2), 16) / 255f, Convert.ToInt32(hex.Substring(5, 2), 16) / 255f);
    }

    static void Put(int x, int y, (float r, float g, float b) c, float depth)
    {
        if (x < 0 || y < 0 || x >= W || y >= H) return;
        int i = y * W + x;
        if (depth < zbuf[i]) return;
        zbuf[i] = depth;
        px[i * 3] = (byte)Math.Clamp(c.r * 255, 0, 255); px[i * 3 + 1] = (byte)Math.Clamp(c.g * 255, 0, 255); px[i * 3 + 2] = (byte)Math.Clamp(c.b * 255, 0, 255);
    }

    static void Poly((float x, float y)[] p, (float, float, float) c, float depth)
    {
        float minx = p.Min(q => q.x), maxx = p.Max(q => q.x), miny = p.Min(q => q.y), maxy = p.Max(q => q.y);
        for (int y = (int)Math.Floor(miny); y <= (int)Math.Ceiling(maxy); y++)
            for (int x = (int)Math.Floor(minx); x <= (int)Math.Ceiling(maxx); x++)
            {
                float fx = x + 0.5f, fy = y + 0.5f;
                bool pos = false, neg = false;
                for (int i = 0; i < p.Length; i++)
                {
                    var a = p[i]; var b = p[(i + 1) % p.Length];
                    float cr = (b.x - a.x) * (fy - a.y) - (b.y - a.y) * (fx - a.x);
                    if (cr > 0.01f) pos = true; else if (cr < -0.01f) neg = true;
                }
                if (!(pos && neg)) Put(x, y, c, depth);
            }
    }

    static (float, float, float) Mul((float r, float g, float b) c, float s) => (c.r * s, c.g * s, c.b * s);

    public static void Write(MapData m, string file)
    {
        px = new byte[W * H * 3];
        zbuf = new float[W * H];
        var bg = Hex(m.background, (0.1f, 0.12f, 0.18f));
        for (int i = 0; i < W * H; i++) { zbuf[i] = float.MinValue; float v = 0.75f + 0.25f * ((i / W) / (float)H); px[i * 3] = (byte)(bg.Item1 * v * 255); px[i * 3 + 1] = (byte)(bg.Item2 * v * 255); px[i * 3 + 2] = (byte)(bg.Item3 * v * 255); }

        int sx = m.sizeX, sy = m.sizeY, sz = m.sizeZ;
        var vox = new Dictionary<(int, int, int), (float, float, float)>();
        foreach (var b in m.blocks)
        {
            var t = BlockTypes.Get(b.type);
            var c = (t.Color.r, t.Color.g, t.Color.b);
            if (t.Transparent) c = (c.Item1 * 0.6f + 0.4f, c.Item2 * 0.6f + 0.4f, c.Item3 * 0.6f + 0.4f);
            for (int x = b.x; x < b.x + b.W; x++) for (int y = b.y; y < b.y + b.H; y++) for (int z = b.z; z < b.z + b.D; z++) vox[(x, y, z)] = c;
        }
        foreach (var k in m.mechs)
        {
            var c = k.deadly ? (1f, 0.3f, 0.25f) : (0.35f, 0.85f, 1f);
            if (k.kind == MechKinds.Pendulum)
            {
                for (int y = (int)(k.y - k.length); y <= k.y; y++) vox[(k.x, y, k.z)] = y <= k.y - k.length + 1 ? c : (0.7f, 0.72f, 0.78f);
                continue;
            }
            int h = k.kind == MechKinds.Spikes ? 1 : Math.Max(1, k.h);
            for (int x = k.x; x < k.x + Math.Max(1, k.w); x++) for (int y = k.y; y < k.y + h; y++) for (int z = k.z; z < k.z + Math.Max(1, k.d); z++) vox[(x, y, z)] = c;
        }
        foreach (var p in m.props) { var c = PropKinds.Get(p.kind).Color; vox[((int)Math.Floor(p.x), (int)Math.Floor(p.y + 0.05f), (int)Math.Floor(p.z))] = (c.r, c.g, c.b); }
        var teamCol = new[] { (0.95f, 0.3f, 0.3f), (0.3f, 0.55f, 1f), (0.35f, 0.9f, 0.4f), (1f, 0.85f, 0.25f), (0.8f, 0.4f, 1f), (1f, 0.55f, 0.15f), (0.3f, 0.95f, 0.95f), (1f, 0.5f, 0.8f) };
        foreach (var s in m.spawns) { var c = s.team >= 0 && s.team < 8 ? teamCol[s.team] : (1f, 1f, 1f); vox[(s.x, s.y, s.z)] = c; }

        // Isometric: viewer at +x +z, above.
        // Fit the occupied cells into the left 780x600 area.
        float minU = float.MaxValue, maxU = float.MinValue, minV = float.MaxValue, maxV = float.MinValue;
        foreach (var key in vox.Keys)
            for (int c = 0; c < 8; c++)
            {
                float x = key.Item1 + (c & 1), y = key.Item2 + ((c >> 1) & 1), z = key.Item3 + ((c >> 2) & 1);
                float u = x - z, v = (x + z) * 0.5f - y * 0.9f;
                minU = Math.Min(minU, u); maxU = Math.Max(maxU, u); minV = Math.Min(minV, v); maxV = Math.Max(maxV, v);
            }
        float a = Math.Min(760f / Math.Max(1f, maxU - minU), 600f / Math.Max(1f, maxV - minV));
        float ox = 20 + (760 - (maxU - minU) * a) * 0.5f - minU * a, oy = 20 + (600 - (maxV - minV) * a) * 0.5f - minV * a;
        (float, float) P(float x, float y, float z) => (ox + (x - z) * a, oy + (x + z) * a * 0.5f - y * a * 0.9f);
        foreach (var kv in vox.OrderBy(kv => kv.Key.Item1 + kv.Key.Item3).ThenBy(kv => kv.Key.Item2))
        {
            var (x, y, z) = kv.Key; var c = kv.Value;
            float depth = x + z + y * 0.001f;
            if (!vox.ContainsKey((x, y + 1, z))) Poly(new[] { P(x, y + 1, z), P(x + 1, y + 1, z), P(x + 1, y + 1, z + 1), P(x, y + 1, z + 1) }, c, depth);
            if (!vox.ContainsKey((x + 1, y, z))) Poly(new[] { P(x + 1, y, z), P(x + 1, y + 1, z), P(x + 1, y + 1, z + 1), P(x + 1, y, z + 1) }, Mul(c, 0.78f), depth);
            if (!vox.ContainsKey((x, y, z + 1))) Poly(new[] { P(x, y, z + 1), P(x + 1, y, z + 1), P(x + 1, y + 1, z + 1), P(x, y + 1, z + 1) }, Mul(c, 0.6f), depth);
        }

        // Top-down schematic.
        float cs = Math.Min(380f / sx, 560f / sz);
        float tx0 = W - 20 - sx * cs, tz0 = (H - sz * cs) * 0.5f;
        var top = new Dictionary<(int, int), (int y, (float, float, float) c)>();
        foreach (var kv in vox) { var key = (kv.Key.Item1, kv.Key.Item3); if (!top.TryGetValue(key, out var e) || e.y < kv.Key.Item2) top[key] = (kv.Key.Item2, kv.Value); }
        for (int x = 0; x < sx; x++) for (int z = 0; z < sz; z++)
        {
            var c = top.TryGetValue((x, z), out var e) ? Mul(e.c, 0.55f + 0.6f * Math.Clamp(e.y / (sy * 0.65f), 0f, 1f)) : Mul(bg, 0.6f);
            for (int yy = (int)(tz0 + z * cs); yy < (int)(tz0 + (z + 1) * cs); yy++) for (int xx = (int)(tx0 + x * cs); xx < (int)(tx0 + (x + 1) * cs); xx++) Put(xx, yy, c, 1e9f);
        }
        using var f = new FileStream(file, FileMode.Create);
        var hdr = Encoding.ASCII.GetBytes("P6\n" + W + " " + H + "\n255\n");
        f.Write(hdr, 0, hdr.Length);
        f.Write(px, 0, px.Length);
    }
}
'''

CSPROJ = '''<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net{ver}</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>disable</Nullable>
    <AssemblyName>MapGenHarness</AssemblyName>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
    <NoWarn>CS0618;CS0414;CS0649;CS0169;CS0067;CS0162;CS8981</NoWarn>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="*.cs" />
  </ItemGroup>
</Project>
'''


def data_part(path, marker_re):
    """The file up to the first runtime (MonoBehaviour) class, with the namespace closed again."""
    src = open(path, encoding="utf-8").read()
    m = re.search(marker_re, src)
    if not m:
        sys.exit("could not find %r in %s" % (marker_re, path))
    cut = src.rfind("/// <summary>", 0, m.start())
    return src[:cut].rstrip() + "\n}\n"


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--count", type=int, default=500)
    ap.add_argument("--start", type=int, default=1)
    ap.add_argument("--teams", type=int, default=4)
    ap.add_argument("--preview")
    ap.add_argument("--preview-seeds", default="")
    ap.add_argument("--work")
    args = ap.parse_args()

    work = args.work or tempfile.mkdtemp(prefix="mapgen_")
    os.makedirs(work, exist_ok=True)
    for name in ("MapGenerator.cs", "MapData.cs", "BlockTypes.cs"):
        shutil.copy(os.path.join(MAPS, name), os.path.join(work, name))
    with open(os.path.join(work, "MechData.cs"), "w") as f:
        f.write(data_part(os.path.join(MAPS, "Mechanisms.cs"), r"public class Mechanism\s*:\s*MonoBehaviour"))
    with open(os.path.join(work, "PropData.cs"), "w") as f:
        f.write(data_part(os.path.join(MAPS, "Props.cs"), r"public class Prop\s*:\s*MonoBehaviour"))
    with open(os.path.join(work, "UnityStubs.cs"), "w") as f:
        f.write(STUBS)
    with open(os.path.join(work, "Program.cs"), "w") as f:
        f.write(PROGRAM)
    sdk = subprocess.run(["dotnet", "--version"], capture_output=True, text=True).stdout.strip()
    ver = ".".join(sdk.split(".")[:1]) + ".0" if sdk else "8.0"
    with open(os.path.join(work, "MapGenHarness.csproj"), "w") as f:
        f.write(CSPROJ.replace("{ver}", ver))

    out = os.path.join(work, "out")
    build = subprocess.run(["dotnet", "build", "-c", "Release", "-nologo", "-v", "q", "-o", out, work],
                           capture_output=True, text=True)
    if build.returncode != 0:
        print(build.stdout[-4000:], build.stderr[-2000:])
        return 2

    cmd = ["dotnet", os.path.join(out, "MapGenHarness.dll"), "--count", str(args.count),
           "--start", str(args.start), "--teams", str(args.teams)]
    if args.preview:
        os.makedirs(args.preview, exist_ok=True)
        cmd += ["--preview", os.path.abspath(args.preview)]
        if args.preview_seeds:
            cmd += ["--preview-seeds", args.preview_seeds]
    rc = subprocess.run(cmd).returncode

    if args.preview and shutil.which("ffmpeg"):
        for name in sorted(os.listdir(args.preview)):
            if name.endswith(".ppm"):
                src = os.path.join(args.preview, name)
                subprocess.run(["ffmpeg", "-loglevel", "error", "-y", "-i", src, src[:-4] + ".png"])
                os.remove(src)
    return rc


if __name__ == "__main__":
    sys.exit(main())
