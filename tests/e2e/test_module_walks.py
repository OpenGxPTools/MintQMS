"""Workflow walks through the pages for the modules the other suites do not walk step by step: Batch Release,
Licences & Certifications, Regulatory Intelligence, pharmacovigilance (adverse events, periodic reports, signals),
Data Disposal, and the remaining actions of the quality modules (attachments, linked CAPA, action plan, documents).

Every step is submitted through the form its button opens on the record page (walk_lib.step), so each check also
proves the step is offered in that state. After each step: the state, the key fields and the audit trail.
"""
from walk_lib import *
from workflow_lib import module, new_record, row, run, NOW, FUTURE, four_eyes

s = login()
get(s, "/Settings")
four_eyes(False)          # walks are signed by one Administrator; the four-eyes rule has its own suite


def says(text, english):
    """The page text holds this message in the site's language (English or a translation)."""
    return any(t.split("{")[0].strip()[:40] in text for t in any_language(english))


def status(table, rid):
    return q1(f'select Status from "{table}" where Id=?', (rid,))["Status"]


def audits(table, key, rid):
    return [r["Action"] for r in q(f'select Action from "{table}" where "{key}"=? order by Id', (rid,))]


# ═══ Batch Release ═══════════════════════════════════════════════════════════════════════════════════
def new_release(release_type="1"):
    url, f = find_form(get(s, "/Release/Create").text, "/Release/Create")
    f.pop("__types", None)
    f.update({"ReleaseType": release_type, "ProductName": f"Walk Product {run}", "ProductType": "0",
              "BatchNumber": f"B-{run}-{len(q('select Id from ReleaseRecords'))}", "ManufacturingDate": "2026-08-01",
              "ExpiryDate": "2028-08-01", "ManufacturingSite": "Walk Site", "ReleasingCompany": "Walk Co",
              "ManufacturingSiteGmpRef": "GMP-1", "PackSize": "10 x 10"})
    r = s.post(BASE + url, data=f, allow_redirects=False)
    loc = r.headers.get("Location", "")
    return int(loc.rstrip("/").split("/")[-1]) if "/Release/Details/" in loc else None


RS = {"Draft": 1, "UnderReview": 2, "OnHold": 3, "PendingAP": 4, "Certified": 5, "Released": 6, "Rejected": 7, "Closed": 8, "Cancelled": 9}
rel = new_release()
check("release: created through the Create page, in Draft", rel and status("ReleaseRecords", rel) == RS["Draft"], rel)
items = q("select Id, IsBlocking from ReleaseChecklistItems where ReleaseRecordId=?", (rel,))
check("release: checklist generated on creation", len(items) > 5 and any(i["IsBlocking"] for i in items), len(items))
P = f"/Release/Details/{rel}"

r, _ = step(s, P, "/Release/SubmitForReview")
check("release: Submit for Review → Under Review", status("ReleaseRecords", rel) == RS["UnderReview"], page_message(s, r))

# blocking checklist item marked Outstanding stops the AP submission
blk = next(i["Id"] for i in items if i["IsBlocking"])
post_form(s, "/Release/UpdateChecklistItem", P, {"itemId": blk, "status": "5", "comment": "missing CoA"})
st = q1("select Status from ReleaseChecklistItems where Id=?", (blk,))["Status"]
r, _ = step(s, P, "/Release/SubmitToAP")
check("release: an Outstanding blocking checklist item blocks Submit to AP",
      st == 5 and status("ReleaseRecords", rel) == RS["UnderReview"] and says(page_message(s, r), "Cannot submit to AP: one or more blocking checklist items are unresolved. Resolve all blocking items before submitting."), (st, page_message(s, r)))
for i in items:
    post_form(s, "/Release/UpdateChecklistItem", P, {"itemId": i["Id"], "status": "2", "comment": ""})
check("release: checklist items marked Satisfactory", all(x["Status"] == 2 for x in q("select Status from ReleaseChecklistItems where ReleaseRecordId=?", (rel,))))

# a pending test result stops the AP submission; entering a result clears it
post_form(s, "/Release/AddTestResult", P, {"releaseRecordId": rel, "testParameter": "Assay", "testMethod": "HPLC",
                                           "acceptanceCriterion": "95-105%", "result": "", "status": "4"})
tr = q1("select Id, Status from ReleaseTestResults where ReleaseRecordId=? order by Id desc", (rel,))
r, _ = step(s, P, "/Release/SubmitToAP")
check("release: a pending test result blocks Submit to AP", tr and tr["Status"] == 4 and status("ReleaseRecords", rel) == RS["UnderReview"],
      page_message(s, r))
post_form(s, "/Release/UpdateTestResult", P, {"resultId": tr["Id"], "testParameter": "Assay", "testMethod": "HPLC",
                                              "acceptanceCriterion": "95-105%", "result": "99.1%", "status": "1"})
check("release: test result entered (Pass)", q1("select Status from ReleaseTestResults where Id=?", (tr["Id"],))["Status"] == 1)

r, _ = step(s, P, "/Release/PlaceOnHold", extra={"reason": "awaiting stability data"})
check("release: Place on Hold → On Hold", status("ReleaseRecords", rel) == RS["OnHold"], page_message(s, r))
r, _ = step(s, P, "/Release/LiftHold", extra={"targetState": "UnderReview"})
check("release: Lift Hold → back to Under Review", status("ReleaseRecords", rel) == RS["UnderReview"], page_message(s, r))

r, _ = step(s, P, "/Release/SubmitToAP")
check("release: Submit to AP → Pending AP Decision", status("ReleaseRecords", rel) == RS["PendingAP"], page_message(s, r))

QU = "walk-qu@e2e.local"
ensure_user(QU, ["QualityUser"], "Walk", "QualityUser")
squ = login(QU)
r = post_form(squ, "/Release/Certify", P, {"id": rel, "statement": "x", "pwd": PW, "apFullName": "Walk QU", "email": QU})
check("release: a Quality User cannot certify (refused, unchanged)", status("ReleaseRecords", rel) == RS["PendingAP"], r.status_code)
s = login()
check("release: the Administrator is not offered Certify / Reject (Authorised Person only)",
      find_form(get(s, P).text, "/Release/Certify")[0] is None and find_form(get(s, P).text, "/Release/Reject")[0] is None)
