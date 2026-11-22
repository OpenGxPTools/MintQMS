"""Work lists built on task assignment (1.2.0 phase 3): home page tabs, register column and filter, reports.

  * Home → My Tasks: records assigned to me (overdue due date marked), plus trainings I attend;
  * Home → Available Tasks: records not assigned to me whose step my roles allow (with who is assigned now) — not for other roles,
    not for ReadOnly, not when the four-eyes rule excludes me (listed again with the rule off); a document's author
    for "submit for review"; supplier qualification only when required; finished and deleted records never; the
    regulatory items (e.g. licences with renewal due) stay there for Regulatory Managers;
  * Home → Raised by me: records I raised that are still waiting for a step (not finished ones);
  * the nine registers: an "Assigned to" column (my own rows marked for the "Assigned to me" filter), one cell per
    header in every row;
  * Reports → Task Assignments: by person, no one named, filters (module, person, no one named, overdue only), Excel
    export; only Administrator / Quality Manager / Authorised Person / Auditor;
  * Reports → Overdue Items: an "Assigned to" column.
"""
import io, re, zipfile
from workflow_lib import *

four_eyes(True)
QM1, QM2, QU, OPS, RO, AUD, TR = (f"wl-{n}@e2e.local" for n in ("qm1", "qm2", "qu", "ops", "ro", "aud", "tr"))
ids = {QM1: ensure_user(QM1, ["QualityManager"], "Wanda", "Manager"), QM2: ensure_user(QM2, ["QualityManager"], "Xavi", "Manager"),
       QU: ensure_user(QU, ["QualityUser"], "Yara", "User"), OPS: ensure_user(OPS, ["OperationsUser"], "Zed", "Ops"),
       RO: ensure_user(RO, ["ReadOnly"], "Rita", "Reader"), AUD: ensure_user(AUD, ["Auditor"], "Abe", "Auditor"),
       TR: ensure_user(TR, ["Trainer"], "Tess", "Author")}


def home(email):
    soup = BeautifulSoup(get(login(email), "/").text, "html.parser")
    def rows(pane):
        p = soup.select_one(pane)
        return {(r["data-module"], int(r["data-record"])): r for r in p.select("tr[data-module]")} if p else {}
    return rows("#pane-my-tasks"), rows("#pane-waiting"), rows("#pane-raised"), soup


def assign(uid, ctrl, rid, due=""):
    return post_form(login(), "/TaskAssignments/Assign", f"/{ctrl}/Details/{rid}",
                     {"module": ctrl, "recordId": rid, "userId": uid, "dueDate": due, "note": "wl"})


m = module("Deviations")
A = new_record(m["table"], m["uniq"], m["prefix"], {**m["init"], "Status": 1, "IdentifiedBy": QU})
key = ("Deviations", A)

# ── waiting for my roles ──────────────────────────────────────────────────────────────────────────────
_, wait, raised, soup = home(QU)
check("home: the old 'My Items' cards and managers' 'Pending' tables are gone",
      soup.select_one("#my-work") is not None and "Pending My Action" not in soup.get_text() and "My Open CAPAs" not in soup.get_text())
check("waiting: unassigned evaluation listed for a Quality User", key in wait)
check("…with its task in plain words", "Evaluate" in wait[key].get_text() if key in wait else False)
check("raised by me: the deviation the Quality User raised", key in raised)
check("waiting: listed for a Quality Manager too", key in home(QM1)[1])
check("waiting: not for an Operations user (role cannot do it)", key not in home(OPS)[1])
check("waiting: not for a ReadOnly user", key not in home(RO)[1])

# ── my tasks ──────────────────────────────────────────────────────────────────────────────────────────
assign(ids[QU], "Deviations", A, "2099-01-01")
exec_sql("update TaskAssignments set DueDate='2026-01-01 00:00:00' where Module='Deviations' and RecordId=? and IsCurrent=1", (A,))   # a due date that has since passed
mine, wait, _, soup = home(QU)
check("my tasks: the assigned record is listed for the assignee", key in mine)
check("…marked overdue (due date passed)", key in mine and "table-danger" in mine[key].get("class", []))
check("…and no longer in their 'waiting' list", key not in wait)
check("tab counts match the rows", soup.select_one("#count-my-tasks").get_text().strip() == str(len(mine)))
w_qm = home(QM1)[1]
check("an assigned record stays in the others' Available Tasks, showing who is assigned", key in w_qm
      and "User" in w_qm[key].select_one("td.assigned-now").get_text() and "No one" not in w_qm[key].select_one("td.assigned-now").get_text(), w_qm[key].get_text(" ", strip=True) if key in w_qm else "")
