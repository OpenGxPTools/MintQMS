"""Task assignment: the person responsible for a record's next step (information, never a lock).

  * the panel shows on a record whose stage has a next step, not on a finished record;
  * only eligible people are listed: a role that can do the next step, an active account (a short wrong-password
    lock-out still counts), not ReadOnly, not excluded by the four-eyes rule (included again when the rule is off);
    a document's author for "submit for review", whatever their role; supplier qualification only when required;
  * assign / reassign / unassign through the panel: one current row, history kept (rows only end), each change in
    the record's own audit trail; an ineligible choice is refused; who may assign (a task role, or a manager), and
    ReadOnly / other roles may not;
  * not a lock: someone else allowed signs the step, and the assignment ends with who actually did it;
  * a send-back puts it back to planned; a stale row (record moved some other way) is never shown;
  * "assign the next step to" in a signature dialog names the person for the new stage, never the signer when the
    next step reviews their work; the dialog data lists the eligible people;
  * deactivating a user unassigns their tasks (with an audit line).
"""
from workflow_lib import *

m = module("Deviations")
four_eyes(True)
QM1, QM2, QU, OPS, RO, LOCKED, GONE = (f"as-{n}@e2e.local" for n in ("qm1", "qm2", "qu", "ops", "ro", "locked", "gone"))
ids = {QM1: ensure_user(QM1, ["QualityManager"], "Ana", "Manager"), QM2: ensure_user(QM2, ["QualityManager"], "Bea", "Manager"),
       QU: ensure_user(QU, ["QualityUser"], "Carl", "User"), OPS: ensure_user(OPS, ["OperationsUser"], "Dan", "Ops"),
       RO: ensure_user(RO, ["ReadOnly"], "Eve", "Reader"), LOCKED: ensure_user(LOCKED, ["QualityUser"], "Finn", "Locked"),
       GONE: ensure_user(GONE, ["QualityUser"], "Gus", "Gone")}
exec_sql("update AspNetUsers set LockoutEnd=datetime('now','+10 minutes') where Id=?", (ids[LOCKED],))   # wrong passwords
exec_sql("update AspNetUsers set LockoutEnd='9999-12-31 23:59:59.9999999+00:00' where Id=?", (ids[GONE],))   # deactivated


def panel(s, ctrl, rid):
    soup = BeautifulSoup(get(s, f"/{ctrl}/Details/{rid}").text, "html.parser")
    p = soup.select_one("#assignment-panel")
    sel = soup.select_one("#assign-user")
    people = {int(o["value"]) for o in sel.select("option") if o.get("value")} if sel else set()
    nxt = soup.select_one("#assign-next-data")
    return p, people, (json.loads(nxt.get_text()) if nxt else None), soup


def assign(s, ctrl, rid, uid, due="", note=""):
    return post_form(s, "/TaskAssignments/Assign", f"/{ctrl}/Details/{rid}",
                     {"module": ctrl, "recordId": rid, "userId": uid, "dueDate": due, "note": note})


def current(ctrl, rid):
    return q(f"select * from TaskAssignments where Module=? and RecordId=? and IsCurrent=1", (ctrl, rid))


def audit_actions(rid):
    return [a["Action"] for a in audit_rows(m, rid)]


import json
admin = login()
four_eyes(False)
dev = new_record(m["table"], m["uniq"], m["prefix"], m["init"])
sign(admin, m, dev, "SubmitForEvaluation", {}, ADMIN)               # → Evaluation (1)
four_eyes(True)

# ── who is listed ─────────────────────────────────────────────────────────────────────────────────────
p, people, nxt, _ = panel(admin, "Deviations", dev)
check("panel shown at Evaluation", p is not None and "assign-button" in str(p))
check("listed: Quality Managers and Quality Users", {ids[QM1], ids[QM2], ids[QU]} <= people, people)
check("listed: a user locked out for a few minutes (wrong passwords) still counts", ids[LOCKED] in people)
check("not listed: Operations user, ReadOnly user, deactivated user", not ({ids[OPS], ids[RO], ids[GONE]} & people), people)