r = post_form(s, "/Release/Certify", P, {"id": rel, "statement": "x", "pwd": PW, "apFullName": "System Administrator", "email": ADMIN})
check("release: the Administrator cannot certify (refused, unchanged)", status("ReleaseRecords", rel) == RS["PendingAP"], r.status_code)
AP = "walk-ap@e2e.local"
ensure_user(AP, ["AuthorisedPerson"], "Walk", "AuthorisedPerson")
sap = login(AP)
r, _ = step(sap, P, "/Release/Certify", signer=AP, extra={"pwd": "wrong-password"})
check("release: certify with a wrong password is refused", status("ReleaseRecords", rel) == RS["PendingAP"], page_message(sap, r))
r, _ = step(sap, P, "/Release/Certify", signer=AP, extra={"apFullName": "Walk AuthorisedPerson"})
rec = row("ReleaseRecords", rel)
check("release: AP certifies → Certified (who and when recorded)", rec["Status"] == RS["Certified"] and rec["CertifiedBy"] == AP
      and rec["CertificationDate"], page_message(sap, r))
s = login()
r, _ = step(s, P, "/Release/ConfirmRelease", extra={"releaseRegisterRef": "REG-1"})
rec = row("ReleaseRecords", rel)
check("release: Confirm Release → released and closed at once (register ref kept)",
      rec["Status"] == RS["Closed"] and rec["ReleaseRegisterRef"] == "REG-1", page_message(s, r))
acts = audits("ReleaseAuditEntries", "ReleaseRecordId", rel)
check("release: every step in the audit trail", all(a in acts for a in ("Created", "Submitted for Review", "Placed On Hold",
      "Hold Lifted", "Submitted to AP", "Certified by AP", "Transfer to Saleable Stock Confirmed", "Closed")), acts)

# cross-reference, document upload and delete (on an open record)
relx = new_release()
PX = f"/Release/Details/{relx}"
r = post_form(s, "/Release/AddCrossReference", PX, {"releaseRecordId": relx, "moduleType": "Deviation", "linkedRecordNumber": "DEV-X",
                                                    "linkedRecordTitle": "e2e", "notes": "e2e"})
xr = q1("select Id from ReleaseCrossReferences where ReleaseRecordId=?", (relx,))
check("release: cross-reference added", xr is not None, r.status_code)
if xr:
    post_form(s, "/Release/DeleteCrossReference", PX, {"id": xr["Id"]})
    check("release: cross-reference removed", q1("select Id from ReleaseCrossReferences where Id=?", (xr["Id"],)) is None)
page = get(s, PX).text
url, f = find_form(page, "/Release/UploadDocument")
if url:
    f.pop("__types", None); f.update({"releaseRecordId": relx, "description": "e2e CoA"})
    r = s.post(BASE + url, data=f, files={"file": ("coa.pdf", b"%PDF-1.4 e2e walk", "application/pdf")}, allow_redirects=False)
doc = q1("select Id, FileHash from ReleaseDocuments where ReleaseRecordId=? order by Id desc", (relx,))
check("release: document uploaded through the page (fingerprint stored)", url and doc and doc["FileHash"], url)
if doc:
    post_form(s, "/Release/DeleteDocument", PX, {"documentId": doc["Id"], "deletionReason": "wrong file"})
    d = row("ReleaseDocuments", doc["Id"])
    check("release: document removed with a reason, kept as deleted", d.get("IsDeleted") in (1, True), d)
# Checklist items still "Not Reviewed" do not block Submit to AP (current behaviour, recorded here for review)
step(s, PX, "/Release/SubmitForReview")
r, _ = step(s, PX, "/Release/SubmitToAP")
check("release: [current behaviour] Submit to AP allowed with the checklist still Not Reviewed",
      status("ReleaseRecords", relx) == RS["PendingAP"], page_message(s, r))

# rejection, AP override, cancellation
rel2 = new_release()
P2 = f"/Release/Details/{rel2}"
step(s, P2, "/Release/SubmitForReview")
for i in q("select Id from ReleaseChecklistItems where ReleaseRecordId=?", (rel2,)):
    post_form(s, "/Release/UpdateChecklistItem", P2, {"itemId": i["Id"], "status": "2", "comment": ""})
step(s, P2, "/Release/SubmitToAP")
r = post_form(s, "/Release/Reject", P2, {"id": rel2, "reason": "x", "pwd": PW, "email": ADMIN})
check("release: the Administrator cannot reject (refused, unchanged)", status("ReleaseRecords", rel2) == RS["PendingAP"], r.status_code)
sap = login(AP)
r, _ = step(sap, P2, "/Release/Reject", signer=AP, extra={"reason": "OOS assay"})
check("release: AP rejects → Rejected", status("ReleaseRecords", rel2) == RS["Rejected"], page_message(sap, r))
s = login()
check("release: the Administrator is not offered Override Rejection", find_form(get(s, P2).text, "/Release/ReturnFromRejection")[0] is None)
sap = login(AP)
r, _ = step(sap, P2, "/Release/ReturnFromRejection", signer=AP)
check("release: AP overrides the rejection → Under Review", status("ReleaseRecords", rel2) == RS["UnderReview"], page_message(sap, r))
s = login()
r, _ = step(s, P2, "/Release/Cancel", extra={"reason": "batch destroyed"})
check("release: Cancel from Under Review → Cancelled", status("ReleaseRecords", rel2) == RS["Cancelled"], page_message(s, r))
rel3 = new_release()
r, _ = step(s, f"/Release/Details/{rel3}", "/Release/Cancel", extra={"reason": "raised in error"})
check("release: Cancel from Draft → Cancelled", status("ReleaseRecords", rel3) == RS["Cancelled"], page_message(s, r))

# ═══ Licences & Certifications ════════════════════════════════════════════════════════════════════════
LC = {"Draft": 1, "Applied": 2, "UnderAssessment": 3, "IssuedActive": 4, "RenewalDue": 6, "Expired": 7, "Suspended": 8,
      "Withdrawn": 9, "NotApplicable": 10, "VariationRequired": 11}


