#!/usr/bin/env python3
"""
i18n_wrap.py — wrap plain English text in Razor views with the translator: >Cancel<  →  >@L["Cancel"]<

Conservative by design: a text node is wrapped only if it is plain words and punctuation — anything
that contains @, braces, code-like characters or HTML entities other than &amp; is left alone and
listed, to be done by hand. <script> and <style> blocks and Razor comments are never touched.
The English text becomes the key, so the English page renders the same text as before.

    tools/i18n_wrap.py FILE...                    wrap every safe text node (and safe attributes)
    tools/i18n_wrap.py --keys KEYS.txt FILE...    wrap only texts listed in KEYS.txt (one per line)
    tools/i18n_wrap.py --dry-run ...              show what would change
    --attrs                                       also placeholder= / title= / aria-label= and the
                                                  value= of submit/button inputs

Always follow with: dotnet build, tools/i18n.py check, and the English page comparison.
"""
import argparse, html, re, sys

NEVER = {"MintQMS", "OpenQMS", "SHA-256", "AGPLv3"}   # names and identifiers, never translated
SAFE = re.compile(r"^(?:[A-Za-z0-9]|-- |— )[A-Za-z0-9 .,:;!?'’\"()/&%#+\-–—…·]*$")
PROTECT = re.compile(r"(@\*.*?\*@|<script\b.*?</script>|<style\b.*?</style>)", re.S | re.I)
TEXT_NODE = re.compile(r">([^<>]*?)<")
ATTR = re.compile(r'\b(placeholder|title|aria-label)="([^"@]*)"')
SUBMIT_VALUE = re.compile(r'(<input\b[^>]*\btype="(?:submit|button)"[^>]*\bvalue=")([^"@]*)(")|(<input\b[^>]*\bvalue=")([^"@]*)("[^>]*\btype="(?:submit|button)")')


def cs(s):
    return s.replace("\\", "\\\\").replace('"', '\\"')


def candidate(raw):
    """Return (lead, text, trail) if raw is a plain-text node worth wrapping, else None."""
    if not raw.strip():
        return None
    lead = raw[: len(raw) - len(raw.lstrip())]
    trail = raw[len(raw.rstrip()):]
    core = raw.strip()
    if "&" in core and re.search(r"&(?!amp;|mdash;|middot;|ndash;)[a-zA-Z#][a-zA-Z0-9]*;", core):
        return None                     # other entities: by hand
    text = " ".join(html.unescape(core).split()) if "\n" in core else html.unescape(core)
    if not re.search(r"[A-Za-z]{2}", text) or not SAFE.match(text):
        return None
    if text.startswith(("L[", "@")) or "=" in text or "{" in text or "}" in text:
        return None
    if re.match(r"(if|else|var|foreach|for|while|switch|return|case|break|using|new|await)\b[ (]", text) \
            or re.search(r"[a-z]\.[A-Z]|\w\(\)|\)\.\w|;", text):
        return None                     # C# statements inside @if/@foreach blocks, method calls
    if re.fullmatch(r"[\d\s.,:;%#+\-()/]+", text) or text in NEVER:
        return None
    return lead, text, trail


def protect_code_blocks(src):
    """Split src into [markup, code, markup, code, ...] where code = @{...} / @functions{...} blocks."""
    out, i, start = [], 0, 0
    while True:
        m = re.compile(r"@(?:functions\s*)?\{").search(src, i)
        if not m:
            break
        depth, j = 0, m.end() - 1
        while j < len(src):
            ch = src[j]
            if ch == '"':                      # skip C# string literals
                j += 1
                while j < len(src) and src[j] != '"':
                    j += 2 if src[j] == "\\" else 1
            elif ch == "{":
                depth += 1
            elif ch == "}":
                depth -= 1
                if depth == 0:
                    break
            j += 1
        out.append(src[start:m.start()]); out.append(src[m.start():j + 1])
        i = start = j + 1
    out.append(src[start:])
    return out