# ── assign, reassign, unassign ─────────────────────────────────────────────────────────────────────────
rows_before = q1("select count(*) n from TaskAssignments")["n"]
assign(admin, "Deviations", dev, ids[QU], "2026-12-01", "please look at batch 42")
cur = current("Deviations", dev)
check("assign: one current row, for the stage", len(cur) == 1 and cur[0]["AssignedToUserId"] == ids[QU] and cur[0]["Stage"] == 1
      and cur[0]["DueDate"].startswith("2026-12-01") and cur[0]["Note"] == "please look at batch 42" and cur[0]["AssignedBy"] == ADMIN, cur)
check("assign: in the record's audit trail", audit_actions(dev)[-1] == "Task Assigned")
note = audit_rows(m, dev)
txt = q1(f'select Notes from "{m["audit"]}" where Id=?', (note[-1]["Id"],))["Notes"]
check("…naming the person, due date and note", QU in txt and "2026-12-01" in txt and "batch 42" in txt, txt)
p, _, _, soup = panel(admin, "Deviations", dev)
check("panel shows the person, the due date and the note", "Carl User" in soup.select_one("#assigned-to").get_text()
      and "batch 42" in (soup.select_one("#assignment-note").get_text() if soup.select_one("#assignment-note") else ""))
assign(admin, "Deviations", dev, ids[QM1])
cur = current("Deviations", dev)
ended = q("select * from TaskAssignments where Module='Deviations' and RecordId=? and IsCurrent=0", (dev,))
check("reassign: new current row, the old one ended as Reassigned", len(cur) == 1 and cur[0]["AssignedToUserId"] == ids[QM1]
      and len(ended) == 1 and ended[0]["EndReason"] == "Reassigned" and ended[0]["EndedBy"] == ADMIN, (cur, ended))
check("reassign: audit trail", audit_actions(dev)[-1] == "Task Reassigned")
post_form(admin, "/TaskAssignments/Unassign", f"/Deviations/Details/{dev}", {"module": "Deviations", "recordId": dev})
check("unassign: no current row; ended as Unassigned", not current("Deviations", dev)
      and q1("select EndReason r from TaskAssignments where Module='Deviations' and RecordId=? order by Id desc", (dev,))["r"] == "Unassigned")
check("unassign: audit trail", audit_actions(dev)[-1] == "Task Unassigned")
check("history kept: rows only added, never deleted", q1("select count(*) n from TaskAssignments")["n"] == rows_before + 2)
assign(admin, "Deviations", dev, ids[RO])
check("an ineligible person (ReadOnly) is refused", not current("Deviations", dev))
assign(admin, "Deviations", dev, ids[GONE])
check("a deactivated person is refused", not current("Deviations", dev))

# ── who may assign ────────────────────────────────────────────────────────────────────────────────────
s = login(QU)
assign(s, "Deviations", dev, ids[QU])
check("a Quality User (a role of this stage's task) may assign", current("Deviations", dev) and current("Deviations", dev)[0]["AssignedToUserId"] == ids[QU])
s = login(OPS)
r = assign(s, "Deviations", dev, ids[QM2])
check("an Operations user may not (refused, nothing changes)", current("Deviations", dev)[0]["AssignedToUserId"] == ids[QU], r.status_code)
s = login(RO)
assign(s, "Deviations", dev, ids[QM2])
check("a ReadOnly user may not", current("Deviations", dev)[0]["AssignedToUserId"] == ids[QU])

