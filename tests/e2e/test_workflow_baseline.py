"""Workflow baseline: every signed step of every module, and who may sign it.

For each module a record is taken through every signed step (and every send-back / re-open route) as the
Administrator. Each step records the state it lands in, which record fields it changed and the audit-trail
entries it wrote. Then, from an exact copy of the record as it was just before each step, the step is tried
once by a user holding each single role: allowed (lands in the expected state) or refused (nothing changes).
The Administrator is tried first on every copy, so a copy that cannot be signed at all is reported, not
mistaken for a role refusal.

The result is compared with tests/e2e/baseline/workflow_baseline.json, the behaviour of MintQMS 1.1.0.
Run with E2E_WRITE_BASELINE=1 to (re)write that file — only on a version whose behaviour is the reference.
"""
import json, os, re
from e2e_lib import *

HERE = os.path.dirname(os.path.abspath(__file__))
BASELINE = os.path.join(HERE, "baseline", "workflow_baseline.json")
WRITE = os.environ.get("E2E_WRITE_BASELINE") == "1"
ONLY = [m for m in os.environ.get("E2E_MODULES", "").split(",") if m]   # optional: limit to some modules
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
                  # known 1.1.0 bug: admin re-open requires Approved, which no step reaches → refused today
                  (3, "AdminReopen", {"reason": "e2e", "_refused": True})]),
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


# ── run ──────────────────────────────────────────────────────────────────────────────────────────────
def walk(m, s):
    """Administrator walk. Returns the result and a snapshot record (copy source) per state."""
    rid = new_record(m["table"], m["uniq"], m["prefix"], m["init"])
    out, snaps = {"walk": [], "returns": []}, {}
    for action, extra, expected in m["steps"]:
        before = row(m["table"], rid)
        snaps.setdefault(before["Status"], copy_record(m["table"], m["uniq"], m["prefix"], rid))
        ba = max([a["Id"] for a in audit_rows(m, rid)] or [0])
        code, msg = sign(s, m, rid, action, extra, ADMIN)
        st, changed, aud = step_effect(m, rid, before, ba)
        ok = check(f"{m['name']}: {action} {before['Status']} → {expected if expected is not None else st}",
                   st == expected if expected is not None else st != before["Status"], f"got {st}; {msg or code}")
        out["walk"].append({"action": action, "from": before["Status"], "to": st, "changed": changed, "audit": aud})
        check(f"{m['name']}: page renders after {action}", get(s, f"/{m['ctrl']}/Details/{rid}").status_code == 200)
        if not ok:
            break
    snaps.setdefault(row(m["table"], rid)["Status"], copy_record(m["table"], m["uniq"], m["prefix"], rid))
    for frm, action, extra in m["returns"]:
        if frm not in snaps:
            check(f"{m['name']}: {action} from {frm} (no record in that state)", False)
            continue
        cid = copy_record(m["table"], m["uniq"], m["prefix"], snaps[frm])
        before = row(m["table"], cid)
        ba = max([a["Id"] for a in audit_rows(m, cid)] or [0])
        code, msg = sign(s, m, cid, action, extra, ADMIN)
        st, changed, aud = step_effect(m, cid, before, ba)
        if extra.get("_refused"):
            check(f"{m['name']}: {action} from {frm} refused (known behaviour)", st == frm, f"moved to {st}")
        else:
            check(f"{m['name']}: {action} from {frm} moves the record", st != frm, f"stayed {st}; {msg or code}")
        snaps.setdefault(st, copy_record(m["table"], m["uniq"], m["prefix"], cid))   # e.g. Suspended, for Reactivate
        out["returns"].append({"action": action, "from": frm, "to": st, "changed": changed, "audit": aud})
    return out, snaps


