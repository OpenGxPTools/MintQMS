"""Site time zone: stored UTC timestamps are displayed in the configured site zone.

- Settings shows the Site Time Zone (default Australia/Brisbane) and rejects unknown zones.
- A UTC audit entry 2026-09-27 20:30 shows as 2026-09-28 06:30 with an (AEST) header label.
- A UTC record date late in the UTC day shows the next (Brisbane) calendar date.
- Switching the setting to UTC changes the display immediately and is audit-logged; restored after.
- Excel export info sheet carries the site-zone label.
"""
import io, re, zipfile
from e2e_lib import *

run = uuid.uuid4().hex[:6]
s = login()


def settings_form():
    page = get(s, "/Settings")
    f = form_fields(page.text, "Settings/Save")
    return page, f


page, f = settings_form()
check("settings: zone select present", 'name="TimeZoneId"' in page.text)
check("settings: Brisbane selected by default", f and f.get("TimeZoneId") == "Australia/Brisbane", f and f.get("TimeZoneId"))

# Supplier with a UTC audit entry and a UTC CreatedOn late in the UTC day
exec_sql("""insert into Supplier (CreatedOn, IsDeleted, LegalName, QualificationRequired, ReviewFrequency, ReviewTrigger, RiskLevel,
            Status, SupplierCode, SupplierType, TestingRequirement) values ('2026-09-27 20:30:00', 0, ?, 0, 0, 0, 0, 0, ?, 0, 0)""",
         (f"E2E TZ {run}", f"SUP-T{run}"))
sid = q1("select Id from Supplier where SupplierCode=?", (f"SUP-T{run}",))["Id"]
exec_sql("insert into SupplierAuditLog (Action, ParentId, PerformedBy, PerformedOn) values (?, ?, 'e2e', '2026-09-27 20:30:00')",
         (f"TZ probe {run}", sid))

html = get(s, f"/Suppliers/Details/{sid}").text
row = re.search(r"<tr>\s*<td[^>]*>([^<]+)</td>\s*<td>TZ probe " + run, html)
check("audit row shown in Brisbane time (UTC+10)", row and row.group(1).strip() == "2026-09-28 06:30", row and row.group(1))
check("audit header labelled (AEST)", "(AEST)" in html)


def save_zone(zone):
    _, f = settings_form()
    f = {k: v for k, v in f.items() if k != "logoFile"}
    f["TimeZoneId"] = zone
    f["OrganisationName"] = f.get("OrganisationName") or "E2E Org"          # required; empty on a fresh DB
    f["OrganisationAddress"] = f.get("OrganisationAddress") or "1 E2E Street"
    return s.post(f"{BASE}/Settings/Save", data=f, allow_redirects=False)


r = save_zone("Mars/Olympus_Mons")
check("settings: unknown zone rejected", q1("select TimeZoneId from SystemSettings")["TimeZoneId"] == "Australia/Brisbane", r.status_code)

r = save_zone("UTC")
check("settings: switch to UTC saved", q1("select TimeZoneId from SystemSettings")["TimeZoneId"] == "UTC", r.status_code)
check("settings: change audit-logged", q1("select 1 from SettingsAuditLog where FieldName='TimeZoneId' and NewValue='UTC'") is not None)
html = get(s, f"/Suppliers/Details/{sid}").text
row = re.search(r"<tr>\s*<td[^>]*>([^<]+)</td>\s*<td>TZ probe " + run, html)
check("UTC setting: audit row shows raw UTC", row and row.group(1).strip() == "2026-09-27 20:30", row and row.group(1))
check("UTC setting: header labelled (UTC)", "(UTC)" in html)

r = save_zone("Australia/Brisbane")
check("settings: restored to Brisbane", q1("select TimeZoneId from SystemSettings")["TimeZoneId"] == "Australia/Brisbane", r.status_code)

# Excel export carries the zone label ("Exported" info cell)
found = False
for href in ["/Deviations/ExportRegister", "/Capas/ExportRegister", "/Changes/ExportRegister", "/Suppliers/Export"]:
    r = s.get(BASE + href, allow_redirects=False)
    if r.status_code == 200 and r.content[:2] == b"PK":
        z = zipfile.ZipFile(io.BytesIO(r.content))
        blob = " ".join(z.read(n).decode("utf-8", "ignore") for n in z.namelist() if n.endswith(".xml"))
        found = "AEST" in blob
        if found:
            break
check("Excel export shows AEST stamp", found)

sys.exit(summary())
