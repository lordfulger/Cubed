#!/usr/bin/env python3
"""
Extract the translatable English strings from the Spheres C# sources.

The translation system (Core/Localization.cs) uses the English text itself as the lookup key,
so a string can only be translated when the exact text reaches Loc.T() on its own. This tool
finds those strings so the built-in tables (Core/LocTables.Generated.cs) or a community
*.spheresloc.json file can be kept up to date.

What counts as translatable:
  * the text/label argument of the UIKit widgets that call Loc.T (Text, Shadowed, Btn, Toggle,
    Stepper/StepperInt label and "zero text", Cycle label and every option, CheckRow label),
    following local variables, string-array fields, ternaries and string-returning helper methods
    (GameManager.ModeName, MechKinds.Name, Weather.Name, ...);
  * the Header("...") / Section("...") helpers in UI/*.cs;
  * text that is later drawn by a UIKit widget: kill-feed lines, banners, toasts, map editor
    status lines, network/Workshop status text;
  * data tables: quick-chat phrases, death-message templates, cosmetic/hat names, achievement
    titles and descriptions, character style names, block type names, game mode names.

Only literals that form a COMPLETE label are reported. Anything concatenated with another value
at the call site ("Round " + n) can never match a key and is skipped, as are whitespace-only
strings, strings with fewer than two letters (symbols, "X", "K"), interpolated strings and a few
proper nouns. Strings are reported verbatim, including leading spaces.

Usage:
  python3 Tools/extract_strings.py                 # JSON: [{"text": ..., "sources": [...]}, ...]
  python3 Tools/extract_strings.py --list          # one string per line (repr-escaped)
  python3 Tools/extract_strings.py --missing es    # strings with no add("es", ...) in LocTables*.cs
"""

import argparse
import json
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SCRIPTS = os.path.join(ROOT, "Assets", "Spheres", "Scripts")

# UIKit widgets that pass their text through Loc.T: name -> argument indexes holding text.
# Stepper: (r, label, value, step, min, max, format, suffix, zeroText); StepperInt: (r, label, value, min, max, zeroText).
WIDGETS = {
    "Text": [1], "Shadowed": [1], "Btn": [1], "Toggle": [1], "CheckRow": [1],
    "Stepper": [1, 8], "StepperInt": [1, 5], "Cycle": [1, 3],
}
# Calls whose text ends up drawn by a UIKit widget: (file glob or None, call regex, argument indexes).
SINKS = [
    (None, r"(?<![\w.])(?:SpheresHud\.)?AddFeedText(?:Local)?\s*\(", [0]),
    (None, r"(?<![\w.])(?:SpheresHud\.)?ShowBanner\s*\(", [0]),
    (None, r"\bToasts\.Show\s*\(", [0, 1]),
    ("Maps/MapEditor.cs", r"(?<![\w.])Status\s*\(", [0]),
    ("UI/", r"(?<![\w.])(?:Header|Section)\s*\(", [0]),
    ("Core/DeathMessages.cs", r"(?<![\w.])AddAll\s*\(", "rest"),
    ("Maps/BlockTypes.cs", r"\bnew\s+BlockType\s*\(", [1]),
]
# Property assignments shown verbatim by a widget: (file, property name).
SINK_ASSIGNMENTS = [("Net/NetSession.cs", "Status"), ("Net/SteamWorkshop.cs", "Status")]
# Object-initializer fields that hold display text: (file, field name).
DATA_FIELDS = [("Core/Cosmetics.cs", "Name"), ("Core/Profile.cs", "Name"),
               ("Core/Profile.cs", "Title"), ("Core/Profile.cs", "Description")]
# String-array fields / string methods whose values are always display text: (class, member).
DATA_MEMBERS = [("Signals", "Phrases"), ("CharacterLook", "EyeStyles"), ("CharacterLook", "MouthStyles"),
                ("CharacterLook", "Details"), ("GameManager", "ModeName"), ("GameManager", "ModeDescription"),
                ("MechKinds", "Name")]

PROPER_NOUNS = {"Spheres", "SPHERES", "Steam", "BepInEx", "Thunderstore", "r2modman"}
SKIP_PATTERNS = [re.compile(r"^\d+v\d+(v\d+)*$"), re.compile(r"^\d+ FFA$")]

TOKEN = re.compile(r"__S(\d+)__")