# ── not a lock: someone else signs; the assignment ends with who did it ──────────────────────────────
s = login(QM2)
sign(s, m, dev, "SubmitEvaluation", {}, QM2)                       # assigned to QU, done by QM2 → Pending Acceptance (5)
check("not a lock: another allowed person signs the step", row(m["table"], dev)["Status"] == 5)
t = q1("select * from TaskAssignments where Module='Deviations' and RecordId=? order by Id desc", (dev,))
check("…the assignment ended as MovedOn, by who actually did it", not t["IsCurrent"] and t["EndReason"] == "MovedOn" and t["EndedBy"] == QM2, dict(t))

# four-eyes in the list: QM2 evaluated, so QM2 cannot be named for acceptance
p, people, _, _ = panel(admin, "Deviations", dev)
check("four-eyes: the evaluator is not listed for acceptance", ids[QM2] not in people and ids[QM1] in people, people)
check("…and Quality Users are not listed (acceptance needs a manager)", ids[QU] not in people)
four_eyes(False)
p, people, _, _ = panel(admin, "Deviations", dev)
check("with the rule off, the evaluator is listed again", ids[QM2] in people)
four_eyes(True)

# ── "assign the next step to" in the signature dialog ─────────────────────────────────────────────────
s = login(QM1)
_, _, nxt, _ = panel(s, "Deviations", dev)
acc = (nxt or {}).get("actions", {}).get("/Deviations/AcceptDeviation", [])
check("dialog data: the people for the step after acceptance", nxt and {ids[QU], ids[QM2]} <= {x["id"] for x in acc}, nxt)
check("dialog data: the signer is listed for a step without a four-eyes rule", ids[QM1] in {x["id"] for x in acc})
sign(s, m, dev, "AcceptDeviation", {"assignNextTo": ids[QU]}, QM1)   # → Accepted (2); next: resolution
cur = current("Deviations", dev)
check("signing with a choice names that person for the new stage", row(m["table"], dev)["Status"] == 2 and len(cur) == 1
      and cur[0]["AssignedToUserId"] == ids[QU] and cur[0]["Stage"] == 2 and cur[0]["AssignedBy"] == QM1, cur)
s = login(QU)
# QU resolves and names themself for the approval: not allowed (the message is shown once, on the page after signing)
r = post_form(s, "/Deviations/SubmitForResolution", f"/Deviations/Details/{dev}",
              {"id": dev, "email": QU, "pwd": PW, "notes": "e2e", "assignNextTo": ids[QU]})
soup = BeautifulSoup(get(s, r.headers.get("Location", f"/Deviations/Details/{dev}")).text, "html.parser")
cur = current("Deviations", dev)
check("the signer is never named for a step that reviews their work", row(m["table"], dev)["Status"] == 3 and not cur, cur)
check("…the page says the choice was not saved", soup.select_one("#assignment-message") is not None)
check("the step itself was still signed", row(m["table"], dev)["ResolvedBy"] == QU)

# ── send-back ends it; stale rows are never shown ─────────────────────────────────────────────────────
assign(login(), "Deviations", dev, ids[QM1])
sign(login(), m, dev, "ReturnDeviation", {"reason": "e2e"}, ADMIN)    # → Accepted (2)
t = q1("select * from TaskAssignments where Module='Deviations' and RecordId=? and Stage=3 order by Id desc", (dev,))
check("a send-back keeps the person planned for the step sent back (it starts again when the record returns)",
      t["IsCurrent"] and t["StartedOn"] is None and t["AssignedToUserId"] == ids[QM1], t)
exec_sql("insert into TaskAssignments (Module, RecordId, Stage, AssignedToUserId, AssignedBy, AssignedOn, IsCurrent) values ('Deviations', ?, 4, ?, 'e2e', datetime('now'), 1)",
         (dev, ids[QM2]))
_, _, _, soup = panel(login(), "Deviations", dev)
check("a row for another stage is never shown as current", "Bea Manager" not in soup.select_one("#assigned-to").get_text())