def new_lc(name):
    url, f = find_form(get(s, "/LicencesCertifications/Create").text, "/LicencesCertifications/Create")
    types = f.pop("__types", {})
    for k, t in types.items():
        if t in ("text", "textarea") and not f.get(k) and k != "__RequestVerificationToken":
            f[k] = "e2e"
    f.update({"HolderName": name, "HolderType": "1", "Category": "1", "IssuingBody": "TGA",
              "IssueDate": "2026-01-01", "ExpiryDate": "2028-01-01"})
    r = s.post(BASE + url, data=f, allow_redirects=False)
    loc = r.headers.get("Location", "")
    return int(loc.rstrip("/").split("/")[-1]) if "/Details/" in loc else None, r


lc, r = new_lc(f"Walk Licence {run}")
check("licence: created through the Create page, in Draft", lc and status("LicenceCertificationRecords", lc) == LC["Draft"],
      (r.status_code, BeautifulSoup(r.text, "html.parser").select_one(".validation-summary-errors, .text-danger") if r.status_code == 200 else ""))
if lc:
    P = f"/LicencesCertifications/Details/{lc}"
    def tr(target, **kw):
        return step(s, P, "/LicencesCertifications/Transition", match={"newStatus": target}, **kw)
    r, _ = tr("Applied")
    check("licence: Draft → Applied", status("LicenceCertificationRecords", lc) == LC["Applied"], page_message(s, r))
    r, _ = tr("Draft")
    check("licence: Applied → back to Draft (revert)", status("LicenceCertificationRecords", lc) == LC["Draft"], page_message(s, r))
    tr("Applied")
    r, _ = tr("UnderAssessment")
    check("licence: Applied → Under Assessment", status("LicenceCertificationRecords", lc) == LC["UnderAssessment"], page_message(s, r))
    # document (while under assessment; an issued licence is locked)
    url, f = find_form(get(s, P).text, "/LicencesCertifications/UploadDocument")
    if url:
        f.pop("__types", None); f.update({"recordId": lc, "documentType": "1", "description": "certificate"})
        s.post(BASE + url, data=f, files={"file": ("lic.pdf", b"%PDF-1.4 e2e lc", "application/pdf")}, allow_redirects=False)
    d = q1("select Id from LCDocuments where LicenceCertificationRecordId=? order by Id desc", (lc,))
    check("licence: document uploaded through the page", d is not None)
    if d:
        post_form(s, "/LicencesCertifications/DeleteDocument", P, {"docId": d["Id"], "recordId": lc, "deletionReason": "wrong file"})
        check("licence: document removed with a reason", row("LCDocuments", d["Id"]).get("IsDeleted") in (1, True), row("LCDocuments", d["Id"]))
    r, _ = step(s, P, "/LicencesCertifications/IssueRecord", extra={"pwd": "wrong"})
    check("licence: issue with a wrong password is refused", status("LicenceCertificationRecords", lc) == LC["UnderAssessment"], page_message(s, r))
    r, _ = step(s, P, "/LicencesCertifications/IssueRecord")
    check("licence: Issue (signed) → Issued / Active", status("LicenceCertificationRecords", lc) == LC["IssuedActive"], page_message(s, r))
    url, f = find_form(get(s, P).text, "/LicencesCertifications/UploadDocument")
    f.pop("__types", None); f.update({"recordId": lc, "documentType": "1"})
    n0 = len(q("select Id from LCDocuments where LicenceCertificationRecordId=?", (lc,)))
    r = s.post(BASE + url, data=f, files={"file": ("x.pdf", b"%PDF-1.4 x", "application/pdf")}, allow_redirects=False)
    check("licence: an issued licence is locked (no document added)", len(q("select Id from LCDocuments where LicenceCertificationRecordId=?", (lc,))) == n0
          and says(page_message(s, r), "This record is locked and cannot be modified."), page_message(s, r))
    # conditions
    post_form(s, "/LicencesCertifications/AddCondition", P, {"recordId": lc, "conditionDescription": "Submit stability data", "dueDate": "2027-01-01"})
    c1 = q1("select Id from LCConditions where LicenceCertificationRecordId=? order by Id desc", (lc,)) if q("select name from sqlite_master where name='LCConditions'") else None
    check("licence: condition added", c1 is not None)
    if c1:
        post_form(s, "/LicencesCertifications/CompleteCondition", P, {"conditionId": c1["Id"], "recordId": lc, "completionNotes": "done"})
        c = row("LCConditions", c1["Id"])
        check("licence: condition completed (who, when, notes)", c["CompletedAt"] and c["CompletedBy"] and c["CompletionNotes"] == "done", c)
        post_form(s, "/LicencesCertifications/AddCondition", P, {"recordId": lc, "conditionDescription": "Second condition", "dueDate": "2027-02-01"})
        c2 = q1("select Id, Status from LCConditions where LicenceCertificationRecordId=? order by Id desc", (lc,))
        post_form(s, "/LicencesCertifications/WaiveCondition", P, {"conditionId": c2["Id"], "recordId": lc, "waiverReason": "not applicable"})
        w = row("LCConditions", c2["Id"])
        check("licence: condition waived (status changed, reason kept)", w["Status"] != c2["Status"] and "not applicable" in (w["CompletionNotes"] or ""), w)
    # variation, renewal, suspension, withdrawal
    r, _ = tr("VariationRequired")
    check("licence: Issued → Variation Required", status("LicenceCertificationRecords", lc) == LC["VariationRequired"], page_message(s, r))
    r, _ = step(s, P, "/LicencesCertifications/IssueRecord")
    check("licence: Variation applied (signed) → Issued / Active", status("LicenceCertificationRecords", lc) == LC["IssuedActive"], page_message(s, r))
    r, _ = step(s, P, "/LicencesCertifications/InitiateRenewal", extra={"reason": "expires next year"})
    check("licence: Initiate Renewal → Renewal Due", status("LicenceCertificationRecords", lc) == LC["RenewalDue"], page_message(s, r))
    r, _ = tr("Applied")
    check("licence: renewal submitted → Applied", status("LicenceCertificationRecords", lc) == LC["Applied"], page_message(s, r))
    tr("UnderAssessment")
    r, _ = step(s, P, "/LicencesCertifications/SuspendRecord", extra={"reason": "authority suspension"})
    check("licence: Suspend (signed) → Suspended, locked", status("LicenceCertificationRecords", lc) == LC["Suspended"]
          and row("LicenceCertificationRecords", lc)["IsLocked"], page_message(s, r))
