"""Every Export/Download link found on the module index pages returns a file (not an error).

Exercises ClosedXML/OpenXML (System.IO.Packaging) and the CSV exporters.
"""
import collections, re
from urllib.parse import urljoin, urlparse
from bs4 import BeautifulSoup
from e2e_lib import *

s = login()
home = get(s, "/").text
index_pages = {urlparse(urljoin(BASE, a["href"])).path
               for a in BeautifulSoup(home, "html.parser").find_all("a", href=True)
               if a["href"].startswith("/") and "Logout" not in a["href"]}
exports = collections.OrderedDict()
for p in sorted(index_pages):
    r = get(s, p)
    for a in BeautifulSoup(r.text, "html.parser").find_all("a", href=True):
        if re.search(r"Export", a["href"], re.I) and a["href"].startswith("/"):
            exports.setdefault(a["href"], p)

print(f"{len(exports)} export links")
for href, src in exports.items():
    r = s.get(BASE + href, allow_redirects=False)
    ctype = r.headers.get("Content-Type", "")
    printable = "Attendance" in href          # printable HTML sheet, not a file download
    ok = r.status_code == 200 and len(r.content) > 0 and (printable or "text/html" not in ctype)
    if ok and "spreadsheetml" in ctype:
        ok = r.content[:2] == b"PK"
    check(f"export {href}", ok, f"{r.status_code} {ctype} (from {src})")

sys.exit(summary())
