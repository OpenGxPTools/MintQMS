"""Task assignment planning: who does each step of a record, planned ahead.

  * the Responsibilities box lists the record's remaining route (Deviation at Evaluation: Evaluate › Accept › Resolve ›
    Approve), the step it is at marked "Now"; each step lists only the people who can do it;
  * saving a plan assigns the current step at once (one e-mail) and PLANS the later ones (no e-mail, StartedOn empty),
    each in the record's audit trail; a past due date, a day count outside 1–365 and an ineligible person are refused;
  * a planned person sees the step under Home → Coming Up, not My Tasks; the signature dialog has them chosen already;
  * when the record reaches the step the plan starts: My Tasks, due date = start + days, e-mail "ready for you now";
  * the person chosen in the signature dialog wins over the plan; a planned person the four-eyes rule now keeps out is
    not started (the step falls back to Available Tasks, the page says so, the audit trail records it);
  * one person planned for two steps the four-eyes rule keeps apart gives a warning;
  * sent back: the step sent back goes back to planned for its person, the step returned to goes to the person who did
    it before (e-mail "sent back"); when the record comes forward again the planned person takes over again;
  * the Task Assignments report links each row to the record with the dialog opened (?plan=1);
  * Home shows due dates (Available Tasks and the Active Items cards).
"""
import json
import sys

from workflow_lib import *

m = module("Deviations")
four_eyes(True)
QM1, QM2, QU, QU2 = (f"pl-{n}@e2e.local" for n in ("qm1", "qm2", "qu", "qu2"))
ids = {QM1: ensure_user(QM1, ["QualityManager"], "Ana", "Planner"), QM2: ensure_user(QM2, ["QualityManager"], "Bea", "Planner"),
       QU: ensure_user(QU, ["QualityUser"], "Carl", "Planner"), QU2: ensure_user(QU2, ["QualityUser"], "Dina", "Planner")}
admin = login()
get(admin, "/Settings")
exec_sql("update SystemSettings set SendAssignmentEmails=1, SmtpHost='localhost', SmtpPort=2599, EmailFromAddress='qms@e2e.local', "
         "SiteWebAddress='http://localhost:5099'")
TODAY = q1("select date('now','localtime') d")["d"]


def new_dev():
    return new_record(m["table"], m["uniq"], m["prefix"], {**m["init"], "Status": 1})


def page(s, rid, extra=""):
    return BeautifulSoup(get(s, f"/Deviations/Details/{rid}{extra}").text, "html.parser")


def plan(s, rid, lines):
    """lines: [(stage, user id or 0, due date or days, note)] → the page shown after saving."""
    data = {"module": "Deviations", "recordId": rid}
    for i, (stage, uid, due, note) in enumerate(lines):
        data[f"steps[{i}].Stage"] = stage
        data[f"steps[{i}].UserId"] = uid
        data[f"steps[{i}].Note"] = note
        if isinstance(due, int):
            data[f"steps[{i}].DueDays"] = due
        else:
            data[f"steps[{i}].DueDate"] = due or ""
    r = post_form(s, "/TaskAssignments/Plan", f"/Deviations/Details/{rid}", data)
    return BeautifulSoup(get(s, r.headers.get("Location", f"/Deviations/Details/{rid}")).text, "html.parser")


def rows(rid):
    return {t["Stage"]: dict(t) for t in q("select * from TaskAssignments where Module='Deviations' and RecordId=? and IsCurrent=1", (rid,))}


def mails(rid):
    return q("select * from EmailOutbox where Module='Deviations' and RecordId=? order by Id", (rid,))


def message(soup):
    el = soup.select_one("#assignment-message")
    return (el.get("class", []), el.get_text(" ", strip=True)) if el else ([], "")


def home(email):
    soup = BeautifulSoup(get(login(email), "/").text, "html.parser")
    def keyed(pane):
        p = soup.select_one(pane)
        return {(r["data-module"], int(r["data-record"])): r for r in p.select("tr[data-module]")} if p else {}
    return keyed("#pane-my-tasks"), keyed("#pane-waiting"), keyed("#pane-coming"), soup


def audits(rid, action):
    return q("select * from DeviationAuditLog where ParentId=? and Action=? order by Id", (rid, action))