def tries_of(m, result):
    """(action, from-state, expected to-state, extra form data) for every walked step and route."""
    extras = {a: e for a, e, _ in m["steps"]}
    out = [(w["action"], w["from"], w["to"], extras.get(w["action"], {})) for w in result["walk"]]
    out += [(r["action"], r["from"], r["to"], dict(e)) for r, (_, _, e) in zip(result["returns"], m["returns"])]
    return out


def role_outcomes(m, snaps, result, s, email):
    """Every step tried once by one user, each from a fresh copy of the record just before the step."""
    out = {}
    for action, frm, to, extra in tries_of(m, result):
        cid = copy_record(m["table"], m["uniq"], m["prefix"], snaps[frm])
        sign(s, m, cid, action, extra, email)
        st = row(m["table"], cid)["Status"]
        out[f"{action}@{frm}"] = ("allowed" if st == to and to != frm else "refused" if st == frm else f"other:{st}")
    return out


def compare(name, got, want):
    if want is None:
        check(f"{name}: in baseline", WRITE, "module missing from the baseline file")
        return
    for part in ("walk", "returns"):
        g = [(x["action"], x["from"], x["to"], x["changed"], x["audit"]) for x in got[part]]
        w = [(x["action"], x["from"], x["to"], x["changed"], x["audit"]) for x in want.get(part, [])]
        for i in range(max(len(g), len(w))):
            a = g[i] if i < len(g) else None
            b = w[i] if i < len(w) else None
            check(f"{name}: {part} {(b or a)[0]} from {(b or a)[1]} same as baseline", a == b, f"now {a} / baseline {b}")
    for key, roles in want.get("roles", {}).items():
        for role, outcome in roles.items():
            now = got["roles"].get(key, {}).get(role)
            if now != outcome:
                check(f"{name}: {key} by {role} same as baseline", False, f"now {now} / baseline {outcome}")
    extra = set(got["roles"]) - set(want.get("roles", {}))
    check(f"{name}: no steps beyond the baseline", not extra, sorted(extra))
    check(f"{name}: role matrix same as baseline ({len(want.get('roles', {}))} steps × {len(ROLES)} roles)",
          all(got["roles"].get(k) == v for k, v in want.get("roles", {}).items()))


users = {r: (ADMIN if r == "Administrator" else ensure_user(f"bl-{r.lower()}@e2e.local", [r], "BL", r)) for r in ROLES}
emails = {r: (ADMIN if r == "Administrator" else f"bl-{r.lower()}@e2e.local") for r in ROLES}
baseline = json.load(open(BASELINE, encoding="utf-8")) if os.path.exists(BASELINE) else {}
results = {}
for m in MODULES:
    if ONLY and m["name"] not in ONLY:
        continue
    admin = login()
    res, snaps = walk(m, admin)
    matrix = {}
    # one role at a time: a new login ends the previous session (one active session per user is enforced)
    for role in ROLES:
        for k, v in role_outcomes(m, snaps, res, login(emails[role]), emails[role]).items():
            matrix.setdefault(k, {})[role] = v
    for k, v in matrix.items():
        refused_route = any(r[1] == k.split("@")[0] and str(r[0]) == k.split("@")[1] and r[2].get("_refused")
                            for r in m["returns"])
        if v["Administrator"] != "allowed" and not refused_route:
            check(f"{m['name']}: {k} can be signed from a copy (Administrator)", False, v["Administrator"])
    res["roles"] = matrix
    results[m["name"]] = res
    if not WRITE:
        compare(m["name"], res, baseline.get(m["name"]))

if WRITE and all(ok for _, ok in e2e_results()):
    os.makedirs(os.path.dirname(BASELINE), exist_ok=True)
    merged = {**baseline, **results}
    json.dump(merged, open(BASELINE, "w", encoding="utf-8"), indent=1, ensure_ascii=False, sort_keys=True)
    print(f"baseline written: {BASELINE} ({', '.join(results)})")
elif WRITE:
    print("baseline NOT written: fix the failures first")
sys.exit(summary())
