"""Signed workflows behave the same in every language.

Takes a deviation, a CAPA, a change and a complaint through every signed step of their workflow, with the site in
whatever language the suite runs (run_all in en, es and pt-BR), and checks:
  * each step lands in exactly the state it lands in English (the expected sequences below);
  * a wrong password is refused and changes nothing, and the refusal is shown to the user;
  * a step whose required fields are empty is refused and changes nothing;
  * the audit trail entries are written in English, whatever the language;
  * every record page renders (no error page) at every step.
"""
import json, os
from e2e_lib import *

TR = json.load(open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "Resources", "Localization", "translations.json"), encoding="utf-8"))

run = uuid.uuid4().hex[:6]
NOW = "2026-09-01 00:00:00"
s = login()


def page_ok(path):
    r = get(s, path)
    return r.status_code == 200 and "An unhandled exception" not in r.text and "Error." not in r.text[:3000]


def insert(table, cols):
    keys = ", ".join(cols); qs = ", ".join("?" for _ in cols)
    exec_sql(f'insert into "{table}" ({keys}) values ({qs})', tuple(cols.values()))
    return q1(f'select max(Id) i from "{table}"')["i"]


def status(table, rid):
    return q1(f'select Status from "{table}" where Id=?', (rid,))["Status"]


def drive(module, table, rid, pwd_field, steps, audit_table, audit_key, refusal_step, empty_field):
    details = f"/{module}/Details/{rid}"
    check(f"{module}: record page renders", page_ok(details))
    # 1) wrong password at the first step → refused, nothing changes, an error is shown
    first_action = steps[0][0]
    before = status(table, rid)
    r = post_form(s, f"/{module}/{first_action}", details, {"id": rid, "email": ADMIN, pwd_field: "wrong-password"})
    shown = get(s, r.headers.get("Location", details)).text
    check(f"{module}: wrong password refused", status(table, rid) == before, status(table, rid))
    check(f"{module}: refusal shown to the user", "alert-danger" in shown or "text-danger" in shown)
    # 2) walk the workflow
    seq = []
    for i, (action, expected) in enumerate(steps):
        if i == refusal_step and empty_field:
            saved = q1(f'select "{empty_field}" v from "{table}" where Id=?', (rid,))["v"]
            exec_sql(f'update "{table}" set "{empty_field}"=NULL where Id=?', (rid,))
            st = status(table, rid)
            post_form(s, f"/{module}/{action}", details, {"id": rid, "email": ADMIN, pwd_field: PW, "notes": "e2e"})
            check(f"{module}: {action} refused while {empty_field} is empty", status(table, rid) == st, status(table, rid))
            exec_sql(f'update "{table}" set "{empty_field}"=? where Id=?', (saved, rid))
        post_form(s, f"/{module}/{action}", details, {"id": rid, "email": ADMIN, pwd_field: PW, "notes": "e2e"})
        seq.append(status(table, rid))
        check(f"{module}: {action} → {expected}", seq[-1] == expected, seq)
        check(f"{module}: page renders after {action}", page_ok(details))
    # 3) audit trail English
    acts = [r["Action"] for r in q(f'select Action from "{audit_table}" where "{audit_key}"=?', (rid,))]
    translated = {v for e in TR.values() for k, v in e.items() if k in ("es", "pt-BR")} - set(TR)
    check(f"{module}: audit trail entries in English", acts and not any(a in translated for a in acts), acts[:6])


# ── Deviation ────────────────────────────────────────────────────────────────────────────────────────
dev = insert("Deviation", {"DeviationId": f"DEV-WF-{run}", "Title": "WF dev", "Identification": "found", "IdentifiedBy": ADMIN,
                           "IdentifiedOn": NOW, "Status": 0, "IsDeleted": 0, "RetentionExtended": 0, "Severity": 1, "EventType": 0,
                           "Evaluation": "eval", "Investigation": "inv", "RootCause": "rc", "RootCauseCategory": 1,
                           "CorrectiveAction": "ca", "Resolution": "res"})
drive("Deviations", "Deviation", dev, "pwd",
      [("SubmitForEvaluation", 1), ("SubmitEvaluation", 5), ("AcceptDeviation", 2), ("SubmitForResolution", 3), ("ApproveDeviation", 4)],
      "DeviationAuditLog", "ParentId", 1, "Investigation")

# ── CAPA ─────────────────────────────────────────────────────────────────────────────────────────────
capa = insert("Capa", {"CapaId": f"CAPA-WF-{run}", "Title": "WF capa", "CorrectiveAction": "ca", "PreventiveAction": "pa",
                       "DeterminedBy": ADMIN, "DeterminedOn": NOW, "Status": 0, "IsDeleted": 0, "RetentionExtended": 0, "Type": 0,
                       "Assessment": "assess", "Implementation": "impl", "TargetCompletionDate": "2027-01-01 00:00:00"})
drive("Capas", "Capa", capa, "password",
      [("SubmitForAcceptance", 5), ("AcceptCapa", 3), ("SubmitForApproval", 6), ("ApproveCapa", 4)],
      "CapaAuditLog", "CapaId", 2, "Implementation")

# ── Change ───────────────────────────────────────────────────────────────────────────────────────────
chg = insert("Change", {"ChangeId": f"CHG-WF-{run}", "Title": "WF change", "Proposal": "prop", "ProposedBy": ADMIN, "ProposedOn": NOW,
                        "Status": 0, "IsDeleted": 0, "RetentionExtended": 0, "DetailedActionPlanRequired": 0,
                        "Assessment": "assess", "Implementation": "impl", "ChangeType": 1, "Nature": 1, "RiskClassification": 1})
drive("Changes", "Change", chg, "pwd",
      [("SubmitForAssessment", 1), ("SubmitForAcceptance", 5), ("AcceptChange", 3), ("SubmitForApproval", 6), ("ApproveChange", 4)],
      "ChangeAuditLog", "ParentId", 1, "Assessment")

# ── Complaint ────────────────────────────────────────────────────────────────────────────────────────
cmp_ = insert("Complaint", {"ComplaintId": f"CMP-WF-{run}", "Title": "WF complaint", "Description": "d", "ReceivedBy": ADMIN,
                            "ReceivedOn": NOW, "Status": 0, "Severity": 1, "IsDeleted": 0, "RetentionExtended": 0,
                            "Evaluation": "eval", "Investigation": "inv", "RootCauseCategory": 1, "CorrectiveAction": "ca",
                            "Resolution": "res"})
drive("Complaints", "Complaint", cmp_, "pwd",
      [("Evaluation", 1), ("PendingAcceptance", 2), ("Accept", 5), ("SubmitForResolution", 3), ("Closed", 4)],
      "ComplaintAuditLog", "ParentId", 1, "Evaluation")

sys.exit(summary())
