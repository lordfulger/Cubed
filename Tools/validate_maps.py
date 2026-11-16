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

BLOCK_TYPES = {"solid", "light", "dark", "bouncy", "hazard", "ice", "glass", "crate", "grass"}
BREAKABLE = {"glass", "crate"}
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

    print("%-22s %-20s %-10s %6s %6s %6s  %-22s %s" % (
        "id", "name", "size", "calls", "blocks", "spawns", "teams(0-7,neutral)", "result"))
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
        print("%-22s %-20s %-10s %6d %6d %6d  %-22s %s" % (
            rid, mp.name[:20], "%dx%dx%d" % (mp.sx, mp.sy, mp.sz), mp.calls, len(mp.blocks),
            len(mp.spawns), team_str, result))
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