# ---------------------------------------------------------------------------------------- lexing

class Source:
    """A .cs file with comments removed and every string literal replaced by a __S<n>__ token."""

    def __init__(self, path, rel, literals):
        self.path, self.rel = path, rel
        with open(path, encoding="utf-8-sig") as f:
            text = f.read()
        self.text = self._mask(text, literals)
        self.literals = literals

    @staticmethod
    def _mask(src, literals):
        out, i, n = [], 0, len(src)
        while i < n:
            c = src[i]
            if src.startswith("//", i):
                j = src.find("\n", i)
                j = n if j < 0 else j
                out.append(" " * (j - i)); i = j
            elif src.startswith("/*", i):
                j = src.find("*/", i + 2)
                j = n if j < 0 else j + 2
                out.append(re.sub(r"[^\n]", " ", src[i:j])); i = j
            elif c == "'":
                j = i + 1
                while j < n and src[j] != "'":
                    j += 2 if src[j] == "\\" else 1
                out.append("__C__"); i = j + 1
            elif c == '"' or (c in "@$" and src[i + 1:i + 2] in ('"', "@", "$")):
                j, prefix = i, ""
                while src[j] in "@$":
                    prefix += src[j]; j += 1
                verbatim, interp = "@" in prefix, "$" in prefix
                j += 1
                buf = []
                while j < n:
                    ch = src[j]
                    if verbatim and ch == '"':
                        if src[j + 1:j + 2] == '"':
                            buf.append('"'); j += 2; continue
                        break
                    if not verbatim and ch == "\\":
                        buf.append(_unescape(src[j:j + 6])); j += _escape_len(src, j); continue
                    if not verbatim and ch == '"':
                        break
                    buf.append(ch); j += 1
                raw = src[i:j + 1]
                newlines = "\n" * raw.count("\n")
                if interp:
                    # Interpolated strings are dynamic, so never keys. (Nested quotes inside
                    # {holes} are not supported; the sources currently use no $"..." at all.)
                    out.append("__I__" + newlines)
                else:
                    literals.append("".join(buf))
                    out.append("__S%d__%s" % (len(literals) - 1, newlines))
                i = j + 1
            else:
                out.append(c); i += 1
        return "".join(out)

    def line(self, pos):
        return self.text.count("\n", 0, pos) + 1


def _escape_len(s, j):
    e = s[j + 1:j + 2]
    if e == "u":
        return 6
    if e == "x":
        k = j + 2
        while k < len(s) and k < j + 6 and s[k] in "0123456789abcdefABCDEF":
            k += 1
        return k - j
    return 2


def _unescape(seq):
    e = seq[1:2]
    simple = {"n": "\n", "t": "\t", "r": "\r", "0": "\0", "\\": "\\", '"': '"', "'": "'", "a": "\a", "b": "\b", "f": "\f", "v": "\v"}
    if e in simple:
        return simple[e]
    if e == "u":
        return chr(int(seq[2:6], 16))
    if e == "x":
        m = re.match(r"[0-9a-fA-F]{1,4}", seq[2:])
        return chr(int(m.group(0), 16))
    return e


# ---------------------------------------------------------------------------------------- parsing helpers

OPEN, CLOSE = "([{", ")]}"


def match_close(text, pos):
    """pos is at an opening bracket; returns the index of its partner."""
    depth = 0
    for k in range(pos, len(text)):
        ch = text[k]
        if ch in OPEN:
            depth += 1
        elif ch in CLOSE:
            depth -= 1
            if depth == 0:
                return k
    return len(text) - 1


def split_top(text, sep=","):
    parts, depth, start = [], 0, 0
    for k, ch in enumerate(text):
        if ch in OPEN:
            depth += 1
        elif ch in CLOSE:
            depth -= 1
        elif ch == sep and depth == 0:
            parts.append(text[start:k]); start = k + 1
    parts.append(text[start:])
    return parts


def call_args(text, open_paren):
    close = match_close(text, open_paren)
    inner = text[open_paren + 1:close]
    return ([] if not inner.strip() else split_top(inner)), close


def statement_end(text, pos):
    depth = 0
    for k in range(pos, len(text)):
        ch = text[k]
        if ch in OPEN:
            depth += 1
        elif ch in CLOSE:
            depth -= 1
            if depth < 0:
                return k
        elif ch == ";" and depth == 0:
            return k
    return len(text)


