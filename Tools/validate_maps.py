#!/usr/bin/env python3
"""Validate the built-in maps defined in Assets/Spheres/Scripts/Maps/BuiltInMaps.cs.

Parses each `public static MapData Name()` method, replays its Box / BoxMirrorX /
BoxMirror4 / Spawn calls exactly like MapData.cs does, and checks:

  errors   - blocks outside the arena, unknown block types, spawns outside the arena,
             spawns not standing on a solid non-hazard block, spawns without two empty
             cells of headroom, teams 0-3 without a spawn, unregistered / duplicate ids
  warnings - overlapping blocks, spawns on bouncy or breakable blocks, fewer than
             4 neutral (team -1) spawns, spawns very close to the side walls

Usage: python3 Tools/validate_maps.py [path/to/BuiltInMaps.cs] [-v]
Exits 1 if any map has errors.
"""
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DEFAULT_PATH = os.path.join(ROOT, "Assets", "Spheres", "Scripts", "Maps", "BuiltInMaps.cs")

BLOCK_TYPES = {"solid", "light", "dark", "bouncy", "hazard", "ice", "glass", "crate", "grass",
               "wood", "brick", "crystal", "blast"}
BREAKABLE = {"glass", "crate", "wood", "brick", "crystal", "blast"}
PROP_KINDS = {"crate": (1.0, 1.0, 1.0), "box": (0.8, 0.7, 0.8), "barrel": (0.8, 1.1, 0.8),
              "ball": (1.3, 1.3, 1.3), "heavy": (1.4, 1.4, 1.4)}

# Mirrors MechKinds.Preset in Mechanisms.cs.
MECH_PRESETS = {
    "Door": dict(w=1, h=3, d=3, dy=3.0, deadly=False),
    "Platform": dict(w=3, h=1, d=3, dx=6.0, deadly=False),
    "Elevator": dict(w=3, h=1, d=3, dy=6.0, deadly=False),
    "Piston": dict(w=2, h=2, d=2, dx=3.0, deadly=False),
    "Spinner": dict(w=9, h=1, d=1, axis=1, deadly=False),
    "Pendulum": dict(w=2, h=2, d=2, axis=2, length=5.0, deadly=True),
    "Saw": dict(w=3, h=1, d=3, axis=1, dx=6.0, deadly=True),
    "Spikes": dict(w=2, h=1, d=2, deadly=True),
}


class Mech:
    def __init__(self, kind, x, y, z, line, tweaks):
        p = dict(w=1, h=1, d=1, dx=0.0, dy=0.0, dz=0.0, axis=1, length=5.0, angle=60.0, deadly=False)
        p.update(MECH_PRESETS[kind])
        p.update(tweaks)
        p["axis"] = int(p["axis"])
        self.kind, self.x, self.y, self.z, self.line, self.p = kind, x, y, z, line, p

    def swept(self):
        """Axis-aligned box covering everywhere the part can reach: (min, max)."""
        p = self.p
        w, h, d = max(1, p["w"]), max(1, p["h"]), max(1, p["d"])
        if self.kind == "Pendulum":
            import math
            reach = p["length"] * math.sin(math.radians(min(90.0, p["angle"]))) + w
            r = p["length"] + w
            c = (self.x + 0.5, self.y + 0.5, self.z + 0.5)
            ax = p["axis"]
            lo = [c[0] - reach, c[1] - r, c[2] - reach]
            hi = [c[0] + reach, c[1] + 0.5, c[2] + reach]
            lo[ax] = c[ax] - w
            hi[ax] = c[ax] + w
            return lo, hi
        if self.kind == "Spinner":
            r = max(w, h, d) * 0.5
            c = (self.x + w / 2, self.y + h / 2, self.z + d / 2)
            ax = p["axis"]
            lo = [c[0] - r, c[1] - r, c[2] - r]
            hi = [c[0] + r, c[1] + r, c[2] + r]
            half = [w / 2, h / 2, d / 2][ax]
            lo[ax], hi[ax] = c[ax] - half, c[ax] + half
            return lo, hi
        if self.kind == "Spikes":
            return [self.x, self.y, self.z], [self.x + w, self.y + 1, self.z + d]
        lo = [self.x, self.y, self.z]
        hi = [self.x + w, self.y + h, self.z + d]
        for i, t in enumerate((p["dx"], p["dy"], p["dz"])):
            if t < 0:
                lo[i] += t
            else:
                hi[i] += t
        return lo, hi

    def __str__(self):
        return "%s(%d,%d,%d)@L%d" % (self.kind, self.x, self.y, self.z, self.line)