INLINE_AFTER = re.compile(r"^<(strong|em|b|a|code|u|abbr|kbd|mark|small)\b", re.I)
INLINE_BEFORE = re.compile(r"</(strong|em|b|a|code|u|abbr|kbd|mark|small)>$", re.I)


def wrap(src, only=None, attrs=False, skipped=None):
    chunks = protect_code_blocks(src)
    res = []
    for k, chunk in enumerate(chunks):
        if k % 2 == 1:
            res.append(chunk)
        else:
            res.append(_wrap_markup(chunk, only, attrs, skipped))
    return "".join(res)


def _wrap_markup(src, only=None, attrs=False, skipped=None):
    parts = PROTECT.split(src)
    out = []
    for i, part in enumerate(parts):
        if i % 2 == 1:                  # protected block
            out.append(part); continue

        def node(m, part=part):
            if m.start() > 0 and part[m.start() - 1] in "=-":
                return m.group(0)               # "=>" or "->": C#, not a tag
            ls = part.rfind("\n", 0, m.start()) + 1
            le = part.find("\n", m.end()); le = len(part) if le < 0 else le
            if re.search(r'Html\.Raw\(|\$"[^"\n]*<|"<[a-zA-Z/]|\\"', part[ls:le]):
                if skipped is not None and m.group(1).strip():
                    skipped.append("[HTML inside a C# string] " + m.group(1).strip()[:60])
                return m.group(0)               # markup built inside a C# string: by hand
            if INLINE_AFTER.match(part[m.end() - 1:m.end() + 12]) or INLINE_BEFORE.search(part[max(0, m.start() - 10):m.start() + 1]):
                if skipped is not None and m.group(1).strip():
                    skipped.append("[sentence with markup] " + m.group(1).strip()[:70])
                return m.group(0)               # part of a sentence with <strong>/<a>…: whole sentence by hand
            c = candidate(m.group(1))
            if not c:
                if skipped is not None and re.search(r"[A-Za-z]{3}", m.group(1)) and "@" not in m.group(1):
                    skipped.append(m.group(1).strip()[:80])
                return m.group(0)
            lead, text, trail = c
            if only is not None and text not in only:
                return m.group(0)
            return f'>{lead}@L["{cs(text)}"]{trail}<'
        part = TEXT_NODE.sub(node, part)

        if attrs:
            def at(m):
                text = html.unescape(m.group(2))
                if not text.strip() or not SAFE.match(text) or (only is not None and text not in only):
                    return m.group(0)
                return f'{m.group(1)}="@L["{cs(text)}"]"'
            part = ATTR.sub(at, part)

            def sv(m):
                g = m.groups()
                pre, val, post = (g[0], g[1], g[2]) if g[0] else (g[3], g[4], g[5])
                text = html.unescape(val)
                if not text.strip() or not SAFE.match(text) or (only is not None and text not in only):
                    return m.group(0)
                return f'{pre}@L["{cs(text)}"]{post}'
            part = SUBMIT_VALUE.sub(sv, part)
        out.append(part)
    return "".join(out)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("files", nargs="+")
    ap.add_argument("--keys")
    ap.add_argument("--attrs", action="store_true")
    ap.add_argument("--dry-run", action="store_true")
    ap.add_argument("--show-skipped", action="store_true")
    a = ap.parse_args()
    only = None
    if a.keys:
        only = {l.rstrip("\n") for l in open(a.keys, encoding="utf-8") if l.strip()}
    total = 0
    for f in a.files:
        raw = open(f, "rb").read()
        bom = raw.startswith(b"\xef\xbb\xbf")
        src = raw.decode("utf-8-sig")
        skipped = []
        new = wrap(src, only, a.attrs, skipped)
        n = new.count('@L["') - src.count('@L["')
        total += n
        if n and not a.dry_run:
            open(f, "wb").write((b"\xef\xbb\xbf" if bom else b"") + new.encode("utf-8"))
        if n or (a.show_skipped and skipped):
            print(f"{f}: {n} wrapped" + (f"; left for hand: {skipped[:6]}" if a.show_skipped and skipped else ""))
    print(f"total wrapped: {total}")


if __name__ == "__main__":
    main()
