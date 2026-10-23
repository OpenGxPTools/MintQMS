"""Warning-period settings, Authorised Person approval of reference data, maintenance work recording.

- LC / Maintenance / Training dashboards use the warning periods from System Settings
  (they used fixed 90 / 14 days / "next 10" before); the unused Date format setting is gone.
- An Authorised Person can approve Materials, Processes, Assets, Services and Customers (Products already could).
- The person doing a maintenance job can record work performed / parts / cost while it is In Progress.
"""
import re, sqlite3
from e2e_lib import *

run = uuid.uuid4().hex[:6]
NOW = "2026-09-01 00:00:00"
DATE_COL = re.compile(r"(On|Date|At|Time|Since|Until|Due)$|Date")


def raw_insert(table, values=None):
    con = sqlite3.connect(DB)
    row = dict(values or {})
    for _, name, ctype, notnull, default, pk in con.execute(f'pragma table_info("{table}")').fetchall():
        if pk or name in row or not notnull or default is not None:
            continue
        t = (ctype or "").upper()
        if name == "IsDeleted" or "INT" in t or "REAL" in t or "NUM" in t:
            row[name] = 0
        elif "BLOB" in t:
            row[name] = b""
        elif DATE_COL.search(name):
            row[name] = NOW
        elif name.endswith("Version"):
            row[name] = "1.0"
        else:
            row[name] = f"E2E-{run}-{name}"[:40]
    keys = ", ".join(f'"{k}"' for k in row)
    cur = con.execute(f'insert into "{table}" ({keys}) values ({", ".join("?" * len(row))})', list(row.values()))
    con.commit()
    rid = cur.lastrowid
    con.close()
    return rid


def day(n):
    return q1("select date('now', ?) as d", (f"{n:+d} days",))["d"] + " 00:00:00"


s = login()

# ── Warning-period settings ──────────────────────────────────────────────────
get(s, "/Settings")      # a fresh database gets its settings row on the first Settings visit
exec_sql("update SystemSettings set MaintenanceDueWarningDays=45, LcExpiryWarningDays=120, TrainingDueWarningDays=10")

m40 = raw_insert("Maintenance", {"AssetId": raw_insert("Asset"), "ScheduledDate": day(40), "Status": 0,
                                 "MaintenanceId": f"MNT-W{run}"})
page = get(s, "/Maintenances/Dashboard").text
check("maintenance dashboard heading uses the setting", "Due Within 45 Days" in page)
check("maintenance due in 40 days listed (window 45)", f"MNT-W{run}" in page)
exec_sql("update SystemSettings set MaintenanceDueWarningDays=30")
page = get(s, "/Maintenances/Dashboard").text
check("maintenance due in 40 days not listed (window 30)", f"MNT-W{run}" not in page)

lc = raw_insert("LicenceCertificationRecords", {"LCNumber": f"LC-W{run}", "Status": 4, "ExpiryDate": day(100),
                                                "LicenceTypeId": raw_insert("LCLicenceTypes")})
page = get(s, "/LicencesCertifications/Dashboard").text
check("LC dashboard heading uses the setting", "Expiring Within 120 Days" in page)
check("LC expiring in 100 days listed (window 120)", f"LC-W{run}" in page)
home = get(s, "/").text
check("home dashboard LC heading uses the setting", "Expiring (120 days)" in home)
exec_sql("update SystemSettings set LcExpiryWarningDays=90")
page = get(s, "/LicencesCertifications/Dashboard").text
check("LC expiring in 100 days not listed (window 90)", f"LC-W{run}" not in page)

t5 = raw_insert("Training", {"Name": f"TRN-SOON-{run}", "Status": 0, "DueDate": day(5), "IsDeleted": 0})
t20 = raw_insert("Training", {"Name": f"TRN-LATER-{run}", "Status": 0, "DueDate": day(20), "IsDeleted": 0})
page = get(s, "/Trainings/Dashboard").text
check("training dashboard heading uses the setting", "Due Within 10 Days" in page)
check("training due in 5 days listed (window 10)", f"TRN-SOON-{run}" in page)
check("training due in 20 days not listed (window 10)", f"TRN-LATER-{run}" not in page)

settings_page = get(s, "/Settings").text
check("Date format no longer offered on Settings", 'name="DateFormat"' not in settings_page)

# ── Authorised Person approves reference data ────────────────────────────────
ap = f"ap-{run}@e2e.local"
ensure_user(ap, ["AuthorisedPerson"])
sa = login(ap)
UNDER_REVIEW, APPROVED = 3, 1          # same enum order for all five
for ctrl, table in [("Materials", "Material"), ("Processes", "Process"), ("Assets", "Asset"),
                    ("Services", "Service"), ("Customers", "Customer")]:
    under_review, approved = UNDER_REVIEW, APPROVED
    rid = raw_insert(table, {"Status": under_review, "IsLocked": 0})
    page = get(sa, f"/{ctrl}/Details/{rid}").text
    check(f"{ctrl}: AP sees the approve/send-back panel", "SendBackToDraft" in page)
    post_form(sa, f"/{ctrl}/Details/{rid}", f"/{ctrl}/Details/{rid}", {"InputEmail": ap, "InputPassword": PW})
    st = q1(f'select Status from "{table}" where Id=?', (rid,))["Status"]
    check(f"{ctrl}: Authorised Person can approve", st == approved, st)