check("…and is not in another person's 'my tasks'", key not in home(QM1)[0])

# ── four-eyes in 'waiting' ────────────────────────────────────────────────────────────────────────────
B = new_record(m["table"], m["uniq"], m["prefix"], {**m["init"], "Status": 5, "EvaluatedBy": QM2, "IdentifiedBy": ADMIN})
check("four-eyes: acceptance waiting for another manager", ("Deviations", B) in home(QM1)[1])
check("four-eyes: not for the manager who evaluated it", ("Deviations", B) not in home(QM2)[1])
check("…and not for a Quality User (acceptance needs a manager)", ("Deviations", B) not in home(QU)[1])
four_eyes(False)
check("with the rule off, the evaluator sees it waiting", ("Deviations", B) in home(QM2)[1])
four_eyes(True)

# ── finished and deleted records never; documents; suppliers ──────────────────────────────────────────
C = new_record(m["table"], m["uniq"], m["prefix"], {**m["init"], "Status": 4, "IdentifiedBy": QU})
D = new_record(m["table"], m["uniq"], m["prefix"], {**m["init"], "Status": 1, "IdentifiedBy": QU, "IsDeleted": 1})
_, wait, raised, _ = home(QU)
check("a finished record is not in 'raised by me'", ("Deviations", C) not in raised)
check("a deleted record is in no list", ("Deviations", D) not in wait and ("Deviations", D) not in raised)
dm = module("Documents")
doc = new_record(dm["table"], dm["uniq"], dm["prefix"], {**dm["init"], "AuthoredBy": TR})
check("document draft: waiting for its author (Trainer role)", ("AppDocuments", doc) in home(TR)[1])
check("…not for a Quality Manager who is not the author", ("AppDocuments", doc) not in home(QM1)[1])
sq, sn = module("Suppliers (qualification)"), module("Suppliers")
s_q = new_record(sq["table"], sq["uniq"], sq["prefix"], {**sq["init"], "Status": 4, "ApprovalStatus": 4, "AssessedBy": QM2})
s_n = new_record(sn["table"], sn["uniq"], sn["prefix"], {**sn["init"], "Status": 4, "ApprovalStatus": 4, "AssessedBy": QM2})
_, wait, _, _ = home(QM1)
check("approved supplier needing qualification: waiting", ("Suppliers", s_q) in wait)
check("approved supplier without qualification: not waiting", ("Suppliers", s_n) not in wait)

# ── trainings I attend are my tasks ───────────────────────────────────────────────────────────────────
tid = new_record("Training", "TrainingId", "TRN-WL", {"Status": 0, "DueDate": FUTURE})
new_record("UserTraining", "Id", "", {"TrainingId": tid, "TraineeId": ids[QU], "Status": 0})
check("my tasks: a scheduled training I attend", ("Trainings", tid) in home(QU)[0])

# ── regulatory items stay for Regulatory Managers ─────────────────────────────────────────────────────
lt = q1("select Id from LCLicenceTypes order by Id limit 1")
if lt:
    lc = new_record("LicenceCertificationRecords", "LCNumber", "LC-WL", {"Status": 6, "LicenceTypeId": lt["Id"], "IsLocked": 0, "IsDeleted": 0})
    _, _, _, soup = home(ADMIN)
    num = row("LicenceCertificationRecords", lc)["LCNumber"]
    check("regulatory: licence renewal due listed under 'waiting' for an Administrator / Regulatory Manager",
          num in soup.select_one("#pane-waiting").get_text())
    _, _, _, soup = home(QM1)
    check("…not in a Quality Manager's work lists", num not in soup.select_one("#my-work").get_text())

# ── registers: column, my marker, one cell per header ─────────────────────────────────────────────────
for ctrl in ("Deviations", "Complaints", "Capas", "Changes", "Audits", "Risks", "Maintenances", "AppDocuments", "Suppliers"):
    html = get(login(QU if ctrl != "Maintenances" else OPS), f"/{ctrl}").text
    soup = BeautifulSoup(html, "html.parser")
    heads = [th.get_text(strip=True) for th in soup.select("table thead tr th")]
    rows_ok = all(len(tr.find_all("td", recursive=False)) == len(heads) for tr in soup.select("table tbody tr")
                  if tr.find_all("td", recursive=False))
    check(f"register {ctrl}: 'Assigned to' column and 'Assigned to me' filter",
          "Assigned to" in heads and soup.select_one("#assigned-to-me-filter") is not None, heads)
    check(f"register {ctrl}: every row has one cell per header", rows_ok)
    # The DataTables column list must have one entry per header, or the browser shows "Incorrect column count" and the
    # register stops working (found by a browser check on CAPA and Documents).
    cols = re.search(r'"?columns"?\s*:\s*\[(.*?)\]\s*,\s*\n', html, re.S)
    if cols:
        body = re.sub(r"//[^\n]*", "", cols.group(1))
        n_cols = len(re.findall(r"\bnull\b", body)) + body.count("{")
        check(f"register {ctrl}: DataTables column list matches the headers ({len(heads)})", n_cols == len(heads), n_cols)


