"""Shared helpers for the workflow tests (test_workflow_baseline.py, test_four_eyes.py): building records of every
workflow module directly in the database, signing steps through the web pages, and the module definitions
(record table, steps, send-back/re-open routes)."""
import os, re
from e2e_lib import *

HERE = os.path.dirname(os.path.abspath(__file__))
ROLES = ["Administrator", "QualityManager", "QualityUser", "RegulatoryManager", "RegulatoryUser", "Trainer",
         "AuthorisedPerson", "Auditor", "OperationsUser", "CommercialUser", "ReadOnly"]
run = uuid.uuid4().hex[:6]
NOW = "2026-09-01 00:00:00"
FUTURE = "2027-06-01 00:00:00"
SKIP_FILL = re.compile(r"(By|On|Date|Path|Reason|Notes?|Url|Email|Hash|Id|Number|Snapshot|Json|Token)$")
_seq = [0]


def e2e_results():
    import e2e_lib
    return e2e_lib._results


# ── records ──────────────────────────────────────────────────────────────────────────────────────────
def insert(table, cols):
    exec_sql(f'insert into "{table}" ({", ".join(f'"{k}"' for k in cols)}) values ({", ".join("?" for _ in cols)})',
             tuple(cols.values()))
    return q1(f'select max(Id) i from "{table}"')["i"]


_TYPES = {}


def clr_types(table):
    """Property -> .NET type for a table, from the EF model snapshot (SQLite keeps dates as TEXT)."""
    if not _TYPES:
        snap = open(os.path.join(HERE, "..", "..", "Migrations", "ApplicationDbContextModelSnapshot.cs"), encoding="utf-8").read()
        for block in re.split(r'modelBuilder\.Entity\("', snap)[1:]:
            t = re.search(r'b\.ToTable\("(\w+)"', block)
            if t:
                _TYPES.setdefault(t.group(1), dict((n, ty) for ty, n in re.findall(r'b\.Property<([\w?.]+)>\("(\w+)"\)', block)))
    return _TYPES.get(table, {})


def columns(table):
    return q(f'pragma table_info("{table}")')


def new_record(table, uniq, prefix, overrides):
    """Insert a record with every required column filled, and free-text fields given content."""
    _seq[0] += 1
    vals = {}
    for c in columns(table):
        n, t = c["name"], (c["type"] or "").upper()
        if c["pk"]:
            continue
        if n in overrides:
            vals[n] = new_record("Asset", "AssetId", "AST-BL", {"Status": 1}) if overrides[n] == "ASSET" else overrides[n]
        elif n == uniq:
            vals[n] = f"{prefix}-{run}-{_seq[0]}"
        elif c["notnull"] and c["dflt_value"] is None:
            ct = clr_types(table).get(n, "")
            vals[n] = "e2e" if ct == "string" else (NOW if ct.startswith(("DateTime", "DateOnly")) else 0)
        elif clr_types(table).get(n) == "string" and not SKIP_FILL.search(n):
            vals[n] = "e2e text"
    return insert(table, vals)


def row(table, rid):
    return dict(q1(f'select * from "{table}" where Id=?', (rid,)))


def copy_record(table, uniq, prefix, rid):
    """An exact copy of a record (same state, same fields), with a new number."""
    _seq[0] += 1
    r = row(table, rid)
    r.pop("Id")
    r[uniq] = f"{prefix}-{run}-{_seq[0]}"
    return insert(table, r)


def audit_rows(m, rid):
    return q(f'select Id, Action from "{m["audit"]}" where "{m["audit_key"]}"=? order by Id', (rid,))


# ── signing ──────────────────────────────────────────────────────────────────────────────────────────
def sign(s, m, rid, action, extra, email):
    details = f"/{m['ctrl']}/Details/{rid}"
    data = {"id": rid, m["email_f"]: email, m["pwd_f"]: PW, "notes": "e2e",
            **{k: (rid if v == "ID" else v) for k, v in extra.items() if not k.startswith("_")}}
    r = post_form(s, f"/{m['ctrl']}/{action}", details, data)
    loc = r.headers.get("Location", "")
    msg = ""
    if r.status_code in (301, 302) and "/Details/" in loc:
        soup = BeautifulSoup(get(s, loc).text, "html.parser")
        el = soup.select_one(".alert-danger")
        msg = " ".join(el.get_text(" ").split()) if el else ""
    return r.status_code, msg


def step_effect(m, rid, before, before_audit):
    after = row(m["table"], rid)
    changed = sorted(k for k in after if after[k] != before.get(k) and k not in ("Status",))
    new_audit = [a["Action"] for a in audit_rows(m, rid) if a["Id"] > before_audit]
    return after["Status"], changed, new_audit


