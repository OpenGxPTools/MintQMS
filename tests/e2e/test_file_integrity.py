"""Attachment integrity check on download/view (SHA-256 recorded at upload).

For every attachment/document type (19 tables) a real file is planted with its correct hash:
  - intact file          -> served (200, file content)
  - legacy row, no hash  -> served (cannot be verified)
  - file altered on disk -> NOT served (redirect), "integrity check failed" in the parent audit trail
The eight inline ViewAttachment actions get the same tamper check. Master-data uploads (six
modules) must now record the SHA-256 at upload.

Needs E2E_APP_ROOT = the running app's content root (its wwwroot/uploads receives the test files).
"""
import hashlib, os, re, sqlite3
from e2e_lib import *

APP_ROOT = os.environ.get("E2E_APP_ROOT") or sys.exit("E2E_APP_ROOT is required")
run = uuid.uuid4().hex[:6]
NOW = "2026-09-01 00:00:00"

# table, parent table, FK column, path column, download URL template, view URL template (or None)
TYPES = [
    ("DeviationAttachment", "Deviation", "DeviationId", "FilePath", "/Deviations/DownloadAttachment/{id}", "/Deviations/ViewAttachment/{id}"),
    ("ComplaintAttachment", "Complaint", "ComplaintId", "FilePath", "/Complaints/DownloadAttachment/{id}", "/Complaints/ViewAttachment/{id}"),
    ("CapaAttachment", "Capa", "CapaId", "FilePath", "/Capas/DownloadAttachment/{id}", "/Capas/ViewAttachment/{id}"),
    ("ChangeAttachment", "Change", "ChangeId", "FilePath", "/Changes/DownloadAttachment/{id}", "/Changes/ViewAttachment/{id}"),
    ("AuditAttachment", "Audit", "AuditId", "FilePath", "/Audits/DownloadAttachment/{id}", "/Audits/ViewAttachment/{id}"),
    ("RiskAttachment", "Risk", "RiskId", "FilePath", "/Risks/DownloadAttachment/{id}", "/Risks/ViewAttachment/{id}"),
    ("MaintenanceAttachment", "Maintenance", "MaintenanceId", "FilePath", "/Maintenances/DownloadAttachment/{id}", "/Maintenances/ViewAttachment/{id}"),
    ("AEAttachment", "AdverseEvent", "AdverseEventId", "FilePath", "/Pharmacovigilance/DownloadAttachment/{id}", "/Pharmacovigilance/ViewAttachment/{id}"),
    ("PSURAttachments", "PVPeriodicReport", "PSURId", "FilePath", "/PVPeriodicReports/DownloadAttachment/{id}", None),
    ("SupplierDocument", "Supplier", "SupplierId", "FilePath", "/Suppliers/DownloadDocument/{id}", None),
    ("AssetDocument", "Asset", "AssetId", "FilePath", "/Assets/DownloadDocument/{id}", None),
    ("CustomerDocument", "Customer", "CustomerId", "FilePath", "/Customers/DownloadDocument/{id}", None),
    ("MaterialDocument", "Material", "MaterialId", "FilePath", "/Materials/DownloadDocument/{id}", None),
    ("ProcessDocument", "Process", "ProcessId", "FilePath", "/Processes/DownloadDocument/{id}", None),
    ("ProductDocument", "Product", "ProductId", "FilePath", "/Products/DownloadDocument/{id}", None),
    ("ServiceDocument", "Service", "ServiceId", "FilePath", "/Services/DownloadDocument/{id}", None),
    ("RIDocuments", "RegulatoryApplications", "RegulatoryApplicationId", "FilePath", "/RegulatoryIntelligence/DownloadDocument/{id}", None),
    ("LCDocuments", "LicenceCertificationRecords", "LicenceCertificationRecordId", "StoragePath", "/LicencesCertifications/DownloadDocument/{id}", None),
    ("ReleaseDocuments", "ReleaseRecords", "ReleaseRecordId", "FilePath", "/Release/DownloadDocument?documentId={id}", None),
]

DATE_COL = re.compile(r"(On|Date|At|Time|Since|Until|Due)$|Date")


