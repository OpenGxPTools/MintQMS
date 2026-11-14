"""Generated records (exports and download stamps) follow the SITE language; nothing stored changes language.

  * site English → exports exactly as always (CSV header line unchanged);
  * site Spanish, user's own choice Portuguese → exports in Spanish (CSV header, Excel sheet + headers,
    PDF title), the screens in Portuguese; the audit entry of the export stays English;
  * every Excel register opens in Spanish and Portuguese (translated sheet names stay within Excel's limits);
  * a downloaded attachment's stamp (watermark text) is in the site language.
Run with E2E_LANGS=1 and E2E_APP_ROOT (a PDF attachment is planted). Leaves the site language English.
"""
import hashlib, io, json, os, shutil, subprocess, sys, tempfile, zipfile
import xml.etree.ElementTree as ET
from e2e_lib import *

if os.environ.get("E2E_LANGS") != "1":
    print("languages not installed — skipped"); sys.exit(0)
APP_ROOT = os.environ.get("E2E_APP_ROOT") or sys.exit("E2E_APP_ROOT is required")
TR = json.load(open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "Resources", "Localization", "translations.json"), encoding="utf-8"))
es = lambda k: TR[k]["es"]
run = uuid.uuid4().hex[:6]
NS = {"m": "http://schemas.openxmlformats.org/spreadsheetml/2006/main"}


def sheets(xlsx):
    wb = ET.fromstring(zipfile.ZipFile(io.BytesIO(xlsx)).read("xl/workbook.xml"))
    return [s.get("name") for s in wb.find("m:sheets", NS)]


def pdf_text(b):
    if not shutil.which("pdftotext"):
        return None
    with tempfile.NamedTemporaryFile(suffix=".pdf") as f:
        f.write(b); f.flush()
        return subprocess.run(["pdftotext", f.name, "-"], capture_output=True, text=True).stdout


s = login()
get(s, "/Settings")
exec_sql("""insert into Complaint (ComplaintId, Title, Description, Status, Severity, ReceivedOn, ReceivedBy, RetentionExtended, IsDeleted)
            values (?, 'Lang test', 'd', 0, 0, '2026-09-01 00:00:00', 'e2e', 0, 0)""", (f"CMP-RL-{run}",))
cid = q1("select Id from Complaint where ComplaintId=?", (f"CMP-RL-{run}",))["Id"]

# ── English: unchanged ───────────────────────────────────────────────────────────────────────────────
exec_sql("update SystemSettings set SiteLanguage='en', AllowUserLanguageChoice=0")
csv = get(s, f"/Complaints/ExportDetailInCSV/{cid}").content.decode("utf-8-sig").split("\n")[0].strip()
check("English CSV header exactly as before", csv.startswith("Complaint ID,Title,Status,Severity,Customer,Received On,"), csv[:60])

# ── Spanish site, viewer chose Portuguese ────────────────────────────────────────────────────────────
exec_sql("update SystemSettings set SiteLanguage='es', AllowUserLanguageChoice=1")
pg = s.get(BASE + "/Identity/Account/Manage")
s.post(BASE + "/Language/Set", data={"culture": "pt-BR", "returnUrl": "/", "__RequestVerificationToken": token(pg.text)})
check("screens follow the viewer's choice (Portuguese)", TR["My Training"]["pt-BR"] in get(s, "/Trainings/MyTraining").text)
r = get(s, f"/Complaints/ExportDetailInCSV/{cid}")
check("CSV starts with the UTF-8 mark (accents read correctly in Excel)", r.content[:3] == b"\xef\xbb\xbf")
hdr = r.content.decode("utf-8-sig").split("\n")[0]
check("CSV export in the site language (Spanish)", hdr.startswith(es("Complaint ID") + ","), hdr[:60])
check("…status value translated too", es("Received") in r.content.decode("utf-8-sig"))
aud = q1("select Action from ComplaintAuditLog where ParentId=? order by Id desc", (cid,))
check("export audit entry stays English", aud and aud["Action"] == "Exported (CSV)", aud and aud["Action"])
r = get(s, "/Complaints/ExportRegister")
check("Excel register: Spanish sheet name", r.status_code == 200 and es("Complaints") in sheets(r.content), r.status_code)
t = pdf_text(get(s, f"/Complaints/ExportComplaintDetail/{cid}").content)
check("PDF detail report title in Spanish", t is None or es("Complaint Detail Report") in t)

for lang in ("es", "pt-BR"):
    exec_sql("update SystemSettings set SiteLanguage=?", (lang,))
    bad = []
    for u in ["/Complaints/ExportRegister", "/Deviations/ExportRegister", "/Capas/ExportRegister", "/Changes/ExportRegister",
              "/Audits/ExportRegister", "/Risks/ExportRegister", "/Maintenances/ExportRegister", "/Suppliers/ExportRegister",
              "/AppDocuments/ExportRegister", "/Release/ExportRegister", "/Trainings/ExportRegister",
              "/LicencesCertifications/ExportRegister", "/LicencesCertifications/ExpiryReport", "/Customers/ContractExpiryReport",
              "/RegulatoryIntelligence/ExportApplications", "/RegulatoryIntelligence/ExportAccessPathways", "/Disposal/ExportReport",
              "/Reports/ExportManagementReview?from=2020-01-01&to=2030-01-01",
              "/Reports/ExportTrainingCompliance?from=2020-01-01&to=2030-01-01"]:
        r = get(s, u, allow_redirects=False)
        if r.status_code != 200 or "spreadsheet" not in r.headers.get("Content-Type", ""):
            bad.append((u, r.status_code)); continue
        names = sheets(r.content)
        if any(len(n) > 31 for n in names): bad.append((u, names))
    check(f"every Excel register opens in {lang}", not bad, bad)

# ── download stamp (watermark) in the site language ──────────────────────────────────────────────────
exec_sql("update SystemSettings set SiteLanguage='en'")
pdf = get(s, f"/Complaints/ExportComplaintDetail/{cid}").content
folder = os.path.join(APP_ROOT, "wwwroot", "uploads", "e2e", run); os.makedirs(folder, exist_ok=True)
open(os.path.join(folder, "stamp.pdf"), "wb").write(pdf)
exec_sql("""insert into ComplaintAttachment (ComplaintId, FileName, FilePath, ContentType, FileSize, UploadedBy, UploadedDate, FileHash, IsDeleted)
            values (?, 'stamp.pdf', ?, 'application/pdf', ?, 'e2e', '2026-09-01 00:00:00', ?, 0)""",
         (cid, f"/uploads/e2e/{run}/stamp.pdf", len(pdf), hashlib.sha256(pdf).hexdigest()))
aid = q1("select max(Id) i from ComplaintAttachment where ComplaintId=?", (cid,))["i"]
exec_sql("update SystemSettings set SiteLanguage='es'")
t = pdf_text(get(s, f"/Complaints/DownloadAttachment/{aid}").content)
check("download stamp in Spanish", t is None or es("SUPPORTING GMP RECORD ATTACHMENT") in t, (t or "")[-200:])
exec_sql("update SystemSettings set SiteLanguage='en'")
t = pdf_text(get(s, f"/Complaints/DownloadAttachment/{aid}").content)
check("download stamp in English when the site is English", t is None or "SUPPORTING GMP RECORD ATTACHMENT" in t)

s.post(BASE + "/Language/Set", data={"culture": "en", "returnUrl": "/", "__RequestVerificationToken": token(pg.text)})
exec_sql("update SystemSettings set SiteLanguage='en', AllowUserLanguageChoice=0")
summary()
