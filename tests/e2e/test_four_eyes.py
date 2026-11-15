"""Four-eyes rule on signed steps (System Settings → Enforce Four-Eyes Rule, on by default).

For every rule in Resources/Workflow/workflow-rules.json:
  * the person recorded as having done the work (each "notBy" field) is refused: the record, its state and its
    audit trail are unchanged, and the refusal names the four-eyes rule;
  * another person with the same role signs the same step;
  * with the rule turned off, the person who did the work may sign (1.1.0 behaviour).
For the "raiser may approve" rule: the person who raised the record is refused when they also signed its
acceptance, and may approve when someone else did.
Also: the roles in workflow-rules.json match the role matrix of the workflow baseline (stages the baseline walk
does not reach — older states, supplier decisions — are tried live with every role); a returned and redone step
is judged on who did it the second time (walked through the pages); a document's author cannot open its approval
form; a wrong password is reported before the rule; the setting is on for a new site, changing it is audited, and
only an Administrator can change it.
"""
import json, os
from workflow_lib import *

HERE = os.path.dirname(os.path.abspath(__file__))
RULES = json.load(open(os.path.join(HERE, "..", "..", "Resources", "Workflow", "workflow-rules.json"), encoding="utf-8"))
BASE = json.load(open(os.path.join(HERE, "baseline", "workflow_baseline.json"), encoding="utf-8"))
X = "fe-x@e2e.local"     # did the work
Y = "fe-y@e2e.local"     # someone else with the same role
OTHER = "someone-else@e2e.local"
for u in (X, Y):
    ensure_user(u, ["QualityManager"], "FourEyes", u[3])
WORDS = [w.lower() for w in any_language("Four-eyes rule")]


def as_user(email):
    return login(email)   # one login at a time: a new login ends the other sessions


def spec_for(ctrl, action):
    return next((m for m in MODULES if m["ctrl"] == ctrl and any(a == action for a, _, _ in m["steps"])), None) \
        or next(m for m in MODULES if m["ctrl"] == ctrl and any(a == action for _, a, _ in m["returns"]))


def extra_for(m, action):
    return next((e for a, e, _ in m["steps"] if a == action), None) \
        or next((e for _, a, e in m["returns"] if a == action), {})


# States the normal walk does not reach: older-version states and the supplier decision states. The record is
# walked to the equivalent state and then put in that state with the fields the real route would leave.
EQUIV = {("Risks", 1): (2, {}), ("Risks", 3): (6, {}), ("Maintenances", 5): (0, {}),
         ("Suppliers", 2): (1, {"ApprovalStatus": 4, "SuspensionReason": "e2e"}),
         ("Suppliers", 3): (1, {"ApprovalStatus": 4, "CancellationReason": "e2e"}),
         ("Suppliers", 5): (4, {"ApprovalStatus": 5})}


def stage_record(module_name, m, state, admin):
    if (module_name, state) not in EQUIV:
        return record_at(m, state, admin)
    eq, fields = EQUIV[(module_name, state)]
    rid = record_at(m, eq, admin)
    for k, v in {**fields, "Status": state}.items():
        exec_sql(f'update "{m["table"]}" set "{k}"=? where Id=?', (v, rid))
    return rid


def record_at(m, state, admin):
    """A record of module m brought to `state` by the Administrator (rule off), using the module's own steps."""
    rid = new_record(m["table"], m["uniq"], m["prefix"], m["init"])
    for action, extra, _ in m["steps"]:
        if row(m["table"], rid)["Status"] == state:
            break
        sign(admin, m, rid, action, extra, ADMIN)
    check(f"{m['name']}: set-up record reached state {state}", row(m["table"], rid)["Status"] == state,
          row(m["table"], rid)["Status"])
    return rid


def with_fields(m, src, values):
    cid = copy_record(m["table"], m["uniq"], m["prefix"], src)
    for k, v in values.items():
        exec_sql(f'update "{m["table"]}" set "{k}"=? where Id=?', (v, cid))
    return cid


def try_as(m, cid, action, extra, email):
    before, audits = row(m["table"], cid), len(audit_rows(m, cid))
    _, msg = sign(as_user(email), m, cid, action, extra, email)
    after = row(m["table"], cid)
    return after["Status"], msg, before == after, len(audit_rows(m, cid)) == audits


