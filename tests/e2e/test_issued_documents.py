"""Issued documents — release certificates, training certificates, attendance lists.

Each is written in the SITE language at the moment it is issued, stored as a complete copy with a SHA-256
checksum, and from then on shown exactly as stored. This suite challenges that:
  * a later change of site language, organisation name, logo (even deleting the file) or user name
    must not change an issued document; the viewer's own language choice must not either;
  * a document issued while the site is in Spanish is in Spanish; the audit trail stays English;
  * a tampered copy is refused (not shown) and the refusal is audited;
  * viewing twice never makes a second copy; withdrawn / superseded status is shown outside the copy;
  * a certificate issued before copies were kept is fixed in English ("frozen on upgrade");
  * training: certificate only once that trainee's training is complete; attendance list live (NOT FINAL)
    until the training is complete, then fixed; reopening supersedes it, completing again issues a new one;
  * a failed competency attempt gets no certificate; text typed into records cannot inject markup.
Needs E2E_APP_ROOT (a logo is planted under its wwwroot). Leaves the site language English.
"""
import base64, hashlib, json, os, sys
from bs4 import BeautifulSoup
from e2e_lib import *

APP_ROOT = os.environ.get("E2E_APP_ROOT") or sys.exit("E2E_APP_ROOT is required")
LANGS = os.environ.get("E2E_LANGS") == "1"
run = uuid.uuid4().hex[:6]
NOW = "2026-10-01 00:00:00"
TR = json.load(open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "Resources", "Localization", "translations.json"), encoding="utf-8"))


def es(english):
    return TR.get(english, {}).get("es", english)


def copies(kind, source):
    return q("select * from IssuedRecordCopies where Kind=? and SourceId=? order by Id", (kind, source))


def active(kind, source):
    rows = [r for r in copies(kind, source) if r["SupersededOn"] is None]
    return rows[-1] if rows else None


def sha_ok(row):
    return hashlib.sha256(row["Html"].encode("utf-8")).hexdigest() == row["Sha256"].lower()


def doc_text(html, sel="#certificate-content"):
    el = BeautifulSoup(html, "html.parser").select_one(sel)
    return " ".join(el.get_text(" ").split()) if el else ""


def site(lang):
    exec_sql("update SystemSettings set SiteLanguage=?", (lang,))


# ── setup: settings row, organisation, logo ──────────────────────────────────────────────────────────
s = login()
get(s, "/Settings")                                  # make sure the settings row exists
logo_dir = os.path.join(APP_ROOT, "wwwroot", "uploads", "logo")
os.makedirs(logo_dir, exist_ok=True)
logo_file = os.path.join(logo_dir, f"e2e-issued-{run}.png")
png = base64.b64decode("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==")
open(logo_file, "wb").write(png)
exec_sql("update SystemSettings set OrganisationName=?, OrganisationAddress='1 Test St', LogoPath=?, SiteLanguage='en', AllowUserLanguageChoice=0",
         (f"Org One {run}", f"/uploads/logo/e2e-issued-{run}.png"))
since = q1("select IssuedCopiesSince from SystemSettings")["IssuedCopiesSince"]
check("start marker set (IssuedCopiesSince)", since is not None)

# ── release record (Certified) with markup in typed fields ───────────────────────────────────────────
rn = f"REL-ID-{run}"
exec_sql("""insert into ReleaseRecords (RecordNumber, BatchNumber, CreatedDate, IsLocked, ManufacturingSite, ProductName,
            ReleaseType, Status, ProductType, BrandName, ManufacturingSiteGmpRef, ReleasingCompany, PackSize, CertificationDate, CertifiedBy)
            values (?,?,?,0,'Site <img src=x onerror=alert(1)>',?,1,5,0,'Brand','GMP-1','Rel Co','10 x 10',?,'admin@trial.local')""",
         (rn, f"B-{run}", NOW, f"Prod <script>alert('{run}')</script>", NOW))
rid = q1("select Id from ReleaseRecords where RecordNumber=?", (rn,))["Id"]
exec_sql("insert into ReleaseTestResults (ReleaseRecordId, DisplayOrder, TestParameter, TestMethod, AcceptanceCriterion, Result, Status, IsLocked) values (?,1,'Assay','HPLC','95-105%','99%',1,0)", (rid,))