MAX_TEAMS = 8


class Block:
    __slots__ = ("x", "y", "z", "w", "h", "d", "type", "line")

    def __init__(self, x, y, z, w, h, d, t, line):
        # MapData.W/H/D clamp to >= 1.
        self.x, self.y, self.z = x, y, z
        self.w, self.h, self.d = max(1, w), max(1, h), max(1, d)
        self.type, self.line = t, line

    def contains(self, cx, cy, cz):
        return (self.x <= cx < self.x + self.w and self.y <= cy < self.y + self.h
                and self.z <= cz < self.z + self.d)

    def overlaps(self, o):
        return (self.x < o.x + o.w and o.x < self.x + self.w and self.y < o.y + o.h
                and o.y < self.y + self.h and self.z < o.z + o.d and o.z < self.z + self.d)

    def __str__(self):
        return "%s(%d,%d,%d %dx%dx%d)@L%d" % (self.type, self.x, self.y, self.z,
                                              self.w, self.h, self.d, self.line)


class Map:
    def __init__(self, method, name, sx, sy, sz):
        self.method, self.name = method, name
        self.sx, self.sy, self.sz = sx, sy, sz
        self.blocks, self.spawns, self.calls = [], [], 0
        self.mechs, self.props = [], []

    # --- replicas of the MapData builder helpers -------------------------------------
    def box(self, x, y, z, w, h, d, t, line):
        self.blocks.append(Block(x, y, z, w, h, d, t, line))

    def box_mirror_x(self, x, y, z, w, h, d, t, line):
        self.box(x, y, z, w, h, d, t, line)
        mx = self.sx - x - w
        if mx != x:
            self.box(mx, y, z, w, h, d, t, line)

    def box_mirror4(self, x, y, z, w, h, d, t, line):
        mx, mz = self.sx - x - w, self.sz - z - d
        self.box(x, y, z, w, h, d, t, line)
        if mx != x:
            self.box(mx, y, z, w, h, d, t, line)
        if mz != z:
            self.box(x, y, mz, w, h, d, t, line)
        if mx != x and mz != z:
            self.box(mx, y, mz, w, h, d, t, line)

    def block_at(self, cx, cy, cz):
        # Later blocks win, like MapData.BlockAt.
        for b in reversed(self.blocks):
            if b.contains(cx, cy, cz):
                return b
        return None


def strip_comments(src):
    src = re.sub(r"/\*.*?\*/", lambda m: "\n" * m.group(0).count("\n"), src, flags=re.S)
    return re.sub(r"//[^\n]*", "", src)


def split_args(s):
    args, depth, cur, in_str = [], 0, "", False
    for ch in s:
        if in_str:
            cur += ch
            if ch == '"':
                in_str = False
            continue
        if ch == '"':
            in_str = True
        elif ch == "(":
            depth += 1
        elif ch == ")":
            depth -= 1
        elif ch == "," and depth == 0:
            args.append(cur.strip())
            cur = ""
            continue
        cur += ch
    if cur.strip():
        args.append(cur.strip())
    return args


def find_call_end(src, open_idx):
    """Index just past the ')' matching the '(' at open_idx."""
    depth, i, in_str = 0, open_idx, False
    while i < len(src):
        ch = src[i]
        if in_str:
            if ch == '"':
                in_str = False
        elif ch == '"':
            in_str = True
        elif ch == "(":
            depth += 1
        elif ch == ")":
            depth -= 1
            if depth == 0:
                return i + 1
        i += 1
    raise ValueError("unbalanced parentheses at offset %d" % open_idx)


def eval_int(expr, env):
    expr = expr.strip()
    if not re.fullmatch(r"[\w\s+\-*/%()]+", expr):
        raise ValueError("unsupported expression: %r" % expr)
    names = set(re.findall(r"[A-Za-z_]\w*", expr))
    unknown = names - set(env)
    if unknown:
        raise ValueError("unknown identifiers %s in %r" % (sorted(unknown), expr))
    # C# integer division truncates toward zero; all operands here are small ints.
    return int(eval(expr.replace("/", "//"), {"__builtins__": {}}, dict(env)))


