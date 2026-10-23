"""Review items 6-9.

6. Stored-XSS: PVSignalReview assignee, PVPeriodicReports product name, Release/Edit lab names +
   Strength (inside <script>), Release certificate statements (script JSON must parse).
7. Suppliers/Details address Add/Delete buttons shown to OperationsUser/CommercialUser/AP
   (matches the controller role gate), not to a read-only user.
8. Supplier UploadDocument stores the SHA-256 FileHash and writes an audit entry.
9. Supplier Link/DeleteQualityRecord: locked server-side when Approved etc., parent taken from
   the link (not the posted supplierId), audit entries name the record code, error shown.
"""
import hashlib, re, subprocess, tempfile
from e2e_lib import *

run = uuid.uuid4().hex[:6]
now = "2026-09-01 00:00:00"
s = login()

# ─── 6. XSS ────────────────────────────────────────────────────────────────────
P1 = f'<b id="xss1{run}">a</b>'
exec_sql("""insert into PVSignalReview (MedDRAPT, DetectedOn, DetectedBy, TriggerCount, Status, CreatedOn, AssignedTo)
            values ('E2E PT', ?, 'e2e', 1, 0, ?, ?)""", (now, now, P1))
html = get(s, "/PVSignalReview").text
check("6 PVSignalReview: raw assignee markup not rendered", f'id="xss1{run}"' not in html)
check("6 PVSignalReview: assignee shown encoded", f"&lt;b id=&quot;xss1{run}&quot;&gt;" in html or f'&lt;b id="xss1{run}"&gt;' in html)
exec_sql("insert into PVSignalReview (MedDRAPT, DetectedOn, DetectedBy, TriggerCount, Status, CreatedOn) values ('E2E PT2', ?, 'e2e', 1, 0, ?)", (now, now))
check("6 PVSignalReview: Unassigned still shown", "Unassigned" in get(s, "/PVSignalReview").text)

P2 = f'<b id="xss2{run}">p</b>'
exec_sql("""insert into Product (ProductId, Name, Description, Version, EditedOn, IsLocked, Status, IsDeleted)
            values (?, ?, 'e2e', '0.01', ?, 0, 0, 0)""", (f"PRD-X{run}", P2, now))
pid = q1("select Id from Product where ProductId=?", (f"PRD-X{run}",))["Id"]
exec_sql("insert into PVPeriodicReport (Title, Status, CreatedBy, CreatedOn, ProductId) values (?, 0, 'e2e', ?, ?)", (f"PSUR {run}", now, pid))
exec_sql("insert into PVPeriodicReport (Title, Status, CreatedBy, CreatedOn) values (?, 0, 'e2e', ?)", (f"PSUR-all {run}", now))
html = get(s, "/PVPeriodicReports").text
check("6 PVPeriodicReports: raw product markup not rendered", f'id="xss2{run}"' not in html)
check("6 PVPeriodicReports: product shown encoded", f"xss2{run}" in html)
check("6 PVPeriodicReports: 'All' still shown for no product", re.search(r'<span class="text-muted">All</span>', html) is not None)

LAB = f"</script><b id=\"xss3{run}\">L'ab\\ \"q\""
STR = f"</script><b id=\"xss4{run}\">5'mg"
exec_sql("""insert into ReleaseRecords (BatchNumber, CreatedDate, IsLocked, ManufacturingSite, ProductName, RecordNumber, ReleaseType, Status, Strength)
            values (?, ?, 0, 'Site', 'E2E Prod', ?, 1, 1, ?)""", (f"B{run}", now, f"REL-{run}", STR))
rid = q1("select Id from ReleaseRecords where RecordNumber=?", (f"REL-{run}",))["Id"]
exec_sql("insert into ReleaseTestingLabs (ReleaseRecordId, LaboratoryName, GmpLicenceRef) values (?, ?, ?)", (rid, LAB, "MI-'1"))
r = get(s, f"/Release/Edit/{rid}")
html = r.text
check("6 Release/Edit: page 200", r.status_code == 200, r.status_code)
check("6 Release/Edit: no raw injected markup", f'<b id="xss3{run}"' not in html and f'<b id="xss4{run}"' not in html)