# ── the menu, the rules shown per step ──
nav = BeautifulSoup(get(login(QM1), "/").text, "html.parser")
check("Reports menu has Task Assignments for a Quality Manager", any("/Reports/TaskAssignments" in a_.get("href", "") for a_ in nav.select(".dropdown-menu a")))
nav = BeautifulSoup(get(login(QU), "/").text, "html.parser")
check("…not for a Quality User", not any("/Reports/TaskAssignments" in a_.get("href", "") for a_ in nav.select(".dropdown-menu a")))

# ── the box and the form ──────────────────────────────────────────────────────────────────────────────
A = new_dev()
soup = page(admin, A)
route = [(int(tr["data-stage"]), tr.select_one(".fw-semibold").get_text(strip=True)) for tr in soup.select("#responsibilities tr[data-stage]")]
check("the box lists the remaining route with plain task names", route == [(1, "Evaluate"), (5, "Accept"), (2, "Resolve"), (3, "Approve")], route)
check("…the step the record is at is marked Now", "Now" in soup.select_one("#responsibilities tr[data-stage='1']").get_text())
acc = {int(o["value"]) for o in soup.select("#plan-user-5 option") if o.get("value") not in (None, "0")}
ev = {int(o["value"]) for o in soup.select("#assign-user option") if o.get("value") not in (None, "0")}
check("each step lists only who can do it (Accept: managers, not quality users)", ids[QM1] in acc and ids[QU] not in acc, acc)
check("…Evaluate: quality users too", ids[QU] in ev, ev)
st5 = soup.select_one(".plan-step[data-stage='5']")
check("each step says who can do it", "Who can do it:" in soup.select_one(".plan-step[data-stage='1'] .plan-rules").get_text()
      and "Quality User" in soup.select_one(".plan-step[data-stage='1'] .plan-rules").get_text()
      and "Quality User" not in st5.select_one(".plan-rules").get_text(), st5.select_one(".plan-rules").get_text(" ", strip=True))
check("…and whom the four-eyes rule keeps out (Accept: not who does Evaluate)", "not the person who does “Evaluate”" in st5.select_one(".plan-rules").get_text()
      and st5["data-not-same"] == "1", st5.get("data-not-same"))
st3 = soup.select_one(".plan-step[data-stage='3']")
check("…Approve: not who does Evaluate or Resolve; the raiser only if someone else accepts", st3["data-not-same"] == "1,2" and st3["data-raiser-other"] == "5"
      and "raised the record" in st3.get_text(), (st3.get("data-not-same"), st3.get("data-raiser-other")))
check("the form has a day count (not a date) for later steps", soup.select_one("#plan-days-5") is not None and soup.select_one("#assign-due") is not None)

# ── refused ───────────────────────────────────────────────────────────────────────────────────────────
soup = plan(admin, A, [(1, ids[QU], "2020-01-01", "")])
check("a due date in the past is refused", "alert-danger" in message(soup)[0] and not rows(A), message(soup))
soup = plan(admin, A, [(5, ids[QM1], 0, "")])
check("a day count outside 1–365 is refused", "alert-danger" in message(soup)[0] and not rows(A), message(soup))
soup = plan(admin, A, [(5, ids[QU], 3, "")])
check("someone who cannot do a later step is refused", "alert-danger" in message(soup)[0] and not rows(A), message(soup))

# ── a whole plan ──────────────────────────────────────────────────────────────────────────────────────
s = login(QM1)
soup = plan(s, A, [(1, ids[QU], "2099-01-31", "start here"), (5, ids[QM1], 3, ""), (2, ids[QU2], 10, "fix it"), (3, ids[QM2], 5, "")])
r = rows(A)
check("saved: the step at Now is active, the later ones planned", set(r) == {1, 5, 2, 3} and r[1]["StartedOn"] is not None
      and all(r[st]["StartedOn"] is None for st in (5, 2, 3)), r)
check("…with their day counts", r[5]["DueDays"] == 3 and r[2]["DueDays"] == 10 and r[3]["DueDays"] == 5 and r[5]["DueDate"] is None)
check("…success message", "alert-success" in message(soup)[0], message(soup))
check("…the box shows the planned people", "Ana Planner" in soup.select_one("#responsibilities tr[data-stage='5']").get_text()
      and "3 day" in soup.select_one("#responsibilities tr[data-stage='5']").get_text())