def strip_parens(e):
    e = e.strip()
    while e.startswith("(") and match_close(e, 0) == len(e) - 1:
        e = e[1:-1].strip()
    return e


def find_ternary(e):
    """Returns (q, colon) indexes of a top-level conditional operator, or None."""
    depth, k = 0, 0
    while k < len(e):
        ch = e[k]
        if ch in OPEN:
            depth += 1
        elif ch in CLOSE:
            depth -= 1
        elif ch == "?" and depth == 0:
            nxt = e[k + 1:k + 2]
            if nxt in ("?", ".", "["):
                k += 2; continue
            if e[k - 1:k] == "?":
                k += 1; continue
            # Nullable type (e.g. "Color?") is not an operator in an expression context we care about.
            nested, d2 = 0, 0
            for m in range(k + 1, len(e)):
                c2 = e[m]
                if c2 in OPEN:
                    d2 += 1
                elif c2 in CLOSE:
                    d2 -= 1
                elif d2 == 0 and c2 == "?" and e[m + 1:m + 2] not in ("?", ".", "[") and e[m - 1:m] != "?":
                    nested += 1
                elif d2 == 0 and c2 == ":":
                    if nested == 0:
                        return k, m
                    nested -= 1
            return None
        k += 1
    return None


def top_level_has(e, op):
    depth = 0
    for k, ch in enumerate(e):
        if ch in OPEN:
            depth += 1
        elif ch in CLOSE:
            depth -= 1
        elif depth == 0 and e.startswith(op, k):
            if op == "+" and e[k + 1:k + 2] in ("+", "="):
                continue
            return True
    return False


def enclosing_block_end(text, pos):
    """End index of the innermost { } block containing pos (len(text) at file level)."""
    depth = 0
    for k in range(pos - 1, -1, -1):
        ch = text[k]
        if ch == "}":
            depth += 1
        elif ch == "{":
            if depth == 0:
                return match_close(text, k)
            depth -= 1
    return len(text)


def is_parameter(text, name, pos):
    """True when `name` is declared by a method, lambda or foreach header around pos."""
    k = pos
    while k > 0:
        depth, open_at = 0, -1
        for j in range(k - 1, -1, -1):
            ch = text[j]
            if ch == "}":
                depth += 1
            elif ch == "{":
                if depth == 0:
                    open_at = j
                    break
                depth -= 1
        if open_at < 0:
            return False
        head = text[max(0, open_at - 300):open_at].rstrip()
        if head.endswith("=>"):
            head = head[:-2].rstrip()
        if head.endswith(")"):
            depth = 0
            for j in range(len(head) - 1, -1, -1):
                if head[j] == ")":
                    depth += 1
                elif head[j] == "(":
                    depth -= 1
                    if depth == 0:
                        params = head[j + 1:-1]
                        if re.search(r"(?:\b[\w<>\[\],.?]+\s+|[(,]\s*)%s\s*(?:[,)=]|$|\s+in\b)" % re.escape(name), params):
                            return True
                        break
        k = open_at
    return False


# ---------------------------------------------------------------------------------------- resolver