def scripts_parse(page):
    """Syntax-check every inline <script> with node; return list of failures."""
    bad = []
    for i, sc in enumerate(re.findall(r"<script(?![^>]*\bsrc=)[^>]*>(.*?)</script>", page, re.S)):
        with tempfile.NamedTemporaryFile("w", suffix=".js", delete=False) as fh:
            fh.write(sc)
        p = subprocess.run(["node", "--check", fh.name], capture_output=True, text=True)
        if p.returncode:
            bad.append((i, p.stderr.strip().splitlines()[-1] if p.stderr else "?"))
    return bad


bad = scripts_parse(html)
check("6 Release/Edit: all inline scripts parse", not bad, bad)
check("6 Release/Edit: lab row call carries JSON-encoded name",
      "addLabRow(\"\\u003C/script\\u003E" in html, re.findall(r"addLabRow\([^\n]{0,120}", html)[:2])
check("6 Release/Edit: Strength JSON-encoded", "var currentStrength = \"\\u003C/script\\u003E" in html)

exec_sql("update ReleaseRecords set Status=5 where Id=?", (rid,))
r = get(s, f"/Release/Details/{rid}")
check("6 Release/Details (certified): page 200", r.status_code == 200, r.status_code)
bad = scripts_parse(r.text)
check("6 Release/Details: certificate-statement script parses", not bad, bad)
check("6 Release/Details: statement present in script", "I hereby certify that this batch has been reviewed" in r.text)

# ─── 7. Address button gate ────────────────────────────────────────────────────
exec_sql("""insert into Supplier (CreatedOn, IsDeleted, LegalName, QualificationRequired, ReviewFrequency, ReviewTrigger,
            RiskLevel, Status, SupplierCode, SupplierType, TestingRequirement) values (?, 0, ?, 0, 0, 0, 0, 0, ?, 0, 0)""",
         (now, f"E2E Supplier {run}", f"SUP-E{run}"))
sid = q1("select Id from Supplier where SupplierCode=?", (f"SUP-E{run}",))["Id"]
exec_sql("insert into Supplier (CreatedOn, IsDeleted, LegalName, QualificationRequired, ReviewFrequency, ReviewTrigger, RiskLevel, Status, SupplierCode, SupplierType, TestingRequirement) values (?, 0, ?, 0, 0, 0, 0, 0, ?, 0, 0)",
         (now, f"E2E Other {run}", f"SUP-O{run}"))
other = q1("select Id from Supplier where SupplierCode=?", (f"SUP-O{run}",))["Id"]

for role, expect in [("OperationsUser", True), ("CommercialUser", True), ("AuthorisedPerson", True),
                     ("QualityUser", True), ("ReadOnly", False)]:
    if not q1("select 1 from AspNetRoles where Name=?", (role,)):
        print(f"(role {role} not present — skipped)")
        continue
    email = f"e2e.{role.lower()}@trial.local"
    ensure_user(email, [role])
    us = login(email)
    page = get(us, f"/Suppliers/Details/{sid}")
    has_add = 'action="/Suppliers/AddAddress"' in page.text
    check(f"7 {role}: Add-address form {'shown' if expect else 'hidden'}", has_add == expect, page.status_code)
    if expect and role == "OperationsUser":
        rr = post_form(us, "/Suppliers/AddAddress", f"/Suppliers/Details/{sid}",
                       {"supplierId": sid, "street": "1 E2E St", "city": "Melbourne", "country": "Australia"})
        check("7 OperationsUser: AddAddress accepted", rr.status_code == 302 and
              q1("select 1 from SupplierAddress where SupplierId=? and Street='1 E2E St'", (sid,)) is not None, rr.status_code)
        page = get(us, f"/Suppliers/Details/{sid}")
        check("7 OperationsUser: Delete-address button shown", 'action="/Suppliers/DeleteAddress"' in page.text)
s = login()