# ── modules ──────────────────────────────────────────────────────────────────────────────────────────
MODULES = [
    dict(name="Deviations", ctrl="Deviations", table="Deviation", uniq="DeviationId", prefix="DEV-BL",
         audit="DeviationAuditLog", audit_key="ParentId", email_f="email", pwd_f="pwd",
         init={"Status": 0, "IdentifiedBy": ADMIN, "IdentifiedOn": NOW, "Severity": 1, "RootCauseCategory": 1, "EventType": 1},
         steps=[("SubmitForEvaluation", {}, 1), ("SubmitEvaluation", {}, 5), ("AcceptDeviation", {}, 2),
                ("SubmitForResolution", {}, 3), ("ApproveDeviation", {}, 4)],
         returns=[(1, "ReturnDeviation", {"reason": "e2e"}), (5, "ReturnDeviation", {"reason": "e2e"}),
                  (2, "ReturnDeviation", {"reason": "e2e"}), (3, "ReturnDeviation", {"reason": "e2e"}),
                  (4, "AdminReopen", {"reason": "e2e"})]),
    dict(name="Complaints", ctrl="Complaints", table="Complaint", uniq="ComplaintId", prefix="CMP-BL",
         audit="ComplaintAuditLog", audit_key="ParentId", email_f="email", pwd_f="pwd",
         init={"Status": 0, "ReceivedBy": ADMIN, "ReceivedOn": NOW, "Severity": 1, "RootCauseCategory": 1},
         steps=[("Evaluation", {}, 1), ("PendingAcceptance", {}, 2), ("Accept", {}, 5),
                ("SubmitForResolution", {}, 3), ("Closed", {}, 4)],
         returns=[(1, "ReturnComplaint", {"reason": "e2e"}), (2, "ReturnComplaint", {"reason": "e2e"}),
                  (5, "ReturnComplaint", {"reason": "e2e"}), (3, "ReturnComplaint", {"reason": "e2e"}),
                  (4, "AdminReopen", {"reason": "e2e"})]),
    dict(name="CAPA", ctrl="Capas", table="Capa", uniq="CapaId", prefix="CAPA-BL",
         audit="CapaAuditLog", audit_key="CapaId", email_f="email", pwd_f="password",
         init={"Status": 0, "DeterminedBy": ADMIN, "DeterminedOn": NOW, "TargetCompletionDate": FUTURE},
         steps=[("SubmitForAcceptance", {}, 5), ("AcceptCapa", {}, 3), ("SubmitForApproval", {}, 6),
                ("ApproveCapa", {}, 4)],
         returns=[(5, "ReturnCapa", {}), (3, "ReturnCapa", {}), (6, "ReturnCapa", {}),
                  (4, "AdminReopen", {"reason": "e2e", "pwd": PW})]),
    dict(name="Change Control", ctrl="Changes", table="Change", uniq="ChangeId", prefix="CHG-BL",
         audit="ChangeAuditLog", audit_key="ParentId", email_f="email", pwd_f="pwd",
         init={"Status": 0, "ProposedBy": ADMIN, "ProposedOn": NOW, "DetailedActionPlanRequired": 0,
               "ChangeType": 1, "Nature": 1, "RiskClassification": 1},
         steps=[("SubmitForAssessment", {}, 1), ("SubmitForAcceptance", {}, 5), ("AcceptChange", {}, 3),
                ("SubmitForApproval", {}, 6), ("ApproveChange", {}, 4)],
         returns=[(5, "ReturnChange", {}), (3, "ReturnChange", {}), (6, "ReturnChange", {}),
                  (4, "AdminReopen", {"reason": "e2e"})]),
]