def eval_num(expr, env):
    expr = re.sub(r"(\d)f\b", r"\1", expr.strip())
    if not re.fullmatch(r"[\w\s+\-*/%().]+", expr):
        raise ValueError("unsupported expression: %r" % expr)
    names = set(re.findall(r"[A-Za-z_]\w*", expr))
    unknown = names - set(env)
    if unknown:
        raise ValueError("unknown identifiers %s in %r" % (sorted(unknown), expr))
    return float(eval(expr, {"__builtins__": {}}, dict(env)))


def eval_str(expr):
    m = re.fullmatch(r'\s*"([^"]*)"\s*', expr)
    if not m:
        raise ValueError("expected a string literal, got %r" % expr)
    return m.group(1)


def parse(path):
    raw = open(path, encoding="utf-8").read()
    src = strip_comments(raw)

    def line_of(idx):
        return src.count("\n", 0, idx) + 1

    # Registrations: MapRegistry.Register("id", Method(), "author")
    registered = []
    for m in re.finditer(r'MapRegistry\.Register\(\s*"([^"]+)"\s*,\s*(\w+)\(\)', src):
        registered.append((m.group(1), m.group(2)))

    maps, parse_errors = [], []
    method_re = re.compile(r"public\s+static\s+MapData\s+(\w+)\s*\(\s*\)\s*\{")
    for mm in method_re.finditer(src):
        method = mm.group(1)
        # Find the method body by brace matching.
        depth, i = 0, mm.end() - 1
        while True:
            if src[i] == "{":
                depth += 1
            elif src[i] == "}":
                depth -= 1
                if depth == 0:
                    break
            i += 1
        body_start, body_end = mm.end(), i
        body = src[body_start:body_end]

        nm = re.search(r"\bNew\s*\(", body)
        if not nm:
            parse_errors.append("%s: no New(...) call" % method)
            continue
        args = split_args(body[nm.end():find_call_end(body, nm.end() - 1) - 1])
        mp = Map(method, eval_str(args[0]), eval_int(args[2], {}), eval_int(args[3], {}),
                 eval_int(args[4], {}))
        env = {"sizeX": mp.sx, "sizeY": mp.sy, "sizeZ": mp.sz}

        for cm in re.finditer(r"\.\s*Mech\s*\(\s*MechKinds\.(\w+)", body):
            open_idx = body.index("(", cm.start())
            line = line_of(body_start + cm.start())
            try:
                a = split_args(body[open_idx + 1:find_call_end(body, open_idx) - 1])
                kind = cm.group(1)
                if kind not in MECH_PRESETS:
                    raise ValueError("unknown mech kind %s" % kind)
                x, y, z = (eval_int(v, env) for v in a[1:4])
                tweaks = {}
                if len(a) > 4:
                    for tm in re.finditer(r"\b\w+\.(\w+)\s*=\s*([^;]+);", a[4]):
                        key, val = tm.group(1), tm.group(2).strip()
                        if val in ("true", "false"):
                            tweaks[key] = val == "true"
                        elif val.startswith('"'):
                            tweaks[key] = eval_str(val)
                        else:
                            tweaks[key] = eval_num(val, env)
                mp.mechs.append(Mech(kind, x, y, z, line, tweaks))
            except (ValueError, KeyError) as e:
                parse_errors.append("%s L%d: %s" % (method, line, e))

        for cm in re.finditer(r"\.\s*(PropMirror4|PropStack|Prop)\s*\(", body):
            kind_call = cm.group(1)
            open_idx = cm.end() - 1
            line = line_of(body_start + cm.start())
            try:
                a = split_args(body[open_idx + 1:find_call_end(body, open_idx) - 1])
                km = re.fullmatch(r"\s*PropKinds\.(\w+)\s*", a[0])
                pk = km.group(1).lower() if km else eval_str(a[0])
                if pk not in PROP_KINDS:
                    raise ValueError("unknown prop kind %r" % pk)
                x, y, z = (eval_num(v, env) for v in a[1:4])
                pts = [(x, y, z)]
                if kind_call == "PropMirror4":
                    mx, mz = mp.sx - x, mp.sz - z
                    pts = [(x, y, z), (mx, y, z), (x, y, mz), (mx, y, mz)]
                elif kind_call == "PropStack":
                    n = eval_int(a[4], env)
                    size = PROP_KINDS[pk][0]
                    pts = [(x + (i - (n - 1) * 0.5) * 1.05 * size, y, z) for i in range(n)]
                for (px, py, pz) in pts:
                    mp.props.append((pk, px, py, pz, line))
            except ValueError as e:
                parse_errors.append("%s L%d: %s" % (method, line, e))

        for cm in re.finditer(r"\.\s*(BoxMirrorX|BoxMirror4|Box|Spawn)\s*\(", body):
            kind = cm.group(1)
            open_idx = cm.end() - 1
            line = line_of(body_start + cm.start())
            try:
                a = split_args(body[open_idx + 1:find_call_end(body, open_idx) - 1])
                if kind == "Spawn":
                    if len(a) != 4:
                        raise ValueError("Spawn expects 4 args")
                    x, y, z, team = (eval_int(v, env) for v in a)
                    mp.spawns.append((x, y, z, team, line))
                else:
                    if len(a) not in (6, 7):
                        raise ValueError("%s expects 6 or 7 args" % kind)
                    nums = [eval_int(v, env) for v in a[:6]]
                    t = eval_str(a[6]) if len(a) == 7 else "solid"
                    mp.calls += 1
                    {"Box": mp.box, "BoxMirrorX": mp.box_mirror_x,
                     "BoxMirror4": mp.box_mirror4}[kind](*nums, t, line)
            except ValueError as e:
                parse_errors.append("%s L%d: %s" % (method, line, e))
        maps.append(mp)
    return maps, registered, parse_errors


