"""Bundled browser libraries: every file unchanged (wwwroot/lib/libraries.json fingerprints) and no page loads a script
or stylesheet from another website — checked in the source (tools/check_libraries.py --offline) and in the pages
actually served. The weekly GitHub workflow also checks for newer versions and known vulnerabilities."""
import re, subprocess
from e2e_lib import *

tool = os.path.join(os.environ.get("E2E_APP_ROOT", os.path.join(os.path.dirname(__file__), "..", "..")), "tools", "check_libraries.py")
r = subprocess.run([sys.executable, tool, "--offline"], capture_output=True, text=True)
check("library files unchanged and no outside code in the page sources (check_libraries --offline)", r.returncode == 0, r.stdout[-800:])

s = login()
outside = re.compile(r"""<(script|link)\b[^>]*\b(src|href)\s*=\s*["'](https?:)?//""", re.I)
for page in ("/", "/Deviations", "/Deviations/Dashboard", "/Audits/Dashboard", "/LicencesCertifications/Dashboard",
             "/LicencesCertifications/Calendar", "/LicencesCertifications/Create", "/RegulatoryIntelligence/Create",
             "/Reports", "/Identity/Account/Manage/Email"):
    html = get(s, page).text
    found = [m.group(0) for m in outside.finditer(html)]
    check(f"{page}: loads only MintQMS's own files", not found, found[:2])
    for src in re.findall(r'<script[^>]+src="(/lib/[^"?]+)', html):
        check(f"{page}: {src} is served", s.get(BASE + src).status_code == 200)
sys.exit(summary())