class Extractor:
    def __init__(self, root):
        self.literals = []
        self.files = []
        for dirpath, _, names in os.walk(root):
            for name in sorted(names):
                if name.endswith(".cs"):
                    path = os.path.join(dirpath, name)
                    rel = os.path.relpath(path, root).replace(os.sep, "/")
                    if rel.startswith("Core/LocTables"):
                        continue
                    self.files.append(Source(path, rel, self.literals))
        self.files.sort(key=lambda s: s.rel)
        self.found = {}

    # -- recording

    def add(self, text, src, pos):
        if not keep(text):
            return
        self.found.setdefault(text, set()).add("%s:%d" % (src.rel, src.line(pos)))

    # -- classes / members

    def class_files(self, cls):
        pat = re.compile(r"\b(?:class|struct)\s+%s\b" % re.escape(cls))
        return [s for s in self.files if pat.search(s.text)]

    def method_returns(self, src, name, depth):
        out = []
        for m in re.finditer(r"\bstring\s+%s\s*\(" % re.escape(name), src.text):
            close = match_close(src.text, m.end() - 1)
            rest = src.text[close + 1:].lstrip()
            body_start = close + 1 + (len(src.text[close + 1:]) - len(rest))
            if rest.startswith("=>"):
                end = statement_end(src.text, body_start + 2)
                out += self.complete(src.text[body_start + 2:end], src, body_start, depth + 1)
            elif rest.startswith("{"):
                end = match_close(src.text, body_start)
                body = src.text[body_start:end]
                for r in re.finditer(r"\breturn\b", body):
                    e = statement_end(body, r.end())
                    out += self.complete(body[r.end():e], src, body_start + r.start(), depth + 1)
        return out

    DECL = r"(?:\b(?:public|private|internal|protected|static|readonly|const)\s+)*(?:\bstring\s*(?:\[\s*\])?|\bvar)\s+%s\s*=(?!=)"

    def field_values(self, src, name, use_pos, depth, whole_file=False):
        """Values of a variable/field `name` visible at use_pos (or anywhere in the file)."""
        text = src.text
        decls = [m for m in re.finditer(self.DECL % re.escape(name), text)]
        values, lo, hi = [], 0, len(text)
        if not whole_file:
            if is_parameter(text, name, use_pos):
                return []
            local = [m for m in decls if m.start() < use_pos and use_pos < enclosing_block_end(text, m.start())]
            fields = [m for m in decls if re.match(r"(?:public|private|internal|protected|static|readonly|const)\b", text[m.start():])]
            if local:
                d = local[-1]
                decls, lo, hi = [d], d.start(), use_pos
            elif fields:
                decls = fields[:1]
            else:
                return []
        for d in decls:
            end = statement_end(text, d.end())
            values += self.complete(text[d.end():end], src, d.end(), depth + 1)
        region = text[lo:hi]
        if not whole_file and re.search(r"(?<![\w.])%s\s*\+=" % re.escape(name), region):
            return []
        for a in re.finditer(r"(?<![\w.])%s\s*(?:\[[^\]]*\])?\s*=(?![=>])" % re.escape(name), region):
            if any(lo + a.start() >= d.start() and lo + a.start() < d.end() for d in decls):
                continue
            start = lo + a.end()
            end = statement_end(text, start)
            values += self.complete(text[start:end], src, start, depth + 1)
        return values

    def complete(self, expr, src, pos, depth=0):
        """Complete string values an expression can evaluate to."""
        if depth > 12:
            return []
        e = strip_parens(expr)
        if not e:
            return []
        t = find_ternary(e)
        if t:
            q, colon = t
            return self.complete(e[q + 1:colon], src, pos, depth + 1) + self.complete(e[colon + 1:], src, pos, depth + 1)
        if top_level_has(e, "??"):
            left, right = e.split("??", 1)
            return self.complete(left, src, pos, depth + 1) + self.complete(right, src, pos, depth + 1)
        if top_level_has(e, "+"):
            parts = split_top(e, "+")
            if all(TOKEN.fullmatch(p.strip()) for p in parts):
                return ["".join(self.literals[int(TOKEN.fullmatch(p.strip()).group(1))] for p in parts)]
            return []
        m = TOKEN.fullmatch(e)
        if m:
            return [self.literals[int(m.group(1))]]
        m = re.match(r"(?:new\s*(?:string\s*)?\[\s*\]\s*)?\{", e)
        if m and e.endswith("}"):
            return [v for item in split_top(e[m.end():-1]) for v in self.complete(item, src, pos, depth + 1)]
        # xs.Select(Class.Method).ToArray() with a method group.
        m = re.fullmatch(r"[\w.]+\s*\.\s*Select\s*\(\s*([\w.]+)\s*\)\s*\.\s*ToArray\s*\(\s*\)", e)
        if m:
            return self.resolve_member(m.group(1), src, depth)
        # Call: Class.Method(...) or Method(...)
        m = re.fullmatch(r"([\w.]+)\s*\((.*)\)", e, re.S)
        if m and match_close(e, e.index("(")) == len(e) - 1:
            if m.group(1).split(".")[-1] in ("ToString", "ToUpperInvariant", "ToLowerInvariant", "Format", "Join", "T", "F"):
                return []
            return self.resolve_member(m.group(1), src, depth, method_only=True)
        # Identifier, optionally qualified / indexed: a, a[i], A.b, A.b[i]
        m = re.fullmatch(r"([\w.]+)\s*(?:\[.*\])?", e, re.S)
        if m:
            name = m.group(1)
            if "." not in name:
                return self.field_values(src, name, pos, depth)
            return self.resolve_member(name, src, depth)
        return []

    def resolve_member(self, qualified, src, depth, method_only=False):
        parts = qualified.split(".")
        name = parts[-1]
        owners = [p for p in parts[:-1] if p and p[0].isupper()]
        if not owners:
            return self.method_returns(src, name, depth) if method_only else []
        cls = owners[-1]
        out = []
        for f in self.class_files(cls):
            out += self.method_returns(f, name, depth)
            if not method_only:
                out += self.field_values(f, name, None, depth, whole_file=True)
        return out

    # -- scanning

    def scan(self):
        widget_re = re.compile(r"\bUIKit\s*\.\s*(%s)\s*\(" % "|".join(WIDGETS))
        for src in self.files:
            if src.rel == "UI/UIKit.cs":
                continue
            text = src.text
            for m in widget_re.finditer(text):
                args, _ = call_args(text, m.end() - 1)
                for idx in WIDGETS[m.group(1)]:
                    if idx < len(args):
                        for v in self.complete(args[idx], src, m.start()):
                            self.add(v, src, m.start())
            for scope, pattern, idxs in SINKS:
                if scope and not src.rel.startswith(scope):
                    continue
                for m in re.finditer(pattern, text):
                    before = text[max(0, m.start() - 40):m.start()]
                    if re.search(r"\b(void|string|static)\s+$", before):
                        continue  # the declaration itself
                    args, _ = call_args(text, m.end() - 1)
                    use = range(1, len(args)) if idxs == "rest" else idxs
                    for idx in use:
                        if idx < len(args):
                            for v in self.complete(args[idx], src, m.start()):
                                self.add(v, src, m.start())
            for rel, prop in SINK_ASSIGNMENTS:
                if src.rel == rel:
                    for a in re.finditer(r"(?<![\w.])%s\s*=(?![=>])" % prop, text):
                        end = statement_end(text, a.end())
                        for v in self.complete(text[a.end():end], src, a.end()):
                            self.add(v, src, a.start())
            for rel, field in DATA_FIELDS:
                if src.rel == rel:
                    for a in re.finditer(r"\b%s\s*=\s*(__S\d+__)" % field, text):
                        self.add(self.literals[int(a.group(1)[3:-2])], src, a.start())
        for cls, member in DATA_MEMBERS:
            for f in self.class_files(cls):
                for v in self.method_returns(f, member, 0) + self.field_values(f, member, None, 0, whole_file=True):
                    self.add(v, f, f.text.find(member))
        return self.found