def check(mp):
    errors, warnings = [], []
    sx, sy, sz = mp.sx, mp.sy, mp.sz

    for b in mp.blocks:
        if b.type not in BLOCK_TYPES:
            errors.append("unknown block type '%s' %s" % (b.type, b))
        if b.x < 0 or b.y < 0 or b.z < 0 or b.x + b.w > sx or b.y + b.h > sy or b.z + b.d > sz:
            errors.append("block outside arena %s" % b)

    overlaps = []
    for i, a in enumerate(mp.blocks):
        for b in mp.blocks[i + 1:]:
            if a.overlaps(b):
                overlaps.append((a, b))
    for a, b in overlaps:
        warnings.append("overlap %s <-> %s" % (a, b))

    for mech in mp.mechs:
        lo, hi = mech.swept()
        if mech.x < 0 or mech.y < 0 or mech.z < 0 or mech.x >= sx or mech.y >= sy or mech.z >= sz:
            errors.append("moving part anchored outside arena %s" % mech)
        elif lo[0] < -0.5 or lo[2] < -0.5 or hi[0] > sx + 0.5 or hi[2] > sz + 0.5 or hi[1] > sy + 0.5:
            warnings.append("moving part reaches past the arena walls %s" % mech)

    for (pk, px, py, pz, line) in mp.props:
        tag = "prop %s(%.1f,%.1f,%.1f)@L%d" % (pk, px, py, pz, line)
        if not (0 <= px < sx and 0 <= py < sy and 0 <= pz < sz):
            errors.append("%s outside arena" % tag)
            continue
        cx, cy, cz = int(px), int(round(py)), int(pz)
        if mp.block_at(cx, cy, cz) is not None:
            errors.append("%s is inside %s" % (tag, mp.block_at(cx, cy, cz)))
        elif mp.block_at(cx, cy - 1, cz) is None:
            warnings.append("%s has no block under it (it will fall)" % tag)

    teams = {}
    seen = set()
    for (x, y, z, team, line) in mp.spawns:
        tag = "spawn(%d,%d,%d team %d)@L%d" % (x, y, z, team, line)
        teams[team] = teams.get(team, 0) + 1
        if (x, y, z) in seen:
            warnings.append("duplicate spawn cell %s" % tag)
        seen.add((x, y, z))
        if not (0 <= x < sx and 1 <= y and y + 1 < sy and 0 <= z < sz):
            errors.append("%s outside arena (needs 1 <= y and y+1 < %d)" % (tag, sy))
            continue
        below = mp.block_at(x, y - 1, z)
        if below is None:
            errors.append("%s is floating: no block under it" % tag)
        elif below.type == "hazard":
            errors.append("%s stands on hazard %s" % (tag, below))
        elif below.type == "bouncy":
            warnings.append("%s stands on bouncy %s" % (tag, below))
        elif below.type in BREAKABLE:
            warnings.append("%s stands on breakable %s" % (tag, below))
        for dy in (0, 1):
            occ = mp.block_at(x, y + dy, z)
            if occ is not None:
                errors.append("%s blocked at y=%d by %s" % (tag, y + dy, occ))
        for mech in mp.mechs:
            lo, hi = mech.swept()
            margin = 1.5 if mech.p.get("deadly") else 0.0
            if (lo[0] - margin <= x + 0.5 <= hi[0] + margin and lo[1] - margin <= y + 0.5 <= hi[1] + margin
                    and lo[2] - margin <= z + 0.5 <= hi[2] + margin):
                errors.append("%s is in the path of moving part %s" % (tag, mech))
        for (pk, px, py, pz, pline) in mp.props:
            if int(px) == x and int(pz) == z and abs(py - y) < 1.5:
                warnings.append("%s has a prop on it (L%d)" % (tag, pline))
        if min(x, z, sx - 1 - x, sz - 1 - z) < 2:
            warnings.append("%s within 2 cells of a side wall" % tag)

    for t in range(4):
        if teams.get(t, 0) < 1:
            errors.append("team %d has no spawn" % t)
    neutral = sum(n for t, n in teams.items() if t < 0 or t >= MAX_TEAMS)
    if neutral < 4:
        warnings.append("only %d neutral (team -1) spawns; teams 4-7 will use generated spawns"
                        % neutral)
    return errors, warnings, teams, neutral


