"""Maintenance: administrative re-open of a reviewed record (fixed in 1.2.0 — 1.1.0 required the Approved state,
which the workflow never reaches, so a reviewed record could not be re-opened).

  * the Administrative Controls card is shown to an Administrator on a Reviewed record, not to a Quality Manager;
  * re-open moves the record back to Completed, clears the review and is in the audit trail with its reason;
  * the next occurrence that was scheduled automatically at review is cancelled (soft-deleted and audited), as
    Send Back to Completed does;
  * a record that is not reviewed cannot be re-opened.
"""
from workflow_lib import *

m = module("Maintenance")
four_eyes(False)          # the Administrator walks the record alone
admin = login()
rid = new_record(m["table"], m["uniq"], m["prefix"], m["init"])
for action, extra, _ in m["steps"]:
    sign(admin, m, rid, action, extra, ADMIN)
check("set-up: record reviewed", row(m["table"], rid)["Status"] == 3, row(m["table"], rid)["Status"])

# an automatically scheduled next occurrence, as review creates for recurring maintenance
child = copy_record(m["table"], m["uniq"], m["prefix"], rid)
exec_sql('update Maintenance set Status=0, ParentMaintenanceId=?, ReviewedBy=NULL, ReviewedOn=NULL, PerformedBy=NULL where Id=?', (rid, child))

page = get(admin, f"/Maintenances/Details/{rid}").text
check("Administrator sees Administrative Controls on a reviewed record", "adminReopenModal" in page)
qm = ensure_user("mr-qm@e2e.local", ["QualityManager"], "Reopen", "QM")
page = get(login("mr-qm@e2e.local"), f"/Maintenances/Details/{rid}").text
check("a Quality Manager does not", 'data-bs-target="#adminReopenModal"' not in page)

admin = login()
before = len(audit_rows(m, rid))
code, msg = sign(admin, m, rid, "AdminReopen", {"reason": "e2e correction after review"}, ADMIN)
r = row(m["table"], rid)
check("re-open: back to Completed", r["Status"] == 2, (r["Status"], msg))
check("re-open: review cleared", not r["ReviewedBy"] and not r["ReviewedOn"] and not r["RetainUntil"], r)
log = q("select Action, Notes, PerformedBy from MaintenanceAuditLog where ParentId=? order by Id", (rid,))[before:]
check("re-open: audited with its reason", [x["Action"] for x in log] == ["Administrative Reopen"]
      and log[0]["Notes"] == "e2e correction after review" and log[0]["PerformedBy"], log)
c = row(m["table"], child)
check("re-open: the automatically scheduled next occurrence is cancelled", c["IsDeleted"] == 1 and c["DeletedBy"], c)
clog = q("select Action, Notes from MaintenanceAuditLog where ParentId=?", (child,))
check("…and that is in its audit trail", any(x["Action"] == "Auto-cancelled" and "administratively reopened" in (x["Notes"] or "")
                                             for x in clog), clog)

code, msg = sign(admin, m, rid, "AdminReopen", {"reason": "again"}, ADMIN)
check("a record that is not reviewed cannot be re-opened", row(m["table"], rid)["Status"] == 2 and msg, msg)

four_eyes(True)
sys.exit(summary())