def assigned_cell(email, rid):
    soup = BeautifulSoup(get(login(email), "/Deviations").text, "html.parser")
    for tr in soup.select("table tbody tr"):
        a = tr.select_one('a[href$="/Deviations/Details/%d"]' % rid)
        if a:
            return tr.select_one("td.assigned-to")
    return None


cell = assigned_cell(QU, A)
check("register: the assignee's name, and the 'mine' marker for the assignee", cell is not None and "Yara User" in cell.get_text()
      and "~assigned-to-me~" in cell.get("data-search", ""), cell)
cell = assigned_cell(QM1, A)
check("register: no 'mine' marker for someone else", cell is not None and "Yara User" in cell.get_text()
      and "~assigned-to-me~" not in cell.get("data-search", ""))

# ── Task Assignments report ───────────────────────────────────────────────────────────────────────────
def report(email, qs=""):
    r = get(login(email), "/Reports/TaskAssignments" + qs)
    soup = BeautifulSoup(r.text, "html.parser")
    rows = {(tr["data-module"], int(tr["data-record"])) for tr in soup.select("#table-tasks tr[data-module]")}
    return r, rows, soup


r, rows, soup = report(QM1)
check("report: open for a Quality Manager, lists the assigned record", r.status_code == 200 and key in rows)
check("report: by-person table lists the assignee", any(tr.get("data-person") == str(ids[QU]) for tr in soup.select("#table-by-person tr")))
check("report: unassigned waiting records are listed too", ("Deviations", B) in rows)
check("report: filter by person", key in report(QM1, f"?person={ids[QU]}")[1] and ("Deviations", B) not in report(QM1, f"?person={ids[QU]}")[1])
check("report: filter 'no one named'", ("Deviations", B) in report(QM1, "?person=0")[1] and key not in report(QM1, "?person=0")[1])
check("report: filter overdue only", key in report(QM1, "?overdue=true")[1] and ("Deviations", B) not in report(QM1, "?overdue=true")[1])
check("report: filter by module", key not in report(QM1, "?module=Complaints")[1])
check("report: open for an Auditor", report(AUD)[0].status_code == 200)
r = get(login(QU), "/Reports/TaskAssignments")
check("report: refused for a Quality User", r.status_code in (302, 403) or "Access denied" in r.text or "AccessDenied" in r.url, r.status_code)
check("reports hub: card only for the allowed roles",
      BeautifulSoup(get(login(QM1), "/Reports").text, "html.parser").select_one("#task-assignments-card") is not None
      and BeautifulSoup(get(login(QU), "/Reports").text, "html.parser").select_one("#task-assignments-card") is None)
x = get(login(QM1), f"/Reports/ExportTaskAssignments?person={ids[QU]}")
strings = ""
try:
    strings = zipfile.ZipFile(io.BytesIO(x.content)).read("xl/sharedStrings.xml").decode("utf-8")
except Exception:
    pass
num = row(m["table"], A)["DeviationId"]
check("report: Excel export with the filtered rows", x.status_code == 200 and "spreadsheetml" in x.headers.get("Content-Type", "")
      and num in strings and row(m["table"], B)["DeviationId"] not in strings)

# ── Overdue Items: Assigned to column ─────────────────────────────────────────────────────────────────
cm = module("CAPA")
cp = new_record(cm["table"], cm["uniq"], cm["prefix"], {**cm["init"], "Status": 0, "TargetCompletionDate": "2026-01-15 00:00:00"})
assign(ids[QU], "Capas", cp)
soup = BeautifulSoup(get(login(QM1), "/Reports/OverdueItems").text, "html.parser")
capa_no = row("Capa", cp)["CapaId"]
tr = next((t for t in soup.select("table tbody tr") if capa_no in t.get_text()), None)
check("overdue items: the overdue CAPA shows who is assigned", tr is not None and "Yara User" in tr.get_text(), tr)

four_eyes(True)
sys.exit(summary())
