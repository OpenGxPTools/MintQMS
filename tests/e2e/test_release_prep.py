"""Public-release preparation fixes.

- RegisterConfirmation is Administrator-only: anonymous visitors can no longer probe which
  e-mail addresses have accounts or obtain an account's confirmation link.
- Public About page (disclaimer, optional source-code link via E2E_SOURCE_URL); leftover OpenQMS
  pages (Terms, UnderConstruction, UpgradePlan) removed.
- Forwarded headers: behind a reverse proxy (X-Forwarded-Proto: https) generated links use https.
- Image watermarking still works after the package changes: the
  header banner, footer and (for deleted attachments) the diagonal mark are drawn; only PNG/JPEG/GIF
  content is decoded (a TIFF renamed to .png is served unchanged).

Needs E2E_APP_ROOT = the running app's content root (an image attachment is planted there).
Optional E2E_OUT = folder to save the watermarked images for a visual check.
"""
import hashlib, io, os
from PIL import Image
from e2e_lib import *

APP_ROOT = os.environ.get("E2E_APP_ROOT") or sys.exit("E2E_APP_ROOT is required")
OUT = os.environ.get("E2E_OUT")          # optional: folder to save the watermarked images for a visual check
run = uuid.uuid4().hex[:6]
NOW = "2026-09-01 00:00:00"

# ── RegisterConfirmation access ─────────────────────────────────────────────
anon = requests.Session()
r = anon.get(BASE + f"/Identity/Account/RegisterConfirmation?email={ADMIN}", allow_redirects=False)
check("anonymous RegisterConfirmation redirected to login",
      r.status_code == 302 and "/Identity/Account/Login" in r.headers.get("Location", ""), r.status_code)
check("anonymous gets no confirmation link", "ConfirmEmail" not in r.text)
r = anon.get(BASE + "/Identity/Account/RegisterConfirmation?email=nobody@example.invalid", allow_redirects=False)
check("anonymous cannot tell unknown addresses apart", r.status_code == 302, r.status_code)

# ── Public pages: disclaimer, source-code offer, OpenQMS leftovers removed ───
r = anon.get(BASE + "/Home/About", allow_redirects=False)
check("About page is public", r.status_code == 200, r.status_code)
check("About page carries the disclaimer", "not validated" in r.text and "Australian Consumer Law" in r.text)
src = os.environ.get("E2E_SOURCE_URL")      # set when the app was started with SourceCodeUrl
if src:
    check("About page links the source code", src in r.text)
r = anon.get(BASE + "/Identity/Account/Login")
check("login page footer links About & disclaimer", "About &amp; disclaimer" in r.text and "/Home/About" in r.text)
if src:
    check("login page footer links the source code", src in r.text)
for gone in ("Terms", "UnderConstruction", "UpgradePlan"):
    r = anon.get(BASE + f"/Home/{gone}", allow_redirects=False)
    check(f"leftover OpenQMS page /Home/{gone} removed", r.status_code in (302, 404), r.status_code)
    check(f"/Home/{gone} not served", "c-realize.com" not in r.text)

s = login()
for gone in ("Terms", "UnderConstruction", "UpgradePlan"):
    r = s.get(BASE + f"/Home/{gone}", allow_redirects=False)
    check(f"/Home/{gone} is 404 for a signed-in user", r.status_code == 404, r.status_code)
r = s.get(BASE + f"/Identity/Account/RegisterConfirmation?email={ADMIN}", allow_redirects=False)
check("administrator still sees RegisterConfirmation", r.status_code == 200 and "ConfirmEmail" in r.text, r.status_code)

# ── Forwarded headers ───────────────────────────────────────────────────────
r = s.get(BASE + f"/Identity/Account/RegisterConfirmation?email={ADMIN}", allow_redirects=False)
link = re.search(r'href="([^"]*ConfirmEmail[^"]*)"', r.text)
check("direct request keeps http", link and link.group(1).startswith("http://"), link and link.group(1)[:40])
# last: the cookie is re-issued Secure over "https", after which plain-http requests drop it
r = login().get(BASE + f"/Identity/Account/RegisterConfirmation?email={ADMIN}",
          headers={"X-Forwarded-Proto": "https"}, allow_redirects=False)