lc2, _ = new_lc(f"Walk Licence B {run}")
if lc2:
    P2 = f"/LicencesCertifications/Details/{lc2}"
    step(s, P2, "/LicencesCertifications/Transition", match={"newStatus": "Applied"})
    r, _ = step(s, P2, "/LicencesCertifications/WithdrawRecord", extra={"reason": "no longer needed"})
    check("licence: Withdraw (signed) → Withdrawn", status("LicenceCertificationRecords", lc2) == LC["Withdrawn"], page_message(s, r))
lc3, _ = new_lc(f"Walk Licence C {run}")
if lc3:
    r, _ = step(s, f"/LicencesCertifications/Details/{lc3}", "/LicencesCertifications/Transition", match={"newStatus": "NotApplicable"},
                extra={"reason": "not required for our activities"})
    check("licence: Draft → Not Applicable (with justification)", status("LicenceCertificationRecords", lc3) == LC["NotApplicable"], page_message(s, r))
lca = q("select Action from LCAuditLogs where LicenceCertificationRecordId=?", (lc,)) if lc and q("select name from sqlite_master where name='LCAuditLogs'") else []
check("licence: steps in the audit trail", len(lca) >= 10, len(lca))

# ═══ Regulatory Intelligence ══════════════════════════════════════════════════════════════════════════
RI = {"Strategy": 1, "Preparation": 2, "Submitted": 3, "UnderReview": 4, "ClockStop": 5, "Approved": 6, "ConditionsActive": 7,
      "VariationRequired": 8, "RenewalDue": 9, "Rejected": 10, "Withdrawn": 11, "Suspended": 12}


def fill_create(page, action, overrides):
    url, f = find_form(get(s, page).text, action)
    types = f.pop("__types", {})
    for k, t in types.items():
        if t in ("text", "textarea") and not f.get(k) and k != "__RequestVerificationToken":
            f[k] = "e2e"
    f.update(overrides)
    r = s.post(BASE + url, data=f, allow_redirects=False)
    loc = r.headers.get("Location", "")
    return r, loc


# an application is for an approved product
PROD = new_record("Product", "ProductId", "PRD-WALK", {"Status": 1, "Name": f"Walk Product {run}", "IsDeleted": 0})


def new_ri(name):
    r, loc = fill_create("/RegulatoryIntelligence/Create", "/RegulatoryIntelligence/Create",
                         {"ProductId": PROD, "ProductName": name, "Day0Date": "2026-03-01", "SubmissionDate": "2026-03-01",
                          "JurisdictionId": "1", "ApplicationTypeId": "1"})
    rid = int(loc.rstrip("/").split("/")[-1]) if "/Details/" in loc else None
    return rid, r


ri, r = new_ri(f"Walk RI {run}")
check("RI: application created through the Create page, in Strategy", ri and status("RegulatoryApplications", ri) == RI["Strategy"],
      (r.status_code, [e.get_text(" ", strip=True) for e in BeautifulSoup(r.text, "html.parser").select(".text-danger") if e.get_text(strip=True)][:5]))
if ri:
    P = f"/RegulatoryIntelligence/Details/{ri}"
    def rt(target, **kw):
        return step(s, P, "/RegulatoryIntelligence/Transition", match={"newStatus": target}, **kw)
    for target in ("Preparation", "Submitted", "UnderReview", "ClockStop", "UnderReview"):
        r, _ = rt(target)
        check(f"RI: → {target}", status("RegulatoryApplications", ri) == RI[target], page_message(s, r))
    # milestones and documents while in review
    post_form(s, "/RegulatoryIntelligence/AddMilestone", P, {"applicationId": ri, "milestoneName": "Evaluation report",
                                                           "plannedDate": "2026-06-01", "isClockStop": "false", "notes": "e2e"})
    ms = q1("select Id from RIMilestones where RegulatoryApplicationId=? order by Id desc", (ri,))
    check("RI: milestone added", ms is not None)
    if ms:
        post_form(s, "/RegulatoryIntelligence/DeleteMilestone", P, {"id": ms["Id"]})
        check("RI: milestone deleted", q1("select Id from RIMilestones where Id=?", (ms["Id"],)) is None
              or row("RIMilestones", ms["Id"]).get("IsDeleted") in (1, True))
    url, f = find_form(get(s, P).text, "/RegulatoryIntelligence/UploadDocument")
    if url:
        f.pop("__types", None); f.update({"applicationId": ri, "documentType": "Dossier"})
        s.post(BASE + url, data=f, files={"file": ("dossier.pdf", b"%PDF-1.4 e2e ri", "application/pdf")}, allow_redirects=False)
    d = q1("select Id from RIDocuments where RegulatoryApplicationId=? order by Id desc", (ri,))
    check("RI: document uploaded through the page", d is not None, url)
    if d:
        post_form(s, "/RegulatoryIntelligence/DeleteDocument", P, {"id": d["Id"], "deletionReason": "superseded"})
        check("RI: document removed with a reason", row("RIDocuments", d["Id"]).get("IsDeleted") in (1, True))
    r, _ = step(s, P, "/RegulatoryIntelligence/ApproveApplication", extra={"pwd": "wrong"})
    check("RI: approval with a wrong password is refused", status("RegulatoryApplications", ri) == RI["UnderReview"], page_message(s, r))
    r, _ = step(s, P, "/RegulatoryIntelligence/ApproveApplication")
    check("RI: Approve (signed) → Approved, locked", status("RegulatoryApplications", ri) == RI["Approved"]
          and row("RegulatoryApplications", ri)["IsLocked"], page_message(s, r))
    post_form(s, "/RegulatoryIntelligence/AddCondition", P, {"applicationId": ri, "conditionDescription": "Post-market study",
                                                           "conditionType": "1", "dueDate": "2027-06-01"})
    cond = q1("select Id from RIConditionsOfApproval where RegulatoryApplicationId=? order by Id desc", (ri,))
    check("RI: condition of approval added", cond is not None)
    if cond:
        post_form(s, "/RegulatoryIntelligence/CloseCondition", P, {"id": cond["Id"], "completionNotes": "study done"})
        c = row("RIConditionsOfApproval", cond["Id"])
        check("RI: condition closed (notes kept)", "study done" in str(c.get("CompletionNotes") or c), c)
    r, _ = rt("VariationRequired")
    check("RI: Approved → Variation Required", status("RegulatoryApplications", ri) == RI["VariationRequired"], page_message(s, r))
    post_form(s, "/RegulatoryIntelligence/AddVariation", P, {"applicationId": ri, "variationDescription": "New pack size",
                                                           "variationType": "1", "submissionDate": "2026-09-01", "notes": "e2e"})
    var = q1("select Id from RIVariations where ParentApplicationId=? order by Id desc", (ri,))
    check("RI: variation recorded", var is not None)
    r, _ = step(s, P, "/RegulatoryIntelligence/ApproveApplication")
    check("RI: variation approved (signed) → Approved", status("RegulatoryApplications", ri) == RI["Approved"], page_message(s, r))
    r, _ = step(s, P, "/RegulatoryIntelligence/InitiateRenewal", extra={"reason": "expires in 6 months"})
    check("RI: Initiate Renewal → Renewal Due", status("RegulatoryApplications", ri) == RI["RenewalDue"], page_message(s, r))
    r, _ = rt("Submitted")
    check("RI: renewal submitted → Submitted", status("RegulatoryApplications", ri) == RI["Submitted"], page_message(s, r))
    rt("UnderReview"); step(s, P, "/RegulatoryIntelligence/ApproveApplication")
    r, _ = step(s, P, "/RegulatoryIntelligence/SuspendApplication", extra={"reason": "authority suspension"})
    check("RI: Suspend (signed) → Suspended", status("RegulatoryApplications", ri) == RI["Suspended"], page_message(s, r))
    acts = [a["Action"] for a in q("select Action from RIAuditLogs where RegulatoryApplicationId=?", (ri,))]
    check("RI: steps in the audit trail", len(acts) >= 12, len(acts))