def issue(ctype, statement="Cert statement"):
    post_form(s, "/Release/IssueCertificate", f"/Release/Details/{rid}",
              {"releaseRecordId": rid, "type": ctype, "email": ADMIN, "pwd": PW, "statement": statement})
    return q1("select * from ReleaseCertificates where ReleaseRecordId=? order by Id desc", (rid,))


coa = issue(1)
c1 = active("ReleaseCertificate", coa["Id"])
check("CoA issued with a stored copy", c1 is not None)
check("copy: English, Issued, checksum valid", c1 and c1["Language"] == "en" and c1["Origin"] == "Issued" and sha_ok(c1),
      c1 and (c1["Language"], c1["Origin"]))
check("copy embeds the logo and the organisation name", c1 and "data:image/png;base64," in c1["Html"] and f"Org One {run}" in c1["Html"])
check("typed markup is escaped in the copy", c1 and "<script>alert" not in c1["Html"] and "<img src=x" not in c1["Html"]
      and "&lt;script&gt;" in c1["Html"])
r = get(s, f"/Release/ViewCertificate?certificateId={coa['Id']}")
t1 = doc_text(r.text)
check("certificate page shows the stored document", r.status_code == 200 and "Certificate of Analysis" in t1 and f"Org One {run}" in t1, r.status_code)
check("page notes the stored copy and its checksum", c1 and c1["Sha256"][:16] in r.text)

# ── settings / user changes after issue do not change it ─────────────────────────────────────────────
exec_sql("update SystemSettings set OrganisationName=?, LogoPath='/uploads/logo/none.png'", (f"Org Two {run}",))
os.remove(logo_file)
r = get(s, f"/Release/ViewCertificate?certificateId={coa['Id']}")
t2 = doc_text(r.text)
check("organisation rename + logo removal: document unchanged", t2 == t1 and f"Org Two {run}" not in t2 and "data:image/png;base64," in r.text)
check("…and the stored copy is untouched", active("ReleaseCertificate", coa["Id"])["Sha256"] == c1["Sha256"])
get(s, f"/Release/ViewCertificate?certificateId={coa['Id']}")
check("viewing again never makes a second copy", len(copies("ReleaseCertificate", coa["Id"])) == 1)

if LANGS:
    # ── site language Spanish: old document stays English, a new one is Spanish ─────────────────────
    site("es")
    r = get(s, f"/Release/ViewCertificate?certificateId={coa['Id']}")
    check("site language → Spanish: English certificate stays English", doc_text(r.text) == t1)
    check("…while the page around it is Spanish", es("Print / Save as PDF") in r.text and es("Print / Save as PDF") != "Print / Save as PDF")
    coc = issue(2, "Declaración de prueba")
    c2 = active("ReleaseCertificate", coc["Id"])
    check("CoC issued in Spanish: copy language es", c2 and c2["Language"] == "es", c2 and c2["Language"])
    check("…its text is Spanish", c2 and es("Certificate of Conformance") in c2["Html"] and "Certificate of Conformance" not in c2["Html"])
    aud = q1("select Action, Details from ReleaseAuditEntries where ReleaseRecordId=? and Details like ? order by Id desc", (rid, f"%{coc['CertificateNumber']}%"))
    check("audit trail stays English", aud and aud["Action"] == "Certificate Issued" and "issued by" in aud["Details"], aud and tuple(aud))

    # the viewer's own language choice does not change the document
    exec_sql("update SystemSettings set AllowUserLanguageChoice=1")
    pg = s.get(BASE + "/Identity/Account/Manage")
    s.post(BASE + "/Language/Set", data={"culture": "pt-BR", "returnUrl": "/", "__RequestVerificationToken": token(pg.text)})
    r = get(s, f"/Release/ViewCertificate?certificateId={coc['Id']}")
    check("viewer in Portuguese: Spanish certificate stays Spanish", doc_text(r.text) == doc_text(c2["Html"]) and 'lang="es"' in c2["Html"])
    s.post(BASE + "/Language/Set", data={"culture": "en", "returnUrl": "/", "__RequestVerificationToken": token(pg.text)})
    exec_sql("update SystemSettings set AllowUserLanguageChoice=0")