# ─── 8. Document upload hash + audit ───────────────────────────────────────────
content = f"%PDF-1.4 e2e {run}\n%%EOF\n".encode()
r = post_form(s, "/Suppliers/UploadDocument", f"/Suppliers/Documents/{sid}",
              {"supplierId": sid, "documentType": "0", "description": "e2e"},
              files={"file": (f"e2e-{run}.pdf", content, "application/pdf")})
doc = q1("select * from SupplierDocument where SupplierId=? order by Id desc", (sid,))
check("8 upload: stored", doc is not None and doc["FileName"].endswith(f"e2e-{run}.pdf") if doc else False, r.status_code)
if doc:
    check("8 upload: FileHash saved (SHA-256 of content)", (doc["FileHash"] or "").lower() == hashlib.sha256(content).hexdigest(),
          (doc["FileHash"], hashlib.sha256(content).hexdigest()))
check("8 upload: audit 'Document uploaded'",
      q1("select 1 from SupplierAuditLog where ParentId=? and Action='Document uploaded' and Notes like ?", (sid, f"%e2e-{run}.pdf%")) is not None)

# ─── 9. Quality record link/unlink ─────────────────────────────────────────────
exec_sql("""insert into Deviation (DeviationId, Identification, IdentifiedBy, IdentifiedOn, Status, Title, IsDeleted, RetentionExtended)
            values (?, 'e2e', 'e2e', ?, 0, 'E2E dev', 0, 0)""", (f"DEV-E{run}", now))
dev = q1("select Id from Deviation where DeviationId=?", (f"DEV-E{run}",))["Id"]
qr_page = f"/Suppliers/QualityRecords/{sid}"
r = post_form(s, "/Suppliers/LinkQualityRecord", qr_page, {"supplierId": sid, "recordType": "0", "deviationId": dev})
link = q1("select * from SupplierQualityRecord where SupplierId=? and DeviationId=?", (sid, dev))
check("9 link (Pending): created", link is not None, r.status_code)
check("9 link: audit names record", q1("select 1 from SupplierAuditLog where ParentId=? and Action='Quality record linked' and Notes=?",
                                        (sid, f"Deviation DEV-E{run}")) is not None)

# lock: Approved
exec_sql("update Supplier set Status=4 where Id=?", (sid,))
r = post_form(s, "/Suppliers/LinkQualityRecord", qr_page, {"supplierId": sid, "recordType": "0", "deviationId": dev})
n = q1("select count(*) c from SupplierQualityRecord where SupplierId=?", (sid,))["c"]
check("9 link while Approved: refused", n == 1, n)
msg = get(s, r.headers.get("Location", qr_page)).text
check("9 link while Approved: error shown on QualityRecords", "Quality records are locked while the supplier is Approved" in msg)

# delete while locked, even posting another (unlocked) supplier's id
r = post_form(s, "/Suppliers/DeleteQualityRecord", qr_page, {"id": link["Id"], "supplierId": other})
check("9 unlink while Approved (spoofed parent): refused",
      q1("select 1 from SupplierQualityRecord where Id=?", (link["Id"],)) is not None, r.status_code)
check("9 unlink: redirect goes to the real parent", f"/Suppliers/QualityRecords/{sid}" in r.headers.get("Location", ""), r.headers.get("Location"))

exec_sql("update Supplier set Status=0 where Id=?", (sid,))
r = post_form(s, "/Suppliers/DeleteQualityRecord", qr_page, {"id": link["Id"], "supplierId": other})
check("9 unlink (Pending): removed", q1("select 1 from SupplierQualityRecord where Id=?", (link["Id"],)) is None)
check("9 unlink: audit on real parent, not posted id",
      q1("select 1 from SupplierAuditLog where ParentId=? and Action='Quality record unlinked' and Notes=?", (sid, f"Deviation DEV-E{run}")) is not None
      and q1("select 1 from SupplierAuditLog where ParentId=? and Action='Quality record unlinked'", (other,)) is None)
check("9 QualityRecords page 200", get(s, qr_page).status_code == 200)

sys.exit(summary())