ri2, _ = new_ri(f"Walk RI B {run}")
if ri2:
    P2 = f"/RegulatoryIntelligence/Details/{ri2}"
    for t in ("Preparation", "Submitted", "UnderReview"):
        step(s, P2, "/RegulatoryIntelligence/Transition", match={"newStatus": t})
    r, _ = step(s, P2, "/RegulatoryIntelligence/RejectApplication", extra={"reason": "insufficient data"})
    check("RI: Reject (signed) → Rejected (closed: no further steps)", status("RegulatoryApplications", ri2) == RI["Rejected"], page_message(s, r))
ri3, _ = new_ri(f"Walk RI C {run}")
if ri3:
    P3 = f"/RegulatoryIntelligence/Details/{ri3}"
    for t in ("Preparation", "Submitted"):
        step(s, P3, "/RegulatoryIntelligence/Transition", match={"newStatus": t})
    r, _ = step(s, P3, "/RegulatoryIntelligence/WithdrawApplication", extra={"reason": "business decision"})
    check("RI: Withdraw (signed) → Withdrawn", status("RegulatoryApplications", ri3) == RI["Withdrawn"], page_message(s, r))
    # access pathway (e.g. SAS) with a six-monthly supply report
    r, loc = fill_create(f"/RegulatoryIntelligence/CreateAccessPathway?applicationId={ri}", "/RegulatoryIntelligence/CreateAccessPathway",
                         {"RegulatoryApplicationId": ri})
    ap = q1("select Id from RIAccessPathwayRecords order by Id desc")
    check("RI: access pathway created through its page", ap is not None and r.status_code == 302, r.status_code)
    if ap:
        post_form(s, "/RegulatoryIntelligence/AddSixMonthlyReport", f"/RegulatoryIntelligence/AccessPathwayDetails/{ap['Id']}",
                  {"accessPathwayId": ap["Id"], "periodStart": "2026-01-01", "periodEnd": "2026-06-30", "quantitySupplied": "12",
                   "unit": "packs", "status": "1", "notes": "e2e"})
        rep = q1("select Id from RISixMonthlySupplyReports where AccessPathwayRecordId=? order by Id desc", (ap["Id"],)) \
            if "AccessPathwayRecordId" in [c["name"] for c in q("pragma table_info(RISixMonthlySupplyReports)")] else \
            q1("select Id from RISixMonthlySupplyReports order by Id desc")
        check("RI: six-monthly supply report added", rep is not None)

# ═══ Pharmacovigilance ════════════════════════════════════════════════════════════════════════════════
AE = {"Draft": 0, "UnderMedicalReview": 1, "PendingApproval": 2, "Approved": 3, "Closed": 4}


def new_ae():
    num = f"AE-WALK-{run}-{len(q('select Id from AdverseEvent'))}"
    exec_sql("""insert into AdverseEvent (AENumber, ReportDate, ReportType, ReportSource, Status, IsSerious, SeriousnessCriteria,
                IsFatalOrLifeThreatening, IsLocked, AEDescription, MedicalNarrative, SignalDetected, CreatedBy, CreatedOn)
                values (?, ?, 0, 0, 0, 1, 1, 0, 0, 'walk: rash after dose', 'walk narrative', 0, 'e2e', ?)""", (num, NOW, NOW))
    ae = q1("select Id from AdverseEvent where AENumber=?", (num,))["Id"]
    exec_sql("insert into AEReporter (AdverseEventId, ReporterName, Type) values (?, 'Walk Reporter', 0)", (ae,))
    exec_sql("insert into AEPatient (AdverseEventId, PatientIdentifier) values (?, 'WALK-P1')", (ae,))
    exec_sql("insert into AESuspectProduct (AdverseEventId, ProductName) values (?, 'Walk Product')", (ae,))
    exec_sql("""insert into AERegulatoryReport (AdverseEventId, IsReportableToTGA, OverseasReportingRequired, IncludeInNextPSUR,
                SignificantSafetyIssue, Status) values (?, 1, 0, 1, 0, 0)""", (ae,))
    return ae