def keep(text):
    if not text.strip():
        return False
    if sum(ch.isalpha() for ch in text) < 2:
        return False
    if text.strip() in PROPER_NOUNS or any(p.match(text) for p in SKIP_PATTERNS):
        return False
    return True


def existing_keys(lang):
    keys = set()
    core = os.path.join(SCRIPTS, "Core")
    lits = []
    for name in os.listdir(core):
        if name.startswith("LocTables") and name.endswith(".cs"):
            src = Source(os.path.join(core, name), name, lits)
            for m in re.finditer(r"\badd\s*\(\s*__S(\d+)__\s*,\s*__S(\d+)__", src.text):
                if lits[int(m.group(1))] == lang:
                    keys.add(lits[int(m.group(2))])
    return keys


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--list", action="store_true", help="print one string per line instead of JSON")
    ap.add_argument("--missing", metavar="LANG", help="only strings with no built-in translation for LANG")
    ap.add_argument("--root", default=SCRIPTS, help="scripts folder (default: Assets/Spheres/Scripts)")
    args = ap.parse_args()

    found = Extractor(args.root).scan()
    keys = sorted(found, key=lambda s: (min(found[s]), s))
    if args.missing:
        have = existing_keys(args.missing)
        keys = [k for k in keys if k not in have]
    if args.list:
        for k in keys:
            print(json.dumps(k, ensure_ascii=False))
    else:
        json.dump([{"text": k, "sources": sorted(found[k])} for k in keys], sys.stdout, ensure_ascii=False, indent=1)
        print()
    print("%d strings" % len(keys), file=sys.stderr)


if __name__ == "__main__":
    main()