def raw_insert(table, values):
    """Insert a row, filling any other NOT NULL column with a plausible value (FKs unchecked here)."""
    con = sqlite3.connect(DB)          # foreign_keys is OFF by default on a new connection
    cols = con.execute(f'pragma table_info("{table}")').fetchall()
    row = dict(values)
    for _, name, ctype, notnull, default, pk in cols:
        if pk or name in row or not notnull or default is not None:
            continue
        t = (ctype or "").upper()
        if "INT" in t:
            row[name] = 0
        elif "REAL" in t or "NUM" in t:
            row[name] = 0
        elif "BLOB" in t:
            row[name] = b""
        elif DATE_COL.search(name):
            row[name] = NOW
        elif name.endswith("Version"):
            row[name] = "1.0"
        else:
            row[name] = f"e2e-{run}-{name}"[:40]
    keys = ", ".join(f'"{k}"' for k in row)
    cur = con.execute(f'insert into "{table}" ({keys}) values ({", ".join("?" * len(row))})', list(row.values()))
    con.commit()
    rid = cur.lastrowid
    con.close()
    return rid


def plant(table, parent_col, parent_id, path_col, name, content, with_hash=True):
    rel = f"/uploads/e2e/{run}/{name}"
    full = os.path.join(APP_ROOT, "wwwroot", rel.lstrip("/"))
    os.makedirs(os.path.dirname(full), exist_ok=True)
    with open(full, "wb") as fh:
        fh.write(content)
    vals = {parent_col: parent_id, "FileName": name, path_col: rel, "IsDeleted": 0,
            "FileHash": hashlib.sha256(content).hexdigest() if with_hash else None}
    return raw_insert(table, vals), full


def integrity_logged(fname):
    con = sqlite3.connect(DB)
    for (t,) in con.execute("select name from sqlite_master where type='table' and name like '%Audit%'"):
        cols = [c[1] for c in con.execute(f'pragma table_info("{t}")')]
        if "Action" not in cols:
            continue
        texts = [c for c in cols if c in ("Notes", "OldValue", "NewValue", "Details", "Action")]
        cond = " or ".join(f'"{c}" like ?' for c in texts)
        if con.execute(f'select 1 from "{t}" where "Action" like \'%ntegrity%\' and ({cond})',
                       [f"%{fname}%"] * len(texts)).fetchone():
            con.close()
            return True
    con.close()
    return False


s = login()
content = f"a,b\n1,{run}\n".encode()
for table, ptable, pcol, pathcol, dl, view in TYPES:
    parent = raw_insert(ptable, {"IsDeleted": 0} if "IsDeleted" in [c[1] for c in sqlite3.connect(DB).execute(f'pragma table_info("{ptable}")')] else {})
    short = table.replace("Attachments", "").replace("Attachment", "").replace("Documents", "").replace("Document", "")

    ok_id, _ = plant(table, pcol, parent, pathcol, f"ok-{short}-{run}.csv", content)
    r = s.get(BASE + dl.format(id=ok_id), allow_redirects=False)
    check(f"{short}: intact file served", r.status_code == 200 and b"a,b" in r.content, (r.status_code, r.headers.get("Location")))

    legacy_id, _ = plant(table, pcol, parent, pathcol, f"legacy-{short}-{run}.csv", content, with_hash=False)
    r = s.get(BASE + dl.format(id=legacy_id), allow_redirects=False)
    check(f"{short}: legacy (no hash) served", r.status_code == 200, r.status_code)

    bad_name = f"bad-{short}-{run}.csv"
    bad_id, full = plant(table, pcol, parent, pathcol, bad_name, content)
    with open(full, "ab") as fh:
        fh.write(b"tampered,1\n")
    r = s.get(BASE + dl.format(id=bad_id), allow_redirects=False)
    check(f"{short}: altered file NOT served", r.status_code == 302 and b"tampered" not in r.content,
          (r.status_code, r.headers.get("Location")))
    check(f"{short}: failure in parent audit trail", integrity_logged(bad_name))

    if view:
        vname = f"vbad-{short}-{run}.csv"
        vid, vfull = plant(table, pcol, parent, pathcol, vname, content)
        with open(vfull, "ab") as fh:
            fh.write(b"tampered,1\n")
        r = s.get(BASE + view.format(id=vid), allow_redirects=False)
        check(f"{short}: altered file NOT viewed inline", r.status_code == 302 and integrity_logged(vname), r.status_code)

