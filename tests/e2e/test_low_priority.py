"""Low-priority review fixes.

- /AppUsers/Create redirects Administrators to the Identity Register page (was a 500: no view).
- Complaints Accept refuses QualityUser (Admin/QM/AP only, as the view and Deviations/Changes).
- Supplier CreateReview sets LastReviewDate to the review start, not the (future) NextReviewDate.
- Shared upload rules: PSUR accepts .odp; Training attachments reject disallowed types.
"""
import datetime
from e2e_lib import *

run = uuid.uuid4().hex[:6]
now = "2026-09-01 00:00:00"
s = login()

# AppUsers/Create
r = get(s, "/AppUsers/Create", allow_redirects=False)
check("AppUsers/Create: admin redirected to Register", r.status_code == 302 and "/Identity/Account/Register" in r.headers.get("Location", ""),
      (r.status_code, r.headers.get("Location")))
check("AppUsers/Create: Register page renders", get(s, "/AppUsers/Create").status_code == 200)
ensure_user("e2e.qm-read@trial.local", ["QualityManager"])
qm = login("e2e.qm-read@trial.local")
r = get(qm, "/AppUsers/Create", allow_redirects=False)
check("AppUsers/Create: QualityManager not sent to Register", "/Identity/Account/Register" not in r.headers.get("Location", "") and r.status_code != 500,
      (r.status_code, r.headers.get("Location")))

# Complaints Accept
exec_sql("""insert into Complaint (ComplaintId, Description, IsDeleted, ReceivedBy, ReceivedOn, RetentionExtended, Severity, Status, Title)
            values (?, 'e2e', 0, 'e2e', ?, 0, 0, 2, 'E2E complaint')""", (f"CMP-E{run}", now))
cid = q1("select Id from Complaint where ComplaintId=?", (f"CMP-E{run}",))["Id"]
ensure_user("e2e.qualityuser@trial.local", ["QualityUser"])
qu = login("e2e.qualityuser@trial.local")
r = post_form(qu, f"/Complaints/Accept/{cid}", f"/Complaints/Details/{cid}",
              {"id": cid, "email": "e2e.qualityuser@trial.local", "pwd": PW})
st = q1("select Status from Complaint where Id=?", (cid,))["Status"]
check("Complaints Accept: QualityUser refused", st == 2 and (r.status_code == 403 or "AccessDenied" in r.headers.get("Location", "")),
      (r.status_code, r.headers.get("Location"), st))
qm = login("e2e.qm-read@trial.local")
r = post_form(qm, f"/Complaints/Accept/{cid}", f"/Complaints/Details/{cid}",
              {"id": cid, "email": "e2e.qm-read@trial.local", "pwd": PW})
check("Complaints Accept: QualityManager accepted", q1("select Status from Complaint where Id=?", (cid,))["Status"] == 5, r.status_code)
s = login()

# Supplier CreateReview LastReviewDate
future = (datetime.date.today() + datetime.timedelta(days=200)).isoformat() + " 00:00:00"
exec_sql("""insert into Supplier (CreatedOn, IsDeleted, LegalName, QualificationRequired, ReviewFrequency, ReviewTrigger, RiskLevel,
            Status, SupplierCode, SupplierType, TestingRequirement, ApprovalStatus, NextReviewDate)
            values (?, 0, ?, 0, 0, 0, 0, 4, ?, 0, 0, 4, ?)""", (now, f"E2E Review {run}", f"SUP-R{run}", future))
sid = q1("select Id from Supplier where SupplierCode=?", (f"SUP-R{run}",))["Id"]
page = get(s, f"/Suppliers/CreateReview/{sid}")
f = form_fields(page.text, "CreateReview")
check("CreateReview: form loads", f is not None, page.status_code)
if f:
    f["supplierId"] = str(sid)
    f.setdefault("ReviewSummary", "e2e")
    r = s.post(f"{BASE}/Suppliers/CreateReview", data=f, allow_redirects=False)
    row = q1("select Status, LastReviewDate, NextReviewDate from Supplier where Id=?", (sid,))
    last = (row["LastReviewDate"] or "")[:10]
    check("CreateReview: review started", row["Status"] == 0, (r.status_code, row["Status"]))
    check("CreateReview: LastReviewDate = today (UTC), not the future due date",
          last == datetime.datetime.now(datetime.timezone.utc).date().isoformat(), (last, future[:10]))

# Uploads: PSUR .odp, Training type check
exec_sql("insert into PVPeriodicReport (Title, Status, CreatedBy, CreatedOn) values (?, 0, 'e2e', ?)", (f"PSUR-odp {run}", now))
psid = q1("select Id from PVPeriodicReport where Title=?", (f"PSUR-odp {run}",))["Id"]
r = post_form(s, "/PVPeriodicReports/UploadAttachment", f"/PVPeriodicReports/Details/{psid}",
              {"psurId": psid, "description": "e2e"}, files={"file": (f"deck-{run}.odp", b"PK\x03\x04odp", "application/octet-stream")})
check("PSUR upload: .odp accepted", q1("select 1 from PSURAttachments where FileName like ?", (f"%deck-{run}.odp",)) is not None
      , r.status_code)
r = post_form(s, "/PVPeriodicReports/UploadAttachment", f"/PVPeriodicReports/Details/{psid}",
              {"psurId": psid}, files={"file": (f"bad-{run}.exe", b"MZ", "application/octet-stream")})
check("PSUR upload: .exe rejected", q1("select 1 from PSURAttachments where FileName like ?", (f"%bad-{run}.exe",)) is None)

admin_id = q1("select Id from AspNetUsers where NormalizedEmail=?", (ADMIN.upper(),))["Id"]
exec_sql("""insert into Training (CreatedOn, Description, IsDeleted, Name, PolicyId, PolicyTitle, RetentionExtended, Status, TrainingId, TrainingType)
            values (?, 'e2e', 0, ?, 0, 'e2e', 0, 0, ?, 0)""", (now, f"E2E Training {run}", f"TRN-E{run}"))
tid = q1("select Id from Training where TrainingId=?", (f"TRN-E{run}",))["Id"]
exec_sql("insert into UserTraining (Status, TraineeId, TrainingId) values (0, ?, ?)", (admin_id, tid))
utid = q1("select Id from UserTraining where TrainingId=?", (tid,))["Id"]
for name, content, expect in [(f"cert-{run}.pdf", b"%PDF-1.4", True), (f"page-{run}.html", b"<script>alert(1)</script>", False)]:
    r = post_form(s, "/Trainings/UploadAttachment", f"/Trainings/Details/{tid}", {"userTrainingId": utid},
                  files={"file": (name, content, "text/html" if name.endswith("html") else "application/pdf")})
    got = q1("select 1 from TrainingAttachment where FileName=?", (name,)) is not None
    check(f"Training upload: {name.split('-')[0]}.{name.rsplit('.', 1)[1]} {'accepted' if expect else 'rejected'}", got == expect, r.status_code)

# Upload forms advertise the shared list
html = get(s, f"/Trainings/Details/{tid}").text
check("Training form: accept attribute uses shared list", 'accept=".pdf,.doc,.docx,.xls,.xlsx,.odt,.ods,.odp,.ppt,.pptx,.csv,.zip,.jpg,.jpeg,.png"' in html)

sys.exit(summary())
