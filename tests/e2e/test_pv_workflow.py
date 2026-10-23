"""Pharmacovigilance adverse-event workflow: the signed steps, including Reject.

Reject (Pending Approval -> Under Medical Review) used to be refused by the workflow rule that only allows
"submit for review" from Draft. A case is created with the four minimum ICSR elements and taken through
Submit -> Complete Evaluation -> Reject -> Complete Evaluation -> Approve -> Close.
"""
from e2e_lib import *

run = uuid.uuid4().hex[:6]
NOW = "2026-09-01 00:00:00"
num = f"AE-E2E-{run}"

exec_sql("""insert into AdverseEvent (AENumber, ReportDate, ReportType, ReportSource, Status, IsSerious,
            SeriousnessCriteria, IsFatalOrLifeThreatening, IsLocked, AEDescription, MedicalNarrative,
            SignalDetected, CreatedBy, CreatedOn)
            values (?, ?, 0, 0, 0, 0, 0, 0, 0, 'E2E rash after dose', 'E2E narrative', 0, 'e2e', ?)""",
         (num, NOW, NOW))
ae = q1("select Id from AdverseEvent where AENumber=?", (num,))["Id"]
exec_sql("insert into AEReporter (AdverseEventId, ReporterName, Type) values (?, 'E2E Reporter', 0)", (ae,))
exec_sql("insert into AEPatient (AdverseEventId, PatientIdentifier) values (?, 'E2E-P1')", (ae,))
exec_sql("insert into AESuspectProduct (AdverseEventId, ProductName) values (?, 'E2E Product')", (ae,))

s = login()
status = lambda: q1("select Status from AdverseEvent where Id=?", (ae,))["Status"]
names = {0: "Draft", 1: "UnderMedicalReview", 2: "PendingApproval", 3: "Approved", 4: "Closed", 5: "Voided"}


def step(action, expected, extra=None):
    data = {"email": ADMIN, "pwd": PW}
    data.update(extra or {})
    post_form(s, f"/Pharmacovigilance/{action}/{ae}", f"/Pharmacovigilance/Details/{ae}", data)
    got = names.get(status())
    check(f"{action} -> {expected}", got == expected, got)


step("SubmitForReview", "UnderMedicalReview")
step("CompleteEvaluation", "PendingApproval")
step("Reject", "UnderMedicalReview", {"comment": "E2E: please revise causality"})
step("CompleteEvaluation", "PendingApproval")
step("Approve", "Approved")
check("approved case is locked", q1("select IsLocked from AdverseEvent where Id=?", (ae,))["IsLocked"] == 1)
step("CloseCase", "Closed")

# wrong password is refused and changes nothing
exec_sql("update AdverseEvent set Status=2, IsLocked=0 where Id=?", (ae,))
post_form(s, f"/Pharmacovigilance/Reject/{ae}", f"/Pharmacovigilance/Details/{ae}",
          {"email": ADMIN, "pwd": "wrong-password", "comment": "x"})
check("Reject with a wrong password is refused", names.get(status()) == "PendingApproval", names.get(status()))

summary()