# ── tampering is detected, refused and audited ───────────────────────────────────────────────────────
exec_sql("update IssuedRecordCopies set Html=replace(Html, ?, ?) where Id=?", (f"Org One {run}", f"Org Evil {run}", c1["Id"]))
r = get(s, f"/Release/ViewCertificate?certificateId={coa['Id']}")
check("tampered copy is refused", 'id="integrity-failed"' in r.text and f"Org Evil {run}" not in r.text)
check("…and the refusal is audited", q1("select 1 from ReleaseAuditEntries where ReleaseRecordId=? and Action='Certificate Integrity Check Failed'", (rid,)))
exec_sql("update IssuedRecordCopies set Html=? where Id=?", (c1["Html"], c1["Id"]))
check("restored copy passes again", 'id="integrity-failed"' not in get(s, f"/Release/ViewCertificate?certificateId={coa['Id']}").text)

# ── status (superseded / withdrawn) is shown outside the copy ────────────────────────────────────────
coa2 = issue(1, "Re-issue")
r = get(s, f"/Release/ViewCertificate?certificateId={coa['Id']}")
check("re-issue: first CoA shown as SUPERSEDED", 'id="certificate-status"' in r.text and doc_text(r.text) == t1)
exec_sql("update ReleaseRecords set Status=6 where Id=?", (rid,))          # Released
post_form(s, f"/Release/AdminReopen/{rid}", f"/Release/Details/{rid}",
          {"id": rid, "reason": "E2E reopen", "deviationRef": "DEV-1", "email": ADMIN, "pwd": PW})
st = q1("select Status from ReleaseCertificates where Id=?", (coa2["Id"],))["Status"]
r = get(s, f"/Release/ViewCertificate?certificateId={coa2['Id']}")
check("admin reopen withdraws the certificate; page says so", st == 4 and 'id="certificate-status"' in r.text, st)
check("…the withdrawn certificate's stored copy is unchanged", sha_ok(active("ReleaseCertificate", coa2["Id"])))

# ── a certificate issued before copies were kept is fixed in English ─────────────────────────────────
if LANGS:
    site("es")
snap = q1("select ContentSnapshot from ReleaseCertificates where Id=?", (coa["Id"],))["ContentSnapshot"]
exec_sql("""insert into ReleaseCertificates (ReleaseRecordId, CertificateNumber, CertificateType, Status, ContentSnapshot, Version, IssuedDate, IssuedBy)
            values (?, ?, 1, 2, ?, 1, '2020-01-01 00:00:00', 'legacy')""", (rid, f"COA-OLD-{run}", snap))
old_id = q1("select Id from ReleaseCertificates where CertificateNumber=?", (f"COA-OLD-{run}",))["Id"]
r = get(s, f"/Release/ViewCertificate?certificateId={old_id}")
co = active("ReleaseCertificate", old_id)
check("legacy certificate fixed on first view: English, frozen on upgrade",
      co and co["Language"] == "en" and co["Origin"] == "FrozenOnUpgrade" and "Certificate of Analysis" in doc_text(r.text),
      co and (co["Language"], co["Origin"]))

# ── training: Read & Acknowledge ─────────────────────────────────────────────────────────────────────
u1 = ensure_user(f"it1-{run}@e2e.local", ["QualityUser"], "Ivy", "One")
u2 = ensure_user(f"it2-{run}@e2e.local", ["QualityUser"], "Ian", "Two")
u3 = ensure_user(f"it3-{run}@e2e.local", ["QualityUser"], "Not", "Enrolled")


def mk_training(code, ttype, users):
    exec_sql("""insert into Training (TrainingId, Name, Description, CreatedOn, IsDeleted, PolicyId, PolicyTitle, RetentionExtended,
                Status, TrainingType, TrainerEmail) values (?,?,'<b>desc</b>',?,0,0,'SOP-X',0,0,?,'admin@trial.local')""",
             (code, f"Training {code}", NOW, ttype))
    tid = q1("select Id from Training where TrainingId=?", (code,))["Id"]
    for u in users:
        exec_sql("insert into UserTraining (TraineeId, TrainingId, Status) values (?,?,0)", (u, tid))
    return tid


def ut_id(tid, uid):
    return q1("select max(Id) i from UserTraining where TrainingId=? and TraineeId=?", (tid, uid))["i"]


def ack(email, tid, uid):
    su = login(email)
    post_form(su, "/Trainings/AcknowledgeReading", "/Trainings/MyTraining",
              {"userTrainingId": ut_id(tid, uid), "InputEmail": email, "InputPassword": PW})
    return su