# ── 1. the rules file matches the role matrix of the baseline ───────────────────────────────────────
names = {"Deviations": ["Deviations"], "Complaints": ["Complaints"], "Capas": ["CAPA"], "Changes": ["Change Control"],
         "Audits": ["Audits"], "Risks": ["Risk"], "Maintenances": ["Maintenance"], "AppDocuments": ["Documents"],
         "Suppliers": ["Suppliers", "Suppliers (qualification)"]}
for mod in RULES["modules"]:
    for st in mod["steps"]:
        key = f'{st["action"]}@{st["from"]}'
        got = next((BASE[n]["roles"][key] for n in names[mod["module"]] if key in BASE[n]["roles"]), None)
        if got is None:
            continue   # a stage the baseline walk does not reach: measured live below
        allowed = sorted(r for r, o in got.items() if o == "allowed")
        check(f"rules = baseline: {mod['module']} {key} roles", allowed == st["roles"],
              f"rules {st['roles']} / baseline {allowed}")

# ── 1b. stages the baseline does not reach (older states, supplier decisions): every role tried live ───
four_eyes(False)
role_mail = {r: (ADMIN if r == "Administrator" else f"bl-{r.lower()}@e2e.local") for r in ROLES}
for r, e in role_mail.items():
    if r != "Administrator":
        ensure_user(e, [r], "BL", r)
for mod in RULES["modules"]:
    for st in mod["steps"]:
        key = f'{st["action"]}@{st["from"]}'
        if any(key in BASE[n]["roles"] for n in names[mod["module"]]):
            continue
        m = spec_for(mod["module"], st["action"])
        src = stage_record(mod["module"], m, st["from"], login())
        allowed = []
        for r in ROLES:
            cid = copy_record(m["table"], m["uniq"], m["prefix"], src)
            sign(login(role_mail[r]), m, cid, st["action"], extra_for(m, st["action"]), role_mail[r])
            if row(m["table"], cid)["Status"] == st["to"]:
                allowed.append(r)
        check(f"rules = live role check: {mod['module']} {key} roles", sorted(allowed) == st["roles"],
              f"rules {st['roles']} / live {sorted(allowed)}")

# ── 2. every four-eyes rule ─────────────────────────────────────────────────────────────────────────
admin = login()
for mod in RULES["modules"]:
    for st in mod["steps"]:
        rule = st.get("fourEyes")
        if not rule:
            continue
        m = spec_for(mod["module"], st["action"])
        extra = extra_for(m, st["action"])
        name = f"{m['name']} {st['action']}@{st['from']}"
        four_eyes(False)
        admin = login()
        src = stage_record(mod["module"], m, st["from"], admin)
        neutral = {f: OTHER for f in rule["notBy"]}
        if rule.get("raiserReview"):
            neutral[rule["raiserReview"]] = OTHER
            neutral[mod["raisedBy"]] = OTHER
        four_eyes(True)
        for field in rule["notBy"]:
            cid = with_fields(m, src, {**neutral, field: X})
            stt, msg, same, no_audit = try_as(m, cid, st["action"], extra, X)
            check(f"{name}: refused for the user in {field}", stt == st["from"] and same and no_audit, (stt, msg))
            check(f"{name}: refusal names the four-eyes rule ({field})", any(w in msg.lower() for w in WORDS), msg)
            stt, msg, _, _ = try_as(m, cid, st["action"], extra, Y)
            check(f"{name}: another person signs it ({field})", stt == st["to"], (stt, msg))
            cid = with_fields(m, src, {**neutral, field: X})
            four_eyes(False)
            stt, msg, _, _ = try_as(m, cid, st["action"], extra, X)
            check(f"{name}: with the rule off, the user in {field} signs it", stt == st["to"], (stt, msg))
            four_eyes(True)
        if rule.get("raiserReview"):
            rv, rb = rule["raiserReview"], mod["raisedBy"]
            cid = with_fields(m, src, {**neutral, rb: X, rv: X})
            stt, msg, same, _ = try_as(m, cid, st["action"], extra, X)
            check(f"{name}: raiser who also signed {rv} is refused", stt == st["from"] and same, (stt, msg))
            check(f"{name}: raiser refusal names the four-eyes rule", any(w in msg.lower() for w in WORDS), msg)
            cid = with_fields(m, src, {**neutral, rb: X, rv: Y})
            stt, msg, _, _ = try_as(m, cid, st["action"], extra, X)
            check(f"{name}: raiser may sign when {rv} is someone else", stt == st["to"], (stt, msg))

