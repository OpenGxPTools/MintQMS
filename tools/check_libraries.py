#!/usr/bin/env python3
"""Check the third-party browser libraries bundled in wwwroot/lib (MintQMS never loads code from other websites).

    python3 tools/check_libraries.py            full check (needs internet for steps 3 and 4)
    python3 tools/check_libraries.py --offline  steps 1 and 2 only (no network)
    python3 tools/check_libraries.py --record   after a deliberate library update: record the new file fingerprints
    add --ipv4 on a network where IPv6 does not work

1. Integrity: every file of every library in wwwroot/lib/libraries.json still has its recorded SHA-256.
2. Self-contained: no page (Views, Areas) loads a script or stylesheet from another website.
3. Updates: the newest version of each library in the npm registry (where the libraries are published).
4. Security: known vulnerabilities for the exact versions in use (npm registry advisory service).

Only library names and versions are sent to the registry — nothing about the site or its data.
Exit code 1 when a file changed, a page loads outside code or a version in use has a known vulnerability;
newer versions alone are reported but do not fail the check. Writes a Markdown report to $GITHUB_STEP_SUMMARY
when set (GitHub Actions).
"""
import hashlib, json, os, re, socket, sys, urllib.request

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
LIB = os.path.join(ROOT, "wwwroot", "lib")
MANIFEST = os.path.join(LIB, "libraries.json")
REGISTRY = "https://registry.npmjs.org"
EXTERNAL = re.compile(r"""<(script|link)\b[^>]*\b(src|href)\s*=\s*["'](https?:)?//""", re.I)
COMMENT = re.compile(r"@\*.*?\*@|<!--.*?-->", re.S)


def sha256(path):
    with open(path, "rb") as f:
        return hashlib.sha256(f.read()).hexdigest()


def lib_files(folder):
    base = os.path.join(LIB, folder)
    for d, _, files in os.walk(base):
        for name in sorted(files):
            yield os.path.relpath(os.path.join(d, name), LIB).replace(os.sep, "/")


def fetch(url, data=None):
    req = urllib.request.Request(url, data=json.dumps(data).encode() if data is not None else None,
                                 headers={"Content-Type": "application/json", "User-Agent": "mintqms-library-check"})
    with urllib.request.urlopen(req, timeout=60) as r:
        return json.load(r)


def version_key(v):
    parts = re.findall(r"\d+|[a-z]+", v.lower())
    return [int(p) if p.isdigit() else -1 for p in parts]


def main():
    args = set(sys.argv[1:])
    if "--ipv4" in args:
        orig = socket.getaddrinfo
        socket.getaddrinfo = lambda *a, **k: [i for i in orig(*a, **k) if i[0] == socket.AF_INET]
    libs = json.load(open(MANIFEST, encoding="utf-8"))
    report, fail = [], False

    if "--record" in args:
        for lib in libs:
            lib["files"] = {p: sha256(os.path.join(LIB, p)) for p in lib_files(lib["folder"])}
        with open(MANIFEST, "w", encoding="utf-8") as f:
            json.dump(libs, f, indent=1, ensure_ascii=False)
            f.write("\n")
        print(f"Recorded fingerprints of {sum(len(l['files']) for l in libs)} files in {len(libs)} libraries.")
        return 0

    # 1. integrity
    changed, missing, unlisted = [], [], []
    for lib in libs:
        on_disk = set(lib_files(lib["folder"]))
        for p, h in lib.get("files", {}).items():
            if not os.path.exists(os.path.join(LIB, p)):
                missing.append(p)
            elif sha256(os.path.join(LIB, p)) != h:
                changed.append(p)
        unlisted += sorted(on_disk - set(lib.get("files", {})))
    folders = {l["folder"] for l in libs}
    stray = [d for d in sorted(os.listdir(LIB)) if os.path.isdir(os.path.join(LIB, d)) and d not in folders]
    report.append("## 1. Integrity of the bundled files")
    if changed or missing or unlisted or stray:
        fail = True
        report += [f"- **changed:** `{p}`" for p in changed] + [f"- **missing:** `{p}`" for p in missing] \
            + [f"- **not recorded:** `{p}`" for p in unlisted] + [f"- **folder not in the manifest:** `{d}`" for d in stray]
        report.append("If a library was updated on purpose, run `python3 tools/check_libraries.py --record`.")
    else:
        report.append(f"- All {sum(len(l['files']) for l in libs)} files of {len(libs)} libraries unchanged.")

    # 2. no outside code
    outside = []
    for top in ("Views", "Areas"):
        for d, _, files in os.walk(os.path.join(ROOT, top)):
            for name in files:
                if name.endswith(".cshtml"):
                    path = os.path.join(d, name)
                    text = COMMENT.sub("", open(path, encoding="utf-8").read())
                    for m in EXTERNAL.finditer(text):
                        outside.append(f"{os.path.relpath(path, ROOT)}: {m.group(0)[:90]}")
    report.append("\n## 2. Pages load only MintQMS's own files")
    if outside:
        fail = True
        report += [f"- **outside file:** `{o}`" for o in outside]
    else:
        report.append("- No page loads a script or stylesheet from another website.")

    # 3 and 4. registry
    if "--offline" not in args:
        report.append("\n## 3. Newer versions\n\n| Library | In use | Newest | Note |\n|---|---|---|---|")
        bulk = {}
        entries = []
        for lib in libs:
            entries.append((lib["name"], lib.get("npm"), lib.get("version"), lib.get("source", "")))
            entries += [(f"{lib['name']}: {pkg}", pkg, ver, "") for pkg, ver in lib.get("parts", {}).items()]
        for name, pkg, ver, source in entries:
            lib = {"name": name}
            if not pkg:
                report.append(f"| {name} | {ver} | — | not published on npm; check {source} |")
                continue
            try:
                latest = fetch(f"{REGISTRY}/{pkg}/latest")["version"]
            except Exception as e:
                report.append(f"| {lib['name']} | {ver} | ? | registry not reachable: {e} |")
                continue
            note = "up to date" if ver == latest else (
                "**newer major version** — test carefully before updating" if version_key(latest)[:1] != version_key(ver)[:1]
                else "newer version available")
            if ver and not re.match(r"^\d", ver):
                note = "version unknown — identify it before updating"
            report.append(f"| {lib['name']} | {ver} | {latest} | {note} |")
            if ver and re.match(r"^\d", ver):
                bulk.setdefault(pkg, []).append(ver)
        report.append("\n## 4. Known vulnerabilities in the versions in use")
        try:
            adv = fetch(f"{REGISTRY}/-/npm/v1/security/advisories/bulk", bulk)
        except Exception as e:
            adv = None
            fail = True
            report.append(f"- Advisory service not reachable: {e}")
        if adv is not None:
            found = [(pkg, a) for pkg, items in adv.items() for a in items]
            if found:
                fail = True
                for pkg, a in found:
                    report.append(f"- **{pkg}** ({a.get('severity', '?')}): {a.get('title', '')} — affects {a.get('vulnerable_versions', '?')}; "
                                  f"{a.get('url', '')}")
            else:
                report.append("- None known for the versions in use.")

    text = "# MintQMS library check\n\n" + "\n".join(report) + "\n"
    print(text)
    if os.environ.get("GITHUB_STEP_SUMMARY"):
        with open(os.environ["GITHUB_STEP_SUMMARY"], "a", encoding="utf-8") as f:
            f.write(text)
    return 1 if fail else 0


if __name__ == "__main__":
    sys.exit(main())