lang_now = "es" if LANGS else "en"
ra = mk_training(f"TRN-ID-{run}", 1, [u1, u2])
s1 = ack(f"it1-{run}@e2e.local", ra, u1)
tc = active("TrainingCertificate", ut_id(ra, u1))
check("trainee 1 acknowledged: certificate issued in the site language", tc and tc["Language"] == lang_now and sha_ok(tc), tc and tc["Language"])
r = get(s1, f"/Trainings/ExportTrainingCertificate/{ra}")
tct = doc_text(r.text)
check("trainee 1 sees the stored certificate", "Ivy One" in tct)
s2 = login(f"it2-{run}@e2e.local")
r = get(s2, f"/Trainings/ExportTrainingCertificate/{ra}")
check("trainee 2 (not acknowledged): no certificate yet", 'id="certificate-not-available"' in r.text and not copies("TrainingCertificate", ut_id(ra, u2)))
s3 = login(f"it3-{run}@e2e.local")
r = get(s3, f"/Trainings/ExportTrainingCertificate/{ra}", allow_redirects=False)
check("a user not enrolled gets no certificate", r.status_code in (302, 403) and "Ivy One" not in r.text, r.status_code)
r = get(s, f"/Trainings/ExportTrainingAttendance/{ra}")
check("open training: attendance list is live and marked NOT FINAL", 'id="attendance-live"' in r.text and not copies("AttendanceList", ra))
exec_sql("update AspNetUsers set FirstName='Renamed' where Id=?", (u1,))
check("renaming the trainee does not change the issued certificate", doc_text(get(s1, f"/Trainings/ExportTrainingCertificate/{ra}").text) == tct)

ack(f"it2-{run}@e2e.local", ra, u2)
st = q1("select Status from Training where Id=?", (ra,))["Status"]
al = active("AttendanceList", ra)
check("both acknowledged → training completed → attendance list fixed", st == 1 and al and al["Language"] == lang_now and sha_ok(al), (st, al and al["Language"]))
r = get(s, f"/Trainings/ExportTrainingAttendance/{ra}")
check("attendance page shows the stored list", 'id="attendance-live"' not in r.text and al and al["Sha256"][:16] in r.text)

post_form(s, f"/Trainings/AdminReopen/{ra}", f"/Trainings/Details/{ra}", {"id": ra, "reason": "E2E reopen", "email": ADMIN, "pwd": PW})
st = q1("select Status from Training where Id=?", (ra,))["Status"]
check("admin reopen: attendance list superseded, page live again",
      st == 0 and active("AttendanceList", ra) is None and copies("AttendanceList", ra)[0]["SupersededOn"] is not None
      and 'id="attendance-live"' in get(s, f"/Trainings/ExportTrainingAttendance/{ra}").text, st)
check("…trainees' certificates stay in force (their training stays completed)", active("TrainingCertificate", ut_id(ra, u1)) is not None)
post_form(s, f"/Trainings/Complete/{ra}", f"/Trainings/Details/{ra}",
          {"id": ra, "InputEmail": ADMIN, "InputPassword": PW, "CompletionNotes": "E2E re-complete"})
al2 = active("AttendanceList", ra)
check("completed again: a new attendance list is issued, the old one kept",
      al2 is not None and al and al2["Id"] != al["Id"] and len(copies("AttendanceList", ra)) == 2)
check("training audit trail stays English", q1("select 1 from MasterDataAuditLog where EntityType='Training' and ParentId=? and Action='Administrative Reopen'", (ra,)))

# ── training: Competency Assessment, failed then passed ──────────────────────────────────────────────
ca = mk_training(f"TRN-CA-{run}", 2, [u2])
ack(f"it2-{run}@e2e.local", ca, u2)
first = ut_id(ca, u2)
post_form(s, "/Trainings/VerifyCompetency", f"/Trainings/Details/{ca}",
          {"userTrainingId": first, "passed": "false", "Notes": "not yet", "InputEmail": ADMIN, "InputPassword": PW})
check("failed competency attempt: no certificate", not copies("TrainingCertificate", first))
ack(f"it2-{run}@e2e.local", ca, u2)
second = ut_id(ca, u2)
post_form(s, "/Trainings/VerifyCompetency", f"/Trainings/Details/{ca}",
          {"userTrainingId": second, "passed": "true", "Notes": "competent", "InputEmail": ADMIN, "InputPassword": PW})
c3 = active("TrainingCertificate", second)
check("passed attempt: certificate issued for that attempt", second != first and c3 is not None and "competent" in (c3["Html"] if c3 else ""))
check("…and the training's attendance list is fixed", active("AttendanceList", ca) is not None)

site("en")
sys.exit(summary())