link = re.search(r'href="([^"]*ConfirmEmail[^"]*)"', r.text)
check("behind a proxy the confirmation link uses https", link and link.group(1).startswith("https://"),
      link and link.group(1)[:40])

# ── Image watermark (regression check after the package changes) ─────────────
exec_sql("""insert into Deviation (DeviationId, Identification, IdentifiedBy, IdentifiedOn, Status, Title, IsDeleted, RetentionExtended)
            values (?, 'e2e', 'e2e', ?, 0, 'E2E image watermark', 0, 0)""", (f"DEV-W{run}", NOW))
dev = q1("select Id from Deviation where DeviationId=?", (f"DEV-W{run}",))["Id"]

buf = io.BytesIO()
Image.new("RGB", (900, 600), (255, 255, 255)).save(buf, "PNG")
png = buf.getvalue()


def plant_png(name, deleted):
    rel = f"/uploads/e2e/{run}/{name}"
    full = os.path.join(APP_ROOT, "wwwroot", rel.lstrip("/"))
    os.makedirs(os.path.dirname(full), exist_ok=True)
    with open(full, "wb") as fh:
        fh.write(png)
    exec_sql("""insert into DeviationAttachment (DeviationId, FileName, FilePath, ContentType, FileSize, UploadedBy,
                UploadedDate, FileHash, IsDeleted, DeletedBy, DeletedOn, DeletionReason) values (?,?,?,?,?,?,?,?,?,?,?,?)""",
             (dev, name, rel, "image/png", len(png), ADMIN, NOW, hashlib.sha256(png).hexdigest(), 1 if deleted else 0,
              ADMIN if deleted else None, NOW if deleted else None, "e2e" if deleted else None))
    return q1("select Id from DeviationAttachment where FileName=?", (name,))["Id"]


def non_white(img, box):
    return sum(1 for p in img.crop(box).getdata() if p[:3] != (255, 255, 255))


live = plant_png(f"live-{run}.png", False)
r = s.get(BASE + f"/Deviations/DownloadAttachment/{live}", allow_redirects=False)
ok = r.status_code == 200 and r.content[:8] == b"\x89PNG\r\n\x1a\n"
check("image attachment downloads as PNG", ok, (r.status_code, r.headers.get("Content-Type")))
if ok:
    img = Image.open(io.BytesIO(r.content)).convert("RGBA")
    if OUT: img.save(os.path.join(OUT, f"watermark_live_{run}.png"))
    check("image size unchanged", img.size == (900, 600), img.size)
    check("footer text drawn at the bottom", non_white(img, (0, 560, 900, 600)) > 200)
    check("centre left clean (no diagonal on a live attachment)", non_white(img, (300, 250, 600, 350)) == 0)

gone = plant_png(f"deleted-{run}.png", True)
r = s.get(BASE + f"/Deviations/DownloadAttachment/{gone}?deleted=true", allow_redirects=False)
ok = r.status_code == 200 and r.content[:8] == b"\x89PNG\r\n\x1a\n"
check("deleted image attachment downloads as PNG", ok, r.status_code)
if ok:
    img = Image.open(io.BytesIO(r.content)).convert("RGBA")
    if OUT: img.save(os.path.join(OUT, f"watermark_deleted_{run}.png"))
    check("diagonal mark drawn across the centre", non_white(img, (300, 250, 600, 350)) > 200)

# A TIFF renamed to .png must not reach the TIFF decoder: it is served unchanged (not watermarked), promptly
tbuf = io.BytesIO()
Image.new("RGB", (200, 100), (255, 255, 255)).save(tbuf, "TIFF")
tiff = tbuf.getvalue()
png, saved = tiff, png
disguised = plant_png(f"disguised-{run}.png", False)
png = saved
r = s.get(BASE + f"/Deviations/DownloadAttachment/{disguised}", allow_redirects=False, timeout=30)
check("TIFF disguised as .png served unchanged (TIFF decoder not used)", r.status_code == 200 and r.content == tiff,
      (r.status_code, len(r.content)))

summary()