check("only the person at the current step is e-mailed now", [x["ToAddress"] for x in mails(A)] == [QU], [x["ToAddress"] for x in mails(A)])
check("audit trail: one 'Task Assigned' and three 'Step Planned' lines", len(audits(A, "Task Assigned")) == 1 and len(audits(A, "Step Planned")) == 3)
soup = plan(s, A, [(1, ids[QU], "2099-01-31", "start here"), (5, ids[QM1], 3, ""), (2, ids[QU2], 10, "fix it"), (3, ids[QM2], 5, "")])
check("saving the same plan again changes nothing", len(audits(A, "Step Planned")) == 3 and len(mails(A)) == 1 and "alert-info" in message(soup)[0], message(soup))

mine, _, coming, _ = home(QM1)
check("Coming Up lists the planned step for the planned person", ("Deviations", A) in coming and "Accept" in coming[("Deviations", A)].get_text())
check("…and not My Tasks (not ready yet)", ("Deviations", A) not in mine)
mine, _, _, _ = home(QU)
check("the person at the current step has it in My Tasks", ("Deviations", A) in mine)

nxt = json.loads(page(login(QU), A).select_one("#assign-next-data").get_text())
check("the signature dialog has the planned person chosen already", nxt["planned"].get("/Deviations/SubmitEvaluation") == ids[QM1], nxt.get("planned"))

# ── the record reaches the planned step ───────────────────────────────────────────────────────────────
sign(login(QU), m, A, "SubmitEvaluation", {}, QU)
r = rows(A)
expected_due = q1("select date(?, '+3 days') d", (TODAY,))["d"]
check("evaluation signed: the planned person takes over Accept", row(m["table"], A)["Status"] == 5 and r.get(5) and r[5]["StartedOn"] is not None
      and r[5]["AssignedToUserId"] == ids[QM1], r)
check("…due 3 days after it started", r.get(5) and str(r[5]["DueDate"]).startswith(expected_due), (r.get(5) or {}).get("DueDate"))
check("…the evaluation assignment ended (done)", 1 not in r)
last = mails(A)[-1]
check("…and the planned person gets the e-mail now ('ready for you now')", last["ToAddress"] == QM1 and "ready for you now" in last["HtmlBody"]
      and "Accept" in last["Subject"], last["Subject"])
check("…audit 'Task Started'", len(audits(A, "Task Started")) == 1)
C0 = new_dev()
s0 = login(QM2)
post_form(s0, "/Deviations/SubmitEvaluation", f"/Deviations/Details/{C0}", {"id": C0, "email": QM2, "pwd": PW, "notes": "e2e"})
outs = [x.get_text(" ", strip=True) for x in page(admin, C0).select(".plan-step[data-stage='5'] .already-out")]
check("the dialog names who is left out because they already did an earlier step", outs == ["Not listed: Bea Planner, who already did “Evaluate”."], outs)
outs = [x.get_text(" ", strip=True) for x in page(admin, A).select(".plan-step[data-stage='5'] .already-out")]
check("…but not someone whose role could not do the step anyway (Carl, a Quality User, evaluated)", outs == [], outs)
mine, _, coming, _ = home(QM1)
check("…it moved from Coming Up to My Tasks", ("Deviations", A) in mine and ("Deviations", A) not in coming)

# ── sent back, and forward again ──────────────────────────────────────────────────────────────────────
sign(admin, m, A, "ReturnDeviation", {"reason": "e2e"}, ADMIN)          # Pending Acceptance → Evaluation
r = rows(A)
check("sent back: Accept is planned again for its person", row(m["table"], A)["Status"] == 1 and r.get(5) and r[5]["StartedOn"] is None
      and r[5]["AssignedToUserId"] == ids[QM1] and r[5]["DueDate"] is None, r.get(5))