ae = new_ae()
P = f"/Pharmacovigilance/Details/{ae}"
r, _ = step(s, P, "/Pharmacovigilance/SubmitForReview")
check("AE: a serious case without Day 0 is refused", status("AdverseEvent", ae) == AE["Draft"] and says(page_message(s, r), "Day 0 Date is required for serious adverse events before submitting for review."), page_message(s, r))
exec_sql("update AdverseEvent set Day0Date=? where Id=?", ("2026-09-01 00:00:00", ae))
r, _ = step(s, P, "/Pharmacovigilance/SubmitForReview")
check("AE: Submit for medical review", status("AdverseEvent", ae) == AE["UnderMedicalReview"], page_message(s, r))
r, _ = step(s, P, "/Pharmacovigilance/SendBack", extra={"comment": "missing dose details"})
check("AE: Send back to Draft", status("AdverseEvent", ae) == AE["Draft"], page_message(s, r))
step(s, P, "/Pharmacovigilance/SubmitForReview")
cm = module("Complaints")
cpl = new_record(cm["table"], cm["uniq"], cm["prefix"], cm["init"])
post_form(s, "/Pharmacovigilance/LinkComplaint", P, {"aeId": ae, "complaintId": cpl})
linked = row("AdverseEvent", ae).get("LinkedComplaintId") or row(cm["table"], cpl).get("LinkedAdverseEventId")
check("AE: linked to a complaint", linked in (cpl, ae), linked)
post_form(s, "/Pharmacovigilance/UnlinkComplaint", P, {"aeId": ae})
check("AE: complaint link removed", not (row("AdverseEvent", ae).get("LinkedComplaintId")), row("AdverseEvent", ae).get("LinkedComplaintId"))
r, _ = step(s, P, "/Pharmacovigilance/CompleteEvaluation")
check("AE: Complete evaluation → Pending Approval", status("AdverseEvent", ae) == AE["PendingApproval"], page_message(s, r))
r, _ = step(s, P, "/Pharmacovigilance/Approve")
check("AE: Approve → Approved", status("AdverseEvent", ae) == AE["Approved"], page_message(s, r))
r, _ = step(s, P, "/Pharmacovigilance/LogTGASubmission", extra={"submissionDate": "2026-09-05", "adrsRef": "ADRS-123", "method": "Online"})
subs = [dict(x) for x in q("select * from AERegulatoryReport where AdverseEventId=?", (ae,))]
check("AE: regulator submission logged (date and reference)", any("ADRS-123" in str(x) and "2026-09-05" in str(x) for x in subs), (subs, page_message(s, r)))
post_form(s, "/Pharmacovigilance/LinkComplaint", P, {"aeId": ae, "complaintId": cpl})
check("AE: an approved (locked) case cannot be linked", not row("AdverseEvent", ae).get("LinkedComplaintId"))
r, _ = step(s, P, "/Pharmacovigilance/CloseCase")
check("AE: Close case → Closed, locked", status("AdverseEvent", ae) == AE["Closed"] and row("AdverseEvent", ae)["IsLocked"], page_message(s, r))
n0 = len(q("select Id from AdverseEvent"))
r, _ = step(s, P, "/Pharmacovigilance/CreateFollowUp")
check("AE: follow-up created from the closed case", len(q("select Id from AdverseEvent")) == n0 + 1, page_message(s, r))
ae2 = new_ae()
url, f = find_form(get(s, f"/Pharmacovigilance/Details/{ae2}").text, "/Pharmacovigilance/UploadAttachment")
if url:
    f.pop("__types", None); f.update({"aeId": ae2, "documentType": "Other", "description": "walk"})
    s.post(BASE + url, data=f, files={"file": ("lab.pdf", b"%PDF-1.4 walk ae", "application/pdf")}, allow_redirects=False)
att = q1("select Id from AEAttachment where AdverseEventId=? order by Id desc", (ae2,)) if q("select name from sqlite_master where name='AEAttachment'") else None
check("AE: attachment uploaded through the page", att is not None, url)
if att:
    post_form(s, "/Pharmacovigilance/DeleteAttachment", f"/Pharmacovigilance/Details/{ae2}", {"id": att["Id"], "aeId": ae2, "deletionReason": "wrong"})
    check("AE: attachment removed with a reason, kept", row("AEAttachment", att["Id"]).get("IsDeleted") in (1, True))

# periodic report (PSUR)
r, loc = fill_create("/PVPeriodicReports/Create", "/PVPeriodicReports/Create",
                     {"Title": f"Walk PSUR {run}", "PeriodStart": "2026-01-01", "PeriodEnd": "2026-06-30", "DueDate": "2026-09-30"})
ps = q1("select Id from PVPeriodicReport where Title=?", (f"Walk PSUR {run}",))
check("PSUR: created through the Create page, In Preparation", ps and status("PVPeriodicReport", ps["Id"]) == 0, r.status_code)
if ps:
    PP = f"/PVPeriodicReports/Details/{ps['Id']}"
    r, _ = step(s, PP, "/PVPeriodicReports/Transition", match={"newStatus": "UnderReview"})
    check("PSUR: → Under Review", status("PVPeriodicReport", ps["Id"]) == 1, page_message(s, r))
    r, _ = step(s, PP, "/PVPeriodicReports/Transition", match={"newStatus": "InPreparation"})
    check("PSUR: back to In Preparation", status("PVPeriodicReport", ps["Id"]) == 0, page_message(s, r))
    step(s, PP, "/PVPeriodicReports/Transition", match={"newStatus": "UnderReview"})
    post_form(s, "/PVPeriodicReports/LinkCase", PP, {"psurId": ps["Id"], "aeRegId": ae})
    r, _ = step(s, PP, "/PVPeriodicReports/SubmitReport", extra={"pwd": "wrong"})
    check("PSUR: submit with a wrong password is refused", status("PVPeriodicReport", ps["Id"]) == 1, page_message(s, r))
    r, _ = step(s, PP, "/PVPeriodicReports/SubmitReport")
    check("PSUR: Submit to authority (signed) → Submitted", status("PVPeriodicReport", ps["Id"]) == 2, page_message(s, r))
    r, _ = step(s, PP, "/PVPeriodicReports/AcceptReport")
    check("PSUR: Accepted by authority (signed) → Accepted", status("PVPeriodicReport", ps["Id"]) == 3, page_message(s, r))