# ── finished record: no panel ─────────────────────────────────────────────────────────────────────────
four_eyes(False)
done = new_record(m["table"], m["uniq"], m["prefix"], m["init"])
for a, e, _ in m["steps"]:
    sign(admin := login(), m, done, a, e, ADMIN)
p, _, _, _ = panel(admin, "Deviations", done)
check("a finished record has no assignment panel", row(m["table"], done)["Status"] == 4 and p is None)
four_eyes(True)

# ── documents: the author is listed for "submit for review" whatever their role ──────────────────────
d = module("Documents")
TR = "as-trainer@e2e.local"
ids[TR] = ensure_user(TR, ["Trainer"], "Hal", "Author")
doc = new_record(d["table"], d["uniq"], d["prefix"], {**d["init"], "AuthoredBy": TR})
_, people, _, _ = panel(login(), "AppDocuments", doc)
check("document draft: its author is listed (Trainer role, author of the document)", ids[TR] in people, people)
check("…a Quality Manager who is not the author is not (only the author or an Administrator can submit)", ids[QM1] not in people)

# ── suppliers: qualification is a task only when qualification is required ───────────────────────────
for name, expected in (("Suppliers", None), ("Suppliers (qualification)", True)):
    sm = module(name)
    four_eyes(False)
    sid = new_record(sm["table"], sm["uniq"], sm["prefix"], sm["init"])
    for a, e, _ in sm["steps"][:2]:
        sign(login(), sm, sid, a, e, ADMIN)
    four_eyes(True)
    p, _, _, _ = panel(login(), "Suppliers", sid)
    check(f"{name}: approved supplier {'has' if expected else 'has no'} next-step panel", (p is not None) == bool(expected))

# ── deactivating a user unassigns their tasks ─────────────────────────────────────────────────────────
four_eyes(False)
rec = new_record(m["table"], m["uniq"], m["prefix"], m["init"])
sign(login(), m, rec, "SubmitForEvaluation", {}, ADMIN)
four_eyes(True)
DEACT = f"as-deact-{run}@e2e.local"   # a new user each run: the previous one stays deactivated
ids[DEACT] = ensure_user(DEACT, ["QualityUser"], "Ivy", "Leaving")
s = login()
assign(s, "Deviations", rec, ids[DEACT])
check("set-up: task assigned to the user who will leave", current("Deviations", rec) and current("Deviations", rec)[0]["AssignedToUserId"] == ids[DEACT])
html = get(s, f"/AppUsers/ManageRoles?userId={ids[DEACT]}").text
fields = form_fields(html, "ManageRoles")
fields["IsActive"] = "false"
post_form(s, "/AppUsers/ManageRoles", f"/AppUsers/ManageRoles?userId={ids[DEACT]}", fields)
check("set-up: user deactivated", q1("select LockoutEnd l from AspNetUsers where Id=?", (ids[DEACT],))["l"])
t = q1("select * from TaskAssignments where Module='Deviations' and RecordId=? order by Id desc", (rec,))
check("deactivation: the task is unassigned (UserDeactivated)", not t["IsCurrent"] and t["EndReason"] == "UserDeactivated", dict(t))
check("deactivation: noted in the record's audit trail", audit_actions(rec)[-1] == "Task Unassigned")

