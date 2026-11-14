"""Country-neutral wording and settings (any site, any language).

  * adverse-event reporting deadlines come from System Settings (defaults 15 / 7 calendar days after Day 0);
    an existing case keeps its deadline when the settings change, and is recalculated only when its Day 0
    or seriousness changes (the change is in the case's audit trail);
  * screens show general wording ("Reporting Deadline", "Registration Number") — no Australian regulator
    names in the labels of the adverse-event, product, batch-release and licence pages;
  * the batch-release checklist (stored in English) is shown in the screen language.
"""
import re
from e2e_lib import *

run = uuid.uuid4().hex[:6]
NOW = "2026-09-01 00:00:00"
s = login()


def visible(html):
    """Page text without scripts, styles and attribute values (data typed by users is checked separately)."""
    html = re.sub(r"<(script|style)\b.*?</\1>", " ", html, flags=re.S)
    return re.sub(r"<[^>]+>", " ", html)


# ── settings: the two deadlines, with their defaults ─────────────────────────────────────────────────
page = get(s, "/Settings")
f = form_fields(page.text, "Settings/Save")
check("settings: serious AE deadline field (default 15)", f and f.get("SeriousAEReportingDays") == "15", f and f.get("SeriousAEReportingDays"))
check("settings: fatal AE deadline field (default 7)", f and f.get("FatalAEReportingDays") == "7", f and f.get("FatalAEReportingDays"))

# ── adverse event: deadline from settings ────────────────────────────────────────────────────────────
num = f"AE-RN-{run}"
exec_sql("""insert into AdverseEvent (AENumber, ReportDate, ReportType, ReportSource, Status, IsSerious, SeriousnessCriteria,
            IsFatalOrLifeThreatening, IsLocked, AEDescription, MedicalNarrative, SignalDetected, CreatedBy, CreatedOn, Day0Date)
            values (?, ?, 0, 0, 0, 1, 4, 0, 0, 'E2E rash', 'E2E narrative', 0, 'e2e', ?, '2026-10-01 00:00:00')""", (num, NOW, NOW))
ae = q1("select Id from AdverseEvent where AENumber=?", (num,))["Id"]
exec_sql("insert into AEReporter (AdverseEventId, ReporterName, Type) values (?, 'E2E Reporter', 0)", (ae,))
exec_sql("insert into AEPatient (AdverseEventId, PatientIdentifier) values (?, 'E2E-P1')", (ae,))
exec_sql("insert into AESuspectProduct (AdverseEventId, ProductName) values (?, 'E2E Product')", (ae,))
deadline = lambda: (q1("select TGADeadlineDate d from AdverseEvent where Id=?", (ae,))["d"] or "")[:10]


def edit(**changes):
    pg = get(s, f"/Pharmacovigilance/Edit/{ae}")
    data = form_fields(pg.text, f"Pharmacovigilance/Edit/{ae}") or {}
    data.update(changes)
    return post_form(s, f"/Pharmacovigilance/Edit/{ae}", f"/Pharmacovigilance/Edit/{ae}", data)


edit()
check("serious case: deadline = Day 0 + 15 days", deadline() == "2026-10-16", deadline())

exec_sql("update SystemSettings set SeriousAEReportingDays=10, FatalAEReportingDays=5")
edit(AEDescription="E2E rash (edited)")
check("settings changed: an edit of other fields keeps the case's deadline", deadline() == "2026-10-16", deadline())

edit(Day0Date="2026-10-02")
check("Day 0 changed: deadline recalculated with the new setting (10 days)", deadline() == "2026-10-12", deadline())
aud = [r["Notes"] or "" for r in q("select Notes from AEAuditLog where AdverseEventId=?", (ae,))]
check("deadline change is in the case audit trail (English)", any("Reporting Deadline" in a for a in aud), aud[-3:])

edit(SeriousnessCriteria="1")   # Death
check("fatal case: deadline uses the fatal setting (5 days)", deadline() == "2026-10-07", deadline())

exec_sql("update SystemSettings set SeriousAEReportingDays=15, FatalAEReportingDays=7")

# ── general wording on screens ───────────────────────────────────────────────────────────────────────
html = get(s, f"/Pharmacovigilance/Details/{ae}").text
check("AE details: 'Reporting Deadline' label", shows(html, "Reporting Deadline:"))
check("AE details: no 'TGA' in the labels", "TGA" not in visible(html), re.findall(r"[^\n]{0,40}TGA[^\n]{0,40}", visible(html))[:2])
html = get(s, f"/Pharmacovigilance/Edit/{ae}").text
check("AE edit: no 'TGA' / 'ARTG' in the labels", not re.search(r"\b(TGA|ARTG|AEMS)\b", visible(html)), re.findall(r"[^\n]{0,40}(?:TGA|ARTG|AEMS)[^\n]{0,40}", visible(html))[:2])
html = get(s, "/Pharmacovigilance/Dashboard").text
check("PV dashboard: no 'TGA' in the labels", "TGA" not in visible(html))
html = get(s, "/Products/Create").text
check("product form: 'Registration Number' label", shows(html, "Registration Number"))
check("product form: no 'ARTG /' label", "ARTG /" not in visible(html) and "ARTG Entry" not in visible(html))
html = get(s, "/LicencesCertifications/Create").text
check("licence form: 'GMP Evidence Pathway' label", shows(html, "GMP Evidence Pathway"))
# (the licence-type and authority lists are pre-loaded data and do name the TGA; the labels must not)
opt = re.search(r'<option value="TGAInspection">([^<]*)', html)
check("licence form: pathway options in general wording", shows(html, "On-Site Inspection by the Authority")
      and opt and "TGA" not in opt.group(1) and "TGA framework" not in html and "TGA recognises" not in html, opt and opt.group(1))
html = get(s, "/Suppliers/Create").text
check("supplier form: 'Company Registration / Tax ID' label", shows(html, "Company Registration / Tax ID"))

# ── release: checklist shown in the screen language; general labels ──────────────────────────────────
exec_sql("""insert into ReleaseRecords (BatchNumber, CreatedDate, IsLocked, ManufacturingSite, ProductName, RecordNumber, ReleaseType, Status, Strength)
            values (?, ?, 0, 'Site', 'E2E Prod', ?, 1, 1, '5 mg')""", (f"B{run}", NOW, f"REL-RN-{run}"))
rid = q1("select Id from ReleaseRecords where RecordNumber=?", (f"REL-RN-{run}",))["Id"]
item = "Equipment qualification status confirmed current"
exec_sql("""insert into ReleaseChecklistItems (ReleaseRecordId, Category, ItemText, IsBlocking, IsRequired, DisplayOrder, Status)
            values (?, 'Manufacturing', ?, 0, 1, 1, 1)""", (rid, item))
html = get(s, f"/Release/Details/{rid}").text
check("release checklist item shown in the screen language", shows(html, item))
check("release checklist heading shown in the screen language", shows(html, "Manufacturing"))
check("checklist text stays stored in English", q1("select ItemText from ReleaseChecklistItems where ReleaseRecordId=?", (rid,))["ItemText"] == item)
check("release details: 'Registration Number' label", shows(html, "Registration Number"))
check("release details: no 'TGA' / 'ARTG' in the labels", not re.search(r"\b(TGA|ARTG)\b", visible(html)), re.findall(r"[^\n]{0,40}(?:TGA|ARTG)[^\n]{0,40}", visible(html))[:2])

sys.exit(summary())