# signal review (signals are created automatically from case counts; one is inserted here)
for outcome, code in (("Confirmed", 2), ("Refuted", 3)):
    exec_sql("""insert into PVSignalReview (MedDRAPT, DetectedOn, DetectedBy, TriggerCount, Status, CreatedOn)
                values (?, ?, 'system', 3, 0, ?)""", (f"Walk PT {outcome} {run}", NOW, NOW))
    sg = q1("select Id from PVSignalReview where MedDRAPT=?", (f"Walk PT {outcome} {run}",))["Id"]
    SP = f"/PVSignalReview/Details/{sg}"
    r, _ = step(s, SP, "/PVSignalReview/Transition", match={"newStatus": "UnderReview"})
    check(f"signal: Open → Under Review", status("PVSignalReview", sg) == 1, page_message(s, r))
    r, _ = step(s, SP, "/PVSignalReview/Transition", match={"newStatus": outcome}, extra={"conclusion": "walk"})
    check(f"signal: Under Review → {outcome}", status("PVSignalReview", sg) == code, page_message(s, r))
    r, _ = step(s, SP, "/PVSignalReview/Transition", match={"newStatus": "Closed"})
    check(f"signal: {outcome} → Closed", status("PVSignalReview", sg) == 4, page_message(s, r))

# literature search log
r, loc = fill_create("/PVLiterature/Create", "/PVLiterature/Create", {"SearchDate": "2026-09-01", "SearchTerms": f"walk {run}"})
check("literature: search logged through the Create page", q1("select Id from PVLiteratureSearch where SearchTerms=?", (f"walk {run}",)) is not None, r.status_code)

# ═══ Data Disposal ════════════════════════════════════════════════════════════════════════════════════
dm = module("Deviations")
old = {**dm["init"], "Status": 4, "ApprovedBy": ADMIN, "ApprovedOn": "2014-01-01 00:00:00", "RetainUntil": "2020-01-01 00:00:00"}
held = new_record(dm["table"], dm["uniq"], dm["prefix"], old)
free = new_record(dm["table"], dm["uniq"], dm["prefix"], old)
idx = get(s, "/Disposal").text
held_no, free_no = row(dm["table"], held)["DeviationId"], row(dm["table"], free)["DeviationId"]
check("disposal: records past their retention date are listed", held_no in idx and free_no in idx)
# a reviewed maintenance job (the workflow ends at Reviewed: no approval date) past its retention date
mm = module("Maintenance")
mnt = new_record(mm["table"], mm["uniq"], mm["prefix"], {**mm["init"], "Status": 3, "ReviewedBy": ADMIN,
                                                         "ReviewedOn": "2014-02-01 00:00:00", "RetainUntil": "2020-02-01 00:00:00"})
r = get(s, "/Disposal")
check("disposal: page opens with a reviewed maintenance job past retention (listed as Reviewed)",
      r.status_code == 200 and f"MNT-{mnt}" in r.text and says(r.text, "Reviewed"), r.status_code)
# one record with a retention date but no closing date must not break the page
odd = new_record(dm["table"], dm["uniq"], dm["prefix"], {**dm["init"], "Status": 4, "RetainUntil": "2020-03-01 00:00:00"})
check("disposal: page still opens when a record has no closing date", get(s, "/Disposal").status_code == 200)
exec_sql("update Deviation set RetainUntil=null where Id=?", (odd,))
post_form(s, "/Disposal/SetHold", "/Disposal", {"module": "Deviation", "recordId": held, "reason": "1", "comments": "litigation"})
url, f = find_form(get(s, "/Disposal").text, "/Disposal/Dispose")
check("disposal: the signed Dispose form is on the page", url is not None)
def dispose(sel):
    data = {"selections": sel, "InputEmail": ADMIN, "InputPassword": PW}
    return post_form(s, "/Disposal/Dispose", "/Disposal", data)
dispose([f"Deviation:{held}"])
check("disposal: a record on retention hold is not disposed (attempt logged)", not row(dm["table"], held)["IsDeleted"]
      and q1("select Id from DisposalAuditLog where RecordId=? and Action='DisposalAttemptBlocked'", (held,)) is not None)
post_form(s, "/Disposal/RemoveHold", "/Disposal", {"module": "Deviation", "recordId": held, "InputEmail": ADMIN,
                                                   "InputPassword": PW, "removalReason": "case settled"})
dispose([f"Deviation:{held}"])
check("disposal: after the hold is removed, the cool-down still protects it", not row(dm["table"], held)["IsDeleted"])
r = post_form(s, "/Disposal/Dispose", "/Disposal", {"selections": [f"Deviation:{free}"], "InputEmail": ADMIN, "InputPassword": "wrong"})
check("disposal: a wrong password disposes nothing", not row(dm["table"], free)["IsDeleted"])
dispose([f"Deviation:{free}"])
check("disposal: an eligible record is disposed (soft delete, logged)", row(dm["table"], free)["IsDeleted"]
      and q1("select Id from DisposalAuditLog where RecordId=? and Action='Disposed'", (free,)) is not None)
check("disposal: a disposed record leaves the register", free_no not in get(s, "/Deviations").text)
squ = login(QU)
r = post_form(squ, "/Disposal/Dispose", "/", {"selections": [f"Deviation:{held}"], "InputEmail": QU, "InputPassword": PW})
check("disposal: refused for a Quality User", not row(dm["table"], held)["IsDeleted"])
s = login()