# ── stages added 2026-11-16: supplier suspension / cancellation decisions ─────────────────────────────
sm = module("Suppliers")
for state, action, extra, to, label in ((2, "Suspend", {"suspensionReason": "e2e"}, 6, "Pending Suspension"),
                                        (3, "Cancel", {"cancellationReason": "e2e"}, 7, "Pending Cancellation")):
    four_eyes(True)
    sid = new_record(sm["table"], sm["uniq"], sm["prefix"],
                     {**sm["init"], "Status": state, "ApprovalStatus": 4, "AssessedBy": QM1, "AssessedOn": NOW})
    p, people, _, _ = panel(login(), "Suppliers", sid)
    check(f"supplier {label}: the decision has a next-step panel", p is not None)
    check(f"…managers listed, but not whoever recommended it (four-eyes)", ids[QM2] in people and ids[QM1] not in people, people)
    check(f"…Quality Users are not listed (the decision needs a manager)", ids[QU] not in people)
    _, msg = sign(login(QM1), sm, sid, action, extra, QM1)
    check(f"{label}: the person who recommended it cannot decide it", row("Supplier", sid)["Status"] == state and "our-eyes" in msg, msg)
    assign(login(), "Suppliers", sid, ids[QM1])
    check(f"…and cannot be assigned to it", not current("Suppliers", sid))
    assign(login(), "Suppliers", sid, ids[QM2])
    sign(login(QM2), sm, sid, action, extra, QM2)
    t = q1("select * from TaskAssignments where Module='Suppliers' and RecordId=? order by Id desc", (sid,))
    check(f"{label}: another manager decides; the assignment ends with who did it",
          row("Supplier", sid)["Status"] == to and not t["IsCurrent"] and t["EndReason"] == "MovedOn" and t["EndedBy"] == QM2, dict(t))
    four_eyes(False)
    sid2 = copy_record("Supplier", sm["uniq"], sm["prefix"], sid)
    exec_sql("update Supplier set Status=? where Id=?", (state, sid2))
    sign(login(QM1), sm, sid2, action, extra, QM1)
    check(f"{label}: with the rule off, the recommender may decide (as in 1.1.0)", row("Supplier", sid2)["Status"] == to)
four_eyes(True)

# ── a new supplier review ends the assignment of the stage it leaves ──────────────────────────────────
qm = module("Suppliers (qualification)")
sid = new_record(qm["table"], qm["uniq"], qm["prefix"], {**qm["init"], "Status": 4, "ApprovalStatus": 4,
                                                          "AssessedBy": QM1, "ApprovedBy": QM1})
assign(login(), "Suppliers", sid, ids[QM2])
check("set-up: qualification of an approved supplier assigned", current("Suppliers", sid))
post_form(login(), "/Suppliers/CreateReview", f"/Suppliers/CreateReview/{sid}",
          {"supplierId": sid, "ReviewTrigger": "0", "ReviewSummary": "e2e"})
t = q1("select * from TaskAssignments where Module='Suppliers' and RecordId=? order by Id desc", (sid,))
check("new review: supplier back to Pending and the old assignment ended",
      row("Supplier", sid)["Status"] == 0 and not t["IsCurrent"] and t["EndReason"] == "MovedOn", dict(t))

# ── records left in older states: Risk Assessment / Review, Maintenance Overdue ───────────────────────
rm = module("Risk")
rk = new_record(rm["table"], rm["uniq"], rm["prefix"], {**rm["init"], "Status": 1})
p, people, _, _ = panel(login(), "Risks", rk)
check("risk in the old Assessment state: next-step panel (submit for review)", p is not None and ids[QU] in people, people)
rk = new_record(rm["table"], rm["uniq"], rm["prefix"], {**rm["init"], "Status": 3, "ReviewedBy": QM1, "AcceptedBy": QM2})
p, people, _, _ = panel(login(), "Risks", rk)
check("risk in the old Review state: next-step panel (close), without who submitted it for review",
      p is not None and ids[QM2] in people and ids[QM1] not in people, people)
_, msg = sign(login(QM1), rm, rk, "CloseRisk", {"reviewConclusion": "e2e"}, QM1)
check("…and the four-eyes rule now applies there too", row("Risk", rk)["Status"] == 3 and "our-eyes" in msg, msg)
mm = module("Maintenance")
mt = new_record(mm["table"], mm["uniq"], mm["prefix"], {**mm["init"], "Status": 5})
p, people, _, _ = panel(login(), "Maintenances", mt)
check("maintenance in the old Overdue state: next-step panel (start work), operations users listed", p is not None and ids[OPS] in people, people)

four_eyes(True)
sys.exit(summary())