# ── 3. a returned and redone step is judged on who redid it (through the pages) ──────────────────────
four_eyes(True)
m = module("Deviations")
rid = new_record(m["table"], m["uniq"], m["prefix"], m["init"])
sign(as_user(ADMIN), m, rid, "SubmitForEvaluation", {}, ADMIN)
sign(as_user(X), m, rid, "SubmitEvaluation", {}, X)
check("redo: X evaluated", row(m["table"], rid)["EvaluatedBy"] == X and row(m["table"], rid)["Status"] == 5)
_, msg = sign(as_user(X), m, rid, "AcceptDeviation", {}, X)
check("redo: X cannot accept their own evaluation", row(m["table"], rid)["Status"] == 5, msg)
sign(as_user(ADMIN), m, rid, "ReturnDeviation", {"reason": "e2e redo"}, ADMIN)
check("redo: returned to Evaluation, evaluator cleared", row(m["table"], rid)["Status"] == 1 and not row(m["table"], rid)["EvaluatedBy"])
sign(as_user(Y), m, rid, "SubmitEvaluation", {}, Y)
_, msg = sign(as_user(X), m, rid, "AcceptDeviation", {}, X)
check("redo: after Y redid the evaluation, X accepts it", row(m["table"], rid)["Status"] == 2, msg)

# ── 4. wrong password is reported first; the author cannot open the approval form ───────────────────
cid = with_fields(m, rid, {"Status": 5, "EvaluatedBy": X, "AcceptedBy": None})
s = as_user(X)
r = post_form(s, f"/Deviations/AcceptDeviation", f"/Deviations/Details/{cid}", {"id": cid, "email": X, "pwd": "wrong"})
page = get(s, r.headers.get("Location", f"/Deviations/Details/{cid}")).text
alert = " ".join(e.get_text(" ") for e in BeautifulSoup(page, "html.parser").select(".alert-danger")).lower()
check("wrong password: refused, and reported as a wrong password (not the rule)",
      row(m["table"], cid)["Status"] == 5 and alert and not any(w in alert for w in WORDS), alert)
d = module("Documents")
doc = record_at(d, 4, login())
exec_sql('update AppDocument set AuthoredBy=? where Id=?', (X, doc))
s = as_user(X)
r = get(s, f"/AppDocuments/Approve/{doc}", allow_redirects=False)
check("document author: the approval form is not shown (back to the document)",
      r.status_code in (301, 302) and f"/AppDocuments/Details/{doc}" in r.headers.get("Location", ""), r.status_code)
page = get(s, f"/AppDocuments/Details/{doc}").text
check("document author: the page says why", any(w in page.lower() for w in WORDS))
s = as_user(Y)
check("another approver opens the approval form", get(s, f"/AppDocuments/Approve/{doc}").status_code == 200)

# ── 5. the setting ──────────────────────────────────────────────────────────────────────────────────
four_eyes(True)
s = login()
html = get(s, "/Settings").text
fields = form_fields(html, "/Settings/Save")
check("settings page shows the four-eyes switch, on", fields is not None and fields.get("EnforceFourEyes") == "true",
      fields and fields.get("EnforceFourEyes"))
fields["OrganisationName"] = fields.get("OrganisationName") or "E2E Organisation"      # required to save
fields["OrganisationAddress"] = fields.get("OrganisationAddress") or "1 Test Street"
before = len(q("select Id from SettingsAuditLog where FieldName='EnforceFourEyes'"))
fields["EnforceFourEyes"] = "false"
post_form(s, "/Settings/Save", "/Settings", fields)
check("Administrator turns it off", q1("select EnforceFourEyes v from SystemSettings")["v"] == 0)
log = q("select OldValue, NewValue, ChangedBy from SettingsAuditLog where FieldName='EnforceFourEyes' order by Id")
check("the change is in the settings audit trail",
      len(log) == before + 1 and log[-1]["OldValue"] == "True" and log[-1]["NewValue"] == "False" and log[-1]["ChangedBy"],
      log[-1:] )
html = get(s, "/Settings").text
check("with the rule off, the page shows the GMP note", any(w.lower() in html.lower() for w in any_language("GMP note: with the four-eyes rule off, one person can do and approve the same work.")))
four_eyes(True)
s = as_user(X)
r = post_form(s, "/Settings/Save", "/Settings", {**fields, "EnforceFourEyes": "false"})
check("a Quality Manager cannot change it", q1("select EnforceFourEyes v from SystemSettings")["v"] == 1, r.status_code)

four_eyes(True)
sys.exit(summary())