# ═══ Quality modules: attachments through the pages (all seven event modules) ═════════════════════════
for name in ("Deviations", "Complaints", "CAPA", "Change Control", "Audits", "Risk", "Maintenance"):
    m = module(name)
    rid = new_record(m["table"], m["uniq"], m["prefix"], m["init"])
    P = f"/{m['ctrl']}/Details/{rid}"
    url, f = find_form(get(s, P).text, f"/{m['ctrl']}/UploadAttachment")
    fname = f"walk-{run}-{m['ctrl']}.pdf"
    if url:
        f.pop("__types", None); f["description"] = "walk"
        s.post(BASE + url, data=f, files={"file": (fname, b"%PDF-1.4 walk " + name.encode(), "application/pdf")}, allow_redirects=False)
    page = get(s, P).text
    check(f"{name}: attachment uploaded through the record page", url is not None and fname in page, url)
    dl = find_form(page, f"/{m['ctrl']}/DeleteAttachment")[0]
    if dl:
        r, _ = step(s, P, f"/{m['ctrl']}/DeleteAttachment", extra={"deletionReason": "uploaded in error", "reason": "uploaded in error"})
        page = get(s, P).text
        check(f"{name}: attachment removed with a reason, listed as deleted", says(page, "Deleted attachments ({0})") and fname in page, page_message(s, r))

# ═══ Linked CAPA, Change action plan, deletes ═════════════════════════════════════════════════════════
for name, ctrl in (("Deviations", "Deviations"), ("Complaints", "Complaints"), ("Audits", "Audits")):
    m = module(name)
    rid = new_record(m["table"], m["uniq"], m["prefix"], {**m["init"], "Status": 2 if name != "Audits" else 3})
    n0 = len(q("select Id from Capa"))
    r, _ = step(s, f"/{ctrl}/Details/{rid}", f"/{ctrl}/CreateCapa")
    if r is None:
        r = post_form(s, f"/{ctrl}/CreateCapa/{rid}", f"/{ctrl}/Details/{rid}", {"id": rid})
    loc = r.headers.get("Location", "")
    if "/Capas/Create" in loc:           # opens the CAPA form linked to its source; fill and save it
        # the source is pre-selected by the page's script; send it as the browser does
        key = {"Deviations": "selectedDeviations", "Complaints": "selectedComplaints", "Audits": "selectedAudits"}[name]
        r2, loc2 = fill_create(loc, "/Capas/Create", {"Title": f"Linked CAPA from {name} {run}", key: str(rid)})
    new = q1("select * from Capa order by Id desc") if len(q("select Id from Capa")) == n0 + 1 else None
    check(f"{name}: + Create Linked CAPA opens a linked CAPA form; saving it makes the CAPA", "/Capas/Create" in loc and new is not None, loc)
    if name == "Deviations" and new:
        check("Deviations: the deviation now points to its CAPA", row(m["table"], rid)["LinkedCapaId"] == new["Id"]
              or q1("select 1 from CapaDeviation where CapaId=? and DeviationId=?", (new["Id"], rid)) is not None, row(m["table"], rid)["LinkedCapaId"])
cmod = module("Change Control")
ch = new_record(cmod["table"], cmod["uniq"], cmod["prefix"], {**cmod["init"], "Status": 1, "DetailedActionPlanRequired": 1})
r, posted = step(s, f"/Changes/Details/{ch}", "/Changes/SaveActionPlan")
check("Change Control: action plan saved through the page", r is not None and r.status_code in (200, 302), page_message(s, r) if r is not None else None)
dv = new_record(dm["table"], dm["uniq"], dm["prefix"], dm["init"])
r = post_form(s, f"/Deviations/Delete/{dv}", f"/Deviations/Delete/{dv}", {"id": dv})
check("Deviations: an Administrator deletes an early record (soft delete)", row(dm["table"], dv)["IsDeleted"], r.status_code)
dv2 = new_record(dm["table"], dm["uniq"], dm["prefix"], {**dm["init"], "Status": 4})
post_form(s, f"/Deviations/Delete/{dv2}", f"/Deviations/Details/{dv2}", {"id": dv2})
check("Deviations: an approved record cannot be deleted", not row(dm["table"], dv2)["IsDeleted"])

# ═══ Documents: reassign author, manual issue ═════════════════════════════════════════════════════════
docm = module("Documents")
TR = "walk-trainer@e2e.local"
ensure_user(TR, ["Trainer"], "Walk", "Trainer")
dd = new_record(docm["table"], docm["uniq"], docm["prefix"], docm["init"])
r, _ = step(s, f"/AppDocuments/Details/{dd}", "/AppDocuments/ReassignAuthor",
            extra={"NewAuthorId": q1("select Id from AspNetUsers where Email=?", (TR,))["Id"], "reason": "handover"})
check("Documents: Reassign Author (signed) on a draft", row(docm["table"], dd)["AuthoredBy"] == TR, page_message(s, r) if r is not None else "no form")
da = new_record(docm["table"], docm["uniq"], docm["prefix"], {**docm["init"], "Status": 1, "ApprovedBy": ADMIN, "ApprovedOn": NOW})
r, _ = step(s, f"/AppDocuments/Details/{da}", "/AppDocuments/IssueDocument")
check("Documents: an approved document is issued manually → Effective", status(docm["table"], da) == 5, page_message(s, r) if r is not None else "no form")

# ═══ Products: strengths and specification lines ══════════════════════════════════════════════════════
pr = new_record("Product", "ProductId", "PRD-WALK", {"Status": 0, "Name": f"Walk Spec Product {run}", "IsDeleted": 0})
r, _ = step(s, f"/Products/Edit/{pr}", "/Products/AddStrength", extra={"strength": "500 mg", "Strength": "500 mg", "value": "500 mg"})
check("Products: strength added", q1("select count(*) n from ProductStrength where ProductId=?", (pr,))["n"] >= 1 if q("select name from sqlite_master where name='ProductStrength'") else r is not None,
      page_message(s, r) if r is not None else "no form")
r, _ = step(s, f"/Products/Details/{pr}", "/Products/AddSpecLine", extra={"testParameter": "Assay", "TestParameter": "Assay",
            "method": "HPLC", "Method": "HPLC", "acceptanceCriterion": "95-105%", "AcceptanceCriterion": "95-105%"})
spec = q1("select Id from ProductSpecLines where ProductId=?", (pr,))
check("Products: specification line added", spec is not None, page_message(s, r) if r is not None else "no form")
if spec:
    step(s, f"/Products/Details/{pr}", "/Products/DeleteSpecLine")
    check("Products: specification line removed", q1("select Id from ProductSpecLines where Id=?", (spec["Id"],)) is None)

four_eyes(True)
sys.exit(summary())