# ── Submit for Review is limited to Quality User / Quality Manager / Administrator ──
tr = f"trainer-{run}@e2e.local"
ensure_user(tr, ["Trainer"])
st_ = login(tr)
qu = f"qu-{run}@e2e.local"
ensure_user(qu, ["QualityUser"])
sq = login(qu)
for ctrl, table in [("Products", "Product"), ("Materials", "Material"), ("Processes", "Process"),
                    ("Assets", "Asset"), ("Services", "Service"), ("Customers", "Customer")]:
    rid = raw_insert(table, {"Status": 0, "IsLocked": 0})
    post_form(st_, f"/{ctrl}/SubmitForReview/{rid}", f"/{ctrl}/Details/{rid}", {})
    check(f"{ctrl}: Trainer cannot submit for review", q1(f'select Status from "{table}" where Id=?', (rid,))["Status"] == 0)
    post_form(sq, f"/{ctrl}/SubmitForReview/{rid}", f"/{ctrl}/Details/{rid}", {})
    check(f"{ctrl}: Quality User can submit for review", q1(f'select Status from "{table}" where Id=?', (rid,))["Status"] == 3)

# ── Periodic review of a document: author or document manager (QM / AP / Admin), per the SOP ──
EFFECTIVE = 5
qm = f"qm-{run}@e2e.local"
ensure_user(qm, ["QualityManager"])
sm = login(qm)


def effective_doc():
    did = f"DOC-R{uuid.uuid4().hex[:5]}"
    return raw_insert("AppDocument", {"DocumentId": did, "Status": EFFECTIVE, "IsLocked": 0, "Version": 1.0,
                                      "PeriodicReviewRequired": 1, "ReviewDueDate": day(-5),
                                      "AuthoredBy": "someone-else@e2e.local"}), did


doc, did = effective_doc()
st_ = login(tr)          # login() resets the single-session guard, so sign in again right before use
post_form(st_, f"/AppDocuments/InitiatePeriodicReview/{doc}", f"/AppDocuments/Library",
          {"InputEmail": tr, "InputPassword": PW})
check("Trainer (not author) cannot initiate periodic review",
      q1("select count(*) n from AppDocument where DocumentId=?", (did,))["n"] == 1)
sm = login(qm)
page = get(sm, f"/AppDocuments/Details/{doc}").text
check("Quality Manager sees Initiate periodic review", "InitiatePeriodicReview" in page)
post_form(sm, f"/AppDocuments/InitiatePeriodicReview/{doc}", f"/AppDocuments/Details/{doc}",
          {"InputEmail": qm, "InputPassword": PW})
new = q("select AuthoredBy, Status from AppDocument where DocumentId=? and Id<>?", (did, doc))
check("Quality Manager (not author) can initiate periodic review: new draft version created",
      len(new) == 1 and new[0]["Status"] == 0, [dict(r) for r in new])
check("initiator becomes author of the new version", len(new) == 1 and new[0]["AuthoredBy"] == qm)

doc2, did2 = effective_doc()
sa = login(ap)
post_form(sa, f"/AppDocuments/InitiatePeriodicReview/{doc2}", f"/AppDocuments/Details/{doc2}",
          {"InputEmail": ap, "InputPassword": PW})
check("Authorised Person can initiate periodic review",
      q1("select count(*) n from AppDocument where DocumentId=?", (did2,))["n"] == 2)

# ── Maintenance: the doer records the work ───────────────────────────────────
ops = f"ops-{run}@e2e.local"
ensure_user(ops, ["OperationsUser"])
so = login(ops)
mid = raw_insert("Maintenance", {"AssetId": raw_insert("Asset"), "ScheduledDate": day(1), "Status": 1,
                                 "MaintenanceId": f"MNT-R{run}"})
page = get(so, f"/Maintenances/Details/{mid}").text
check("operations user sees Record Work on an In Progress job", "Record Work" in page)
post_form(so, f"/Maintenances/RecordWork/{mid}", f"/Maintenances/Details/{mid}",
          {"workPerformed": "Replaced filter; leak test passed", "partsUsed": "Filter F-100", "cost": "125.50"})
row = q1("select WorkPerformed, PartsUsed, Cost from Maintenance where Id=?", (mid,))
check("work performed recorded", row["WorkPerformed"] == "Replaced filter; leak test passed", row["WorkPerformed"])
check("parts and cost recorded", row["PartsUsed"] == "Filter F-100" and float(row["Cost"]) == 125.5, (row["PartsUsed"], row["Cost"]))
check("work recording audit-logged",
      q1("select count(*) n from MaintenanceAuditLog where ParentId=? and Action='Work Recorded'", (mid,))["n"] == 1)

exec_sql("update Maintenance set Status=0 where Id=?", (mid,))
post_form(so, f"/Maintenances/RecordWork/{mid}", f"/Maintenances/Details/{mid}", {"workPerformed": "changed later"})
check("work cannot be recorded when not In Progress",
      q1("select WorkPerformed from Maintenance where Id=?", (mid,))["WorkPerformed"] == "Replaced filter; leak test passed")

ro = f"ro-{run}@e2e.local"
ensure_user(ro, ["ReadOnly"])
sr = login(ro)
exec_sql("update Maintenance set Status=1 where Id=?", (mid,))
r = post_form(sr, f"/Maintenances/RecordWork/{mid}", f"/Maintenances/Details/{mid}", {"workPerformed": "read-only edit"})
check("read-only user cannot record work",
      q1("select WorkPerformed from Maintenance where Id=?", (mid,))["WorkPerformed"] == "Replaced filter; leak test passed",
      r.status_code)

summary()