check("…Evaluate goes back to the person who did it before", r.get(1) and r[1]["AssignedToUserId"] == ids[QU] and r[1]["StartedOn"] is not None, r.get(1))
check("…who is told it was sent back", mails(A)[-1]["ToAddress"] == QU and "sent back" in mails(A)[-1]["HtmlBody"])
check("…audit 'Task Assigned (step done before)'", len(audits(A, "Task Assigned (step done before)")) == 1)
sign(login(QU), m, A, "SubmitEvaluation", {}, QU)
r = rows(A)
check("forward again: the planned person takes over Accept again", r.get(5) and r[5]["StartedOn"] is not None and r[5]["AssignedToUserId"] == ids[QM1], r.get(5))

# ── the signature dialog choice wins over the plan ────────────────────────────────────────────────────
B = new_dev()
plan(admin, B, [(5, ids[QM1], 2, "")])
post_form(login(QU), "/Deviations/SubmitEvaluation", f"/Deviations/Details/{B}",
          {"id": B, "email": QU, "pwd": PW, "notes": "e2e", "assignNextTo": ids[QM2]})
r = rows(B)
ended = q1("select * from TaskAssignments where Module='Deviations' and RecordId=? and Stage=5 and AssignedToUserId=?", (B, ids[QM1]))
check("a different person chosen when signing takes the step", r.get(5) and r[5]["AssignedToUserId"] == ids[QM2] and r[5]["StartedOn"] is not None, r)
check("…the plan for the other person ends (Reassigned)", ended and not ended["IsCurrent"] and ended["EndReason"] == "Reassigned", ended)

# ── the four-eyes rule at start ───────────────────────────────────────────────────────────────────────
C = new_dev()
plan(admin, C, [(5, ids[QM1], None, "")])
s1 = login(QM1)
r = post_form(s1, "/Deviations/SubmitEvaluation", f"/Deviations/Details/{C}", {"id": C, "email": QM1, "pwd": PW, "notes": "e2e"})
soup = BeautifulSoup(get(s1, r.headers.get("Location", f"/Deviations/Details/{C}")).text, "html.parser")
t = q1("select * from TaskAssignments where Module='Deviations' and RecordId=? and Stage=5 order by Id desc", (C,))
check("the planned person who did the evaluation is not started for Accept (four-eyes)", row(m["table"], C)["Status"] == 5 and t
      and not t["IsCurrent"] and t["EndReason"] == "NotAllowed", t)
check("…the page says so", "alert-warning" in message(soup)[0], message(soup))
check("…the audit trail records it", len(audits(C, "Planned Step Not Started")) == 1)
_, wait, _, _ = home(QM2)
check("…and the step is in Available Tasks for others, with no one named", ("Deviations", C) in wait
      and "No one named" in wait[("Deviations", C)].select_one("td.assigned-now").get_text())

D = new_dev()
soup = plan(admin, D, [(1, ids[QM1], "", ""), (5, ids[QM1], None, "")])
cls, txt = message(soup)
check("one person for two steps the four-eyes rule keeps apart: refused, naming the person and both steps",
      "alert-danger" in cls and "Ana Planner" in txt and "Evaluate" in txt and "Accept" in txt and not rows(D), txt)
soup = plan(admin, D, [(1, ids[QM1], "", ""), (3, ids[QM1], None, "")])
check("…the same for Evaluate and Approve", "alert-danger" in message(soup)[0] and not rows(D), message(soup))
soup = plan(admin, D, [(1, ids[QM1], "", ""), (2, ids[QM1], None, "")])
check("…but Evaluate and Resolve by one person is fine (no rule between them)", "alert-success" in message(soup)[0] and len(rows(D)) == 2, message(soup))
R = new_record(m["table"], m["uniq"], m["prefix"], {**m["init"], "Status": 1, "IdentifiedBy": QM2})
soup = plan(admin, R, [(5, ids[QM2], None, ""), (3, ids[QM2], None, "")])
cls, txt = message(soup)
check("the raiser planned for Accept and Approve: refused (raiser may approve only if someone else accepted)",
      "alert-danger" in cls and "raised this record" in txt and not rows(R), txt)
soup = plan(admin, R, [(5, ids[QM1], None, ""), (3, ids[QM2], None, "")])
check("…the raiser for Approve with someone else accepting is fine", "alert-success" in message(soup)[0], message(soup))
four_eyes(False)
soup = plan(admin, R, [(1, ids[QM1], "", ""), (5, ids[QM1], None, "")])
check("with the four-eyes rule off, one person may be planned for both", "alert-success" in message(soup)[0], message(soup))
four_eyes(True)
plan(admin, D, [(1, 0, "", ""), (2, 0, None, "")])
soup = plan(admin, D, [(1, ids[QM1], "", ""), (5, ids[QM2], None, "")])