# The warning reaches the user (checked on a properly created record)
exec_sql("""insert into Deviation (DeviationId, Identification, IdentifiedBy, IdentifiedOn, Status, Title, IsDeleted, RetentionExtended)
            values (?, 'e2e', 'e2e', ?, 0, 'E2E integrity', 0, 0)""", (f"DEV-I{run}", NOW))
dev = q1("select Id from Deviation where DeviationId=?", (f"DEV-I{run}",))["Id"]
wid, wfull = plant("DeviationAttachment", "DeviationId", dev, "FilePath", f"warn-{run}.csv", content)
with open(wfull, "ab") as fh:
    fh.write(b"x")
page = s.get(BASE + f"/Deviations/DownloadAttachment/{wid}", allow_redirects=True).text
check("warning shown on the record", "failed its integrity check" in page)

# Training attachments (held in the database)
admin_id = q1("select Id from AspNetUsers where NormalizedEmail=?", (ADMIN.upper(),))["Id"]
exec_sql("""insert into Training (CreatedOn, Description, IsDeleted, Name, PolicyId, PolicyTitle, RetentionExtended, Status, TrainingId, TrainingType)
            values (?, 'e2e', 0, ?, 0, 'e2e', 0, 0, ?, 0)""", (NOW, f"E2E Integrity {run}", f"TRN-I{run}"))
tid = q1("select Id from Training where TrainingId=?", (f"TRN-I{run}",))["Id"]
exec_sql("insert into UserTraining (Status, TraineeId, TrainingId) values (0, ?, ?)", (admin_id, tid))
utid = q1("select Id from UserTraining where TrainingId=?", (tid,))["Id"]
tbody = f"%PDF-1.4 training {run}\n".encode()
tname = f"trn-{run}.pdf"
r = post_form(s, "/Trainings/UploadAttachment", f"/Trainings/Details/{tid}", {"userTrainingId": utid},
              files={"file": (tname, tbody, "application/pdf")})
att = q1("select Id, FileHash from TrainingAttachment where FileName=?", (tname,))
check("Training: upload records SHA-256", att is not None and att["FileHash"] == hashlib.sha256(tbody).hexdigest(),
      (r.status_code, att and att["FileHash"]))
if att:
    r = s.get(BASE + f"/Trainings/DownloadAttachment/{att['Id']}", allow_redirects=False)
    check("Training: intact attachment served", r.status_code == 200, r.status_code)
    exec_sql("update TrainingAttachment set Content=? where Id=?", (tbody + b"tampered", att["Id"]))
    r = s.get(BASE + f"/Trainings/DownloadAttachment/{att['Id']}", allow_redirects=False)
    check("Training: altered attachment NOT served", r.status_code == 302 and b"tampered" not in r.content, r.status_code)
    check("Training: failure in training audit trail", integrity_logged(tname))
    page = s.get(BASE + r.headers.get("Location", "/"), allow_redirects=True).text
    check("Training: warning shown", "failed its integrity check" in page)
    exec_sql("update TrainingAttachment set FileHash=NULL where Id=?", (att["Id"],))
    r = s.get(BASE + f"/Trainings/DownloadAttachment/{att['Id']}", allow_redirects=False)
    check("Training: legacy (no hash) served", r.status_code == 200, r.status_code)

# Master-data uploads now record the SHA-256
for mod, table, pcol in [("Assets", "Asset", "assetId"), ("Customers", "Customer", "customerId"),
                         ("Materials", "Material", "materialId"), ("Processes", "Process", "processId"),
                         ("Products", "Product", "productId"), ("Services", "Service", "serviceId")]:
    rec = q1(f'select Id from "{table}" where IsDeleted=0 and IsLocked=0 and Status=0 order by Id desc')
    if not check(f"{mod}: draft record available", rec is not None):
        continue
    body = f"up,{mod},{run}\n".encode()
    fname = f"up-{mod}-{run}.csv"
    r = post_form(s, f"/{mod}/UploadDocument", f"/{mod}/Documents/{rec['Id']}",
                  {pcol: rec["Id"], "description": "e2e"}, files={"file": (fname, body, "text/csv")})
    row = q1(f'select FileHash from "{table}Document" where FileName=?', (fname,))
    check(f"{mod}: upload records SHA-256", row is not None and row["FileHash"] == hashlib.sha256(body).hexdigest(),
          (r.status_code, row and row["FileHash"]))

sys.exit(summary())