def warning_kind(w):
    for key, kind in (("overlap", "overlap"), ("neutral", "few neutral spawns"),
                      ("bouncy", "spawn on bouncy"), ("breakable", "spawn on breakable"),
                      ("side wall", "spawn near wall"), ("duplicate", "duplicate spawn")):
        if key in w:
            return kind
    return "other"


def main():
    verbose = "-v" in sys.argv
    paths = [a for a in sys.argv[1:] if not a.startswith("-")]
    path = paths[0] if paths else DEFAULT_PATH
    maps, registered, parse_errors = parse(path)

    total_err, total_warn = len(parse_errors), 0
    for e in parse_errors:
        print("PARSE ERROR: " + e)

    reg_methods = {}
    ids = [r[0] for r in registered]
    for rid in set(ids):
        if ids.count(rid) > 1:
            print("ERROR: id '%s' registered %d times" % (rid, ids.count(rid)))
            total_err += 1
    for rid, meth in registered:
        reg_methods.setdefault(meth, []).append(rid)
    defined = {m.method for m in maps}
    for rid, meth in registered:
        if meth not in defined:
            print("ERROR: '%s' registers unknown method %s()" % (rid, meth))
            total_err += 1
        if not rid.startswith("builtin:"):
            print("WARNING: id '%s' does not start with builtin:" % rid)
            total_warn += 1

    print("%-22s %-20s %-10s %6s %6s %5s %5s %6s  %-22s %s" % (
        "id", "name", "size", "calls", "blocks", "mech", "props", "spawns", "teams(0-7,neutral)", "result"))
    details = []
    for mp in maps:
        errors, warnings, teams, neutral = check(mp)
        rid = ",".join(reg_methods.get(mp.method, []))
        if not rid:
            errors.append("method %s() is not registered in RegisterAll" % mp.method)
            rid = "(unregistered)"
        total_err += len(errors)
        total_warn += len(warnings)
        team_str = "".join(str(min(9, teams.get(t, 0))) for t in range(MAX_TEAMS)) + ",%d" % neutral
        result = "OK" if not errors else "%d ERROR(S)" % len(errors)
        if warnings:
            result += ", %d warning(s)" % len(warnings)
        print("%-22s %-20s %-10s %6d %6d %5d %5d %6d  %-22s %s" % (
            rid, mp.name[:20], "%dx%dx%d" % (mp.sx, mp.sy, mp.sz), mp.calls, len(mp.blocks),
            len(mp.mechs), len(mp.props), len(mp.spawns), team_str, result))
        details.append((rid, errors, warnings))

    for rid, errors, warnings in details:
        if errors or (warnings and verbose):
            print("\n[%s]" % rid)
            for e in errors:
                print("  ERROR:   " + e)
            for w in warnings if verbose else []:
                print("  warning: " + w)
        elif warnings:
            kinds = {}
            for w in warnings:
                k = warning_kind(w)
                kinds[k] = kinds.get(k, 0) + 1
            print("\n[%s] %d warning(s) (run with -v to list): %s" % (
                rid, len(warnings), ", ".join("%d %s" % (n, k) for k, n in sorted(kinds.items()))))

    print("\n%d map(s), %d error(s), %d warning(s)" % (len(maps), total_err, total_warn))
    return 1 if total_err else 0


if __name__ == "__main__":
    sys.exit(main())