MODULES += [
    dict(name="Audits", ctrl="Audits", table="Audit", uniq="AuditId", prefix="AUD-BL",
         audit="AuditAuditLog", audit_key="ParentId", email_f="email", pwd_f="pwd",
         init={"Status": 0, "ScheduledBy": ADMIN, "ScheduledOn": NOW, "Type": 1, "Scope": 1,
               "CriticalFindings": 0, "MajorFindings": 0, "MinorFindings": 0, "Observations": 0},
         steps=[("InProgress", {}, 1), ("ReportDraft", {}, 2), ("UnderReview", {}, 3), ("Approved", {}, 4),
                ("Closed", {}, 5)],
         returns=[(3, "ReturnForRevision", {"returnReason": "e2e"}), (4, "ReturnToReview", {"returnReason": "e2e"}),
                  (5, "AdminReopen", {"reason": "e2e"})]),
    dict(name="Risk", ctrl="Risks", table="Risk", uniq="RiskId", prefix="RSK-BL",
         audit="RiskAuditLog", audit_key="ParentId", email_f="email", pwd_f="pwd",
         init={"Status": 0, "IdentifiedBy": ADMIN, "IdentifiedOn": NOW, "Category": 1, "Severity": 3,
               "Probability": 3, "Detectability": 3, "InitialRPN": 27, "ResidualSeverity": 2, "ResidualProbability": 2,
               "ResidualDetectability": 2, "ResidualRPN": 8, "RiskAcceptable": 1},
         steps=[("SubmitForAssessment", {}, None), ("AcceptAssessment", {"assessmentNotes": "e2e"}, None),
                ("SubmitForReview", {}, None), ("CloseRisk", {"reviewConclusion": "e2e"}, None)],
         returns=[(5, "ReturnFromAssessment", {}), (6, "ReturnFromReview", {}), (4, "AdminReopen", {"reason": "e2e"})]),
    dict(name="Maintenance", ctrl="Maintenances", table="Maintenance", uniq="MaintenanceId", prefix="MNT-BL",
         audit="MaintenanceAuditLog", audit_key="ParentId", email_f="email", pwd_f="pwd",
         init={"Status": 0, "ScheduledBy": ADMIN, "ScheduledOn": NOW, "ScheduledDate": NOW, "Type": 1,
               "Priority": 1, "Frequency": 0, "AssetId": "ASSET"},
         steps=[("StartWork", {}, 1), ("CompleteWork", {}, 2), ("ReviewWork", {}, None)],
         returns=[(1, "SendBackToScheduled", {"notes": "e2e"}), (2, "SendBackToInProgress", {"notes": "e2e"}),
                  (3, "SendBackToCompleted", {"notes": "e2e"}), (0, "Suspend", {"reason": "e2e"}),
                  (6, "Reactivate", {}),
                  # 1.1.0 refused this (it required Approved, which no step reaches); fixed in 1.2.0: Reviewed → Completed
                  (3, "AdminReopen", {"reason": "e2e"})]),
    dict(name="Documents", ctrl="AppDocuments", table="AppDocument", uniq="DocumentId", prefix="DOC-BL",
         audit="DocumentAuditLog", audit_key="AppDocumentId", email_f="InputEmail", pwd_f="InputPassword",
         init={"Status": 0, "AuthoredBy": ADMIN, "AuthoredOn": NOW, "Version": 1, "FileType": "application/pdf",
               "FileExtension": ".pdf", "IsLocked": 0, "PeriodicReviewRequired": 0, "TrainingRequired": 0, "IsExternal": 0},
         steps=[("SubmitForReview", {}, 4), ("Approve", {"DocumentId": "ID"}, None)],
         returns=[(4, "ReturnForRework", {"ReturnReason": "e2e"}), (5, "ObsoleteDocument", {"ObsoleteReason": "e2e"})]),
    dict(name="Suppliers", ctrl="Suppliers", table="Supplier", uniq="SupplierCode", prefix="SUP-BL",
         audit="SupplierAuditLog", audit_key="ParentId", email_f="email", pwd_f="pwd",
         init={"Status": 0, "CreatedOn": NOW, "CreatedBy": ADMIN, "QualificationRequired": 0, "ReviewFrequency": 1,
               "ReviewTrigger": 0, "RiskLevel": 2, "SupplierType": 1, "TestingRequirement": 0},
         steps=[("StartAssessment", {}, 1), ("Approve", {}, 4)],
         returns=[(1, "ReturnToAssessment", {}), (4, "Suspend", {"suspensionReason": "e2e"}),
                  (4, "Cancel", {"cancellationReason": "e2e"}), (6, "Reactivate", {})]),
    dict(name="Suppliers (qualification)", ctrl="Suppliers", table="Supplier", uniq="SupplierCode", prefix="SUQ-BL",
         audit="SupplierAuditLog", audit_key="ParentId", email_f="email", pwd_f="pwd",
         init={"Status": 0, "CreatedOn": NOW, "CreatedBy": ADMIN, "QualificationRequired": 1, "ReviewFrequency": 1,
               "ReviewTrigger": 0, "RiskLevel": 2, "SupplierType": 1, "TestingRequirement": 0, "NextReviewDate": FUTURE},
         steps=[("StartAssessment", {}, 1), ("Approve", {}, 4),
                ("Qualify", {"qualJustification": "e2e", "qualBasis": "AuditResult", "qualRefDoc": "e2e",
                             "riskLevel": "Medium", "riskJustification": "e2e"}, 5)],
         returns=[]),
]




def four_eyes(on):
    """Switch System Settings → Enforce Four-Eyes Rule (the settings row exists from the first start)."""
    exec_sql("update SystemSettings set EnforceFourEyes=?", (1 if on else 0,))


def module(name):
    return next(m for m in MODULES if m["name"] == name)
