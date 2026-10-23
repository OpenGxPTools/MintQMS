"""Breadth-first crawl of every same-origin page reachable by GET links, logged in as admin.

Reports any response that is not 200/302/403/404-by-design, plus pages that render the
developer exception page. Usage: E2E_DB=... python3 crawl.py [max_pages]
"""
import collections, re
from urllib.parse import urljoin, urlparse
from bs4 import BeautifulSoup
from e2e_lib import *

MAX = int(sys.argv[1]) if len(sys.argv) > 1 else 1500
SKIP = re.compile(r"/(Logout|Download|Export|Print|Generate|Watermark|Preview|Pdf|Certificate/|Identity/Account/Manage/(DeletePersonalData|Disable|Reset))",
                  re.I)
s = login()
seen, queue, bad = set(), collections.deque(["/"]), []
per_pattern = collections.Counter()

while queue and len(seen) < MAX:
    path = queue.popleft()
    if path in seen:
        continue
    seen.add(path)
    try:
        r = s.get(BASE + path, allow_redirects=False, timeout=60)
    except Exception as e:  # noqa
        bad.append((path, f"EXC {e}"))
        continue
    ctype = r.headers.get("Content-Type", "")
    if r.status_code >= 500 or "An unhandled exception occurred" in r.text[:5000]:
        bad.append((path, r.status_code))
        continue
    if r.status_code != 200 or "text/html" not in ctype:
        continue
    for a in BeautifulSoup(r.text, "html.parser").find_all("a", href=True):
        u = urlparse(urljoin(BASE + path, a["href"]))
        if u.netloc != urlparse(BASE).netloc or not u.path or SKIP.search(u.path):
            continue
        p = u.path + (("?" + u.query) if u.query else "")
        # cap near-identical pages (e.g. thousands of record Details) at 15 per pattern
        pattern = re.sub(r"\d+", "N", u.path)
        if p not in seen and per_pattern[pattern] < 15:
            per_pattern[pattern] += 1
            queue.append(p)

print(f"crawled {len(seen)} pages")
for p, c in bad:
    print(f"  ERROR {c}  {p}")
check("crawl: no 5xx / exception pages", not bad, bad)
sys.exit(summary())