# ── removing people ───────────────────────────────────────────────────────────────────────────────────
n_un, n_rm = len(audits(D, "Task Unassigned")), len(audits(D, "Planned Step Removed"))
soup = plan(admin, D, [(1, 0, "", ""), (5, 0, None, "")])
check("choosing 'no one' ends both (Unassigned), with audit lines", not rows(D) and len(audits(D, "Task Unassigned")) == n_un + 1
      and len(audits(D, "Planned Step Removed")) == n_rm + 1)

# ── report and home dates ─────────────────────────────────────────────────────────────────────────────
rep = BeautifulSoup(get(admin, "/Reports/TaskAssignments").text, "html.parser")
links = [a["href"] for a in rep.select("a.assign-link")]
check("report: each row links to the record with the dialog opened", f"/Deviations/Details/{D}?plan=1#assignment-panel" in links, links[:3])
check("…the record page opened that way opens the dialog", page(admin, D, "?plan=1").select_one(".modal[data-open='1']") is not None)
check("…and not otherwise", page(admin, D).select_one(".modal[data-open='1']") is None)
E = new_dev()
plan(admin, E, [(5, ids[QM2], 2, ""), (3, ids[QM1], None, "")])
tr = BeautifulSoup(get(admin, "/Reports/TaskAssignments").text, "html.parser").select_one(f"#table-tasks tr[data-module='Deviations'][data-record='{E}']")
nxt_cell = tr.select_one("td.planned-next").get_text(" ", strip=True) if tr else ""
check("report: 'Planned next' shows who is planned for the later steps", "Accept: Bea Planner" in nxt_cell and "Approve: Ana Planner" in nxt_cell, nxt_cell)
_, _, coming, _ = home(QM2)
check("Coming Up: 'planned by' shows the planner's name", ("Deviations", E) in coming and "System" in coming[("Deviations", E)].get_text() or "Admin" in coming[("Deviations", E)].get_text(),
      coming[("Deviations", E)].get_text(" ", strip=True) if ("Deviations", E) in coming else "")
_, _, _, soup = home(QM2)
heads = [th.get_text(strip=True) for th in soup.select("#table-waiting th")]
check("Available Tasks shows a Due column", "Due" in heads, heads)
check("Active Items cards show Due", soup.get_text().count("Due") >= 4)

# ── the rules file: each "records" field is what that step really sets (workflow baseline) ──
import os
base = json.load(open(os.path.join(os.path.dirname(__file__), "baseline", "workflow_baseline.json"), encoding="utf-8"))
rules = json.load(open(os.path.join(os.path.dirname(__file__), "..", "..", "Resources", "Workflow", "workflow-rules.json"), encoding="utf-8"))
names = {"Deviations": "Deviations", "Complaints": "Complaints", "Capas": "CAPA", "Changes": "Change Control", "Audits": "Audits",
         "Risks": "Risk", "Maintenances": "Maintenance", "AppDocuments": "Documents", "Suppliers": "Suppliers"}
bad = []
for mod in rules["modules"]:
    walk = {(w["from"], w["action"]): w["changed"] for w in base[names[mod["module"]]]["walk"]}
    for st in mod["steps"]:
        if st.get("records") and (st["from"], st["action"]) in walk and st["records"] not in walk[(st["from"], st["action"])]:
            bad.append((mod["module"], st["action"], st["records"]))
    notby = {f for st in mod["steps"] if st.get("fourEyes") for f in st["fourEyes"]["notBy"] + [st["fourEyes"].get("raiserReview")] if f}
    for (frm, act), changed in walk.items():
        st = next((x for x in mod["steps"] if x["from"] == frm and x["action"] == act and x["kind"] == "task"), None)
        for f in changed:
            if st and f in notby and st.get("records") != f:
                bad.append((mod["module"], act, "sets " + f + " but 'records' is " + str(st.get("records"))))
check("rules: every 'records' field matches what the step sets, and every four-eyes field a step sets is declared", not bad, bad)

sys.exit(summary())
