# MintQMS — User & Operations Guide

*A guide to every module of MintQMS, for the quality, regulatory and operations staff who log, review,
approve and oversee controlled records.*

**Version:** 1.0 · **Date:** October 2026 · **Source of truth:** the running application code (this guide
describes only what the system actually does).

> **No warranty.** MintQMS is free software provided "as is" and is **not validated** for any regulated use.
> Each organisation is responsible for validating and controlling its own installation — see
> [DISCLAIMER.md](../DISCLAIMER.md).

> **How states and rules were determined.** Workflow states, the roles allowed to perform each action, and
> the electronic-signature requirements in this guide are taken directly from the application's code and,
> where present, its formal state-machine definitions. Behaviour that is intentionally left out of the
> user-facing workflow (legacy compatibility states for old records) is not documented here.

---

## Table of contents

1. [Introduction](#1-introduction)
2. [Core concepts (read once)](#2-core-concepts-read-once)
   - [2.1 Roles & permissions](#21-roles--permissions)
   - [2.2 The electronic signature](#22-the-electronic-signature)
   - [2.3 Record lifecycles & how work moves](#23-record-lifecycles--how-work-moves)
   - [2.4 Immutability & the audit trail](#24-immutability--the-audit-trail)
   - [2.5 Attachments & watermarks](#25-attachments--watermarks)
   - [2.6 Returning, reopening & correcting records](#26-returning-reopening--correcting-records)
   - [2.7 Retention & disposal](#27-retention--disposal)
   - [2.8 Linking records & affected items](#28-linking-records--affected-items)
3. [Roles & access reference](#3-roles--access-reference)
4. [Quality Events](#4-quality-events)
   - [4.1 Deviations](#41-deviations)
   - [4.2 Complaints](#42-complaints)
   - [4.3 CAPA — Corrective & Preventive Actions](#43-capa--corrective--preventive-actions)
5. [Quality Assurance](#5-quality-assurance)
   - [5.1 Change Control](#51-change-control)
   - [5.2 Audits](#52-audits)
   - [5.3 Risk](#53-risk)
6. [Quality Operations](#6-quality-operations)
   - [6.1 Suppliers](#61-suppliers)
   - [6.2 Maintenance](#62-maintenance)
   - [6.3 Batch Release](#63-batch-release)
7. [Regulatory](#7-regulatory)
   - [7.1 Pharmacovigilance — Adverse Events](#71-pharmacovigilance--adverse-events)
   - [7.2 PV Literature Monitoring](#72-pv-literature-monitoring)
   - [7.3 PV Signal Review](#73-pv-signal-review)
   - [7.4 PV Periodic Reports (PSUR)](#74-pv-periodic-reports-psur)
   - [7.5 Regulatory Intelligence](#75-regulatory-intelligence)
   - [7.6 Licences & Certifications](#76-licences--certifications)
8. [Documents & Training](#8-documents--training)
   - [8.1 Document Management](#81-document-management)
   - [8.2 Training](#82-training)
9. [Reference / Master Data](#9-reference--master-data)
10. [Reporting & Oversight](#10-reporting--oversight)
    - [10.1 Reports](#101-reports)
    - [10.2 Dashboards](#102-dashboards)
11. [Administration](#11-administration)
    - [11.1 Users & Roles](#111-users--roles)
    - [11.2 System Settings & Release Certification Statements](#112-system-settings--release-certification-statements)
    - [11.3 Login History](#113-login-history)
    - [11.4 Data Disposal](#114-data-disposal)
    - [11.5 Setting up a new installation](#115-setting-up-a-new-installation)
12. [Appendices](#12-appendices)
    - [A. Design notes](#appendix-a--design-notes)
    - [B. State-transition reference](#appendix-b--state-transition-reference)
    - [C. Glossary](#appendix-c--glossary)

---

## 1. Introduction

**MintQMS** is a web-based Quality Management System. You use it in an ordinary web browser — there is nothing
to install. It holds your controlled records (deviations, complaints, changes, CAPAs, audits, risks, supplier
records, batch releases, regulatory submissions, documents, training, and more), moves each one through a
defined approval workflow, and keeps a permanent, tamper-resistant history of everything that happens.

**Who should read this guide.** Anyone who logs, reviews, approves or oversees quality records — quality staff,
regulatory affairs, operations, auditors and managers. Each module section is self-contained: find the module
you need in the table of contents and follow its walkthrough.

**Setting up a new installation?** Installation is covered in [DEPLOYMENT.md](../DEPLOYMENT.md); the first
steps inside the application (settings, users and roles) are in §11.5.

**How to use a module section.** Every module is documented with the same six parts:
1. **Purpose & intent** — what the module is for.
2. **Who can do what** — which roles may perform each action (this is enforced by the system).
3. **Record lifecycle** — the states a record passes through, and the moves between them.
4. **Step-by-step walkthrough** — how to log a record, move it through each stage, and close it.
5. **Handling exceptions** — how to send work back, correct mistakes, cancel, or re-open a closed record.
6. **Notes** — anything else worth knowing about the module.

**A note on terminology.** A *transition* is a move from one state to the next (for example "submit for
approval"). An *electronic signature* is re-entering your own login email and password to sign an action — see
§2.2. *Read-only* is a view-only access level.

---

## 2. Core concepts (read once)

These behaviours apply across the whole system. Reading this section once means each module section can stay
short.

### 2.1 Roles & permissions

Every user is assigned one or more **roles**. Roles decide what you can see and do. The twelve roles are:

| Role | In plain terms |
|---|---|
| **Administrator** | Full system access; the only role that manages user roles and system settings. |
| **Quality Manager** | Senior quality approver; can approve/close most quality records. |
| **Quality User** | Quality staff; prepares and progresses records, but cannot give final approval on most workflows. |
| **Regulatory Manager** | Senior regulatory approver (pharmacovigilance, registrations, licences). |
| **Regulatory User** | Regulatory staff; prepares and progresses regulatory records. |
| **Authorised Person (AP)** | Legally defined role that **certifies batch release**; also a senior quality approver. |
| **Auditor** | Plans and conducts audits; has view access across many modules; cannot give final approvals. |
| **Trainer** | Manages training and confirms competency assessments. |
| **Operations User** | Operations/shop-floor staff; executes maintenance and enters batch test data. |
| **Commercial User** | Commercial staff; can raise certain records (e.g. complaints, supplier requests). |
| **Read Only** | Can view records across the system but **cannot create or change anything** (see below). |

**Read-Only is enforced everywhere.** A user in the Read-Only role is blocked from every create, edit, approve
or delete action across the entire system, automatically — regardless of any other permission. Read-Only means
exactly that.

**Permissions are real, not just hidden buttons.** If your role is not allowed to perform an action, the system
refuses it even if you reach the action directly — access control is enforced by the application, not merely by
hiding menu items.

### 2.2 The electronic signature

Important actions — submitting, approving, closing, returning, certifying, disposing — require an **electronic
signature**. To sign, you re-enter **your own** login email and password at the point of action. The system:

- confirms the email is **yours** (you cannot sign as someone else),
- verifies the password, and
- records **who signed, what they did, and exactly when** in the record's permanent history.

If the email isn't yours or the password is wrong, the action is refused. An electronic signature here carries
the same intent as a handwritten signature: it binds the signer, the meaning of the action, and the time.

> Most workflow transitions require a signature. A few low-ceremony actions on reference data do not (these are
> called out where relevant).

### 2.3 Record lifecycles & how work moves

Each record type has a **lifecycle** — a fixed set of **states** (e.g. *Draft*, *Under Review*, *Approved*,
*Closed*) and the **moves** allowed between them. You cannot skip states or move backwards except through a
defined "return" action. The system rejects any move that isn't allowed from the current state, so a record can
only ever be in a valid, well-defined condition.

Some modules require certain fields to be completed before a move is permitted (for example, a deviation's
investigation must be filled in before it can leave evaluation). The guide notes these preconditions per module.

### 2.4 Immutability & the audit trail

- **Every record keeps a permanent audit trail** — who did what, when, and (for edits) which fields changed
  from what to what. This includes changes to a record's **linked records and affected items** (see §2.8): the
  audit entry records which links were added or removed, not just changes to text fields. The audit trail
  **cannot be altered or deleted** by anyone; it is locked at the database level. This is your inspection-ready
  history.
- **Approved/finalised records are protected.** Once certain records reach an approved or locked state, their
  content cannot be silently changed. They can only move on through a defined, signed action.
- **Nothing is truly deleted.** "Deleting" a record hides it from active views but retains it (a *soft delete*),
  so the history is never broken.
- **Times are shown in your site's time zone.** The system stores every time-stamp in UTC and displays it in the
  **site time zone** set in *Settings → System Settings* (each organisation sets its own; the initial value is
  Brisbane, AEST). Audit-trail time columns and
  watermarks show the zone label, e.g. *(AEST)*. "Today" for due and overdue flags is the site's today.

### 2.5 Attachments & watermarks

- Most records let you **attach supporting files** (certificates, photos, signed forms). Attachments are kept
  with the record. Accepted types: **PDF, Word, Excel, PowerPoint, OpenDocument, CSV, ZIP, JPG and PNG**, up to
  **25 MB** per file (**10 MB** for training attachments).
- **Removing an attachment asks for a reason.** The file is not destroyed: it moves to a collapsible
  **Deleted attachments (N)** list on the record, where it can still be opened. A deleted attachment opens with a
  diagonal **DELETED ATTACHMENT - RETAINED FOR TRACEABILITY** mark, and both the removal and each later view are
  logged in the record's audit trail.
- Uploaded files can only be reached through the record (signed-in, audited and watermarked) — never by a direct
  web address.
- **Files are fingerprinted.** Each upload's SHA-256 fingerprint is recorded, and every download or view re-checks
  it. If a stored file has been altered, it is not opened: you see an integrity warning, the failure is written to
  the record's audit trail, and the administrator should be contacted.
- When you **download a controlled file**, the system automatically **watermarks** it with its status and your
  details — for example *"Approved copy — valid at time of download"* or *"Unapproved — for review only."* This
  makes uncontrolled printouts easy to spot.

### 2.6 Returning, reopening & correcting records

There are three ways to handle a mistake or a change of circumstance:

- **Return (rework)** — while a record is still in progress, a reviewer can **send it back** to an earlier stage
  for correction. A reason is required and the return is signed and logged. Use this for "not ready yet."
- **Edit (correction)** — records can be edited **only in their early, in-progress states**; once submitted for
  approval or approved, editing is blocked. Each module states which states are editable.
- **Administrative re-open** — an **Administrator** can re-open a completed/approved record when something must
  be revisited. This requires the Administrator's electronic signature and a documented reason, and it is
  logged. The original approval is **never erased** — it stays in the audit trail.

### 2.7 Retention & disposal

When a record is finalised, the system stamps it with a **retain-until date** based on a configurable retention
period for that record type. After that date the record becomes eligible for **disposal** in the Data Disposal
module (found under **Settings**; see §11.4). Disposal is a controlled, Administrator-only, electronically-signed
action; a retention **hold** can extend retention where needed.

---

### 2.8 Linking records & affected items

Records do not stand alone — the system lets you connect them so that cause, effect and impact stay traceable.
There are two kinds of link, and both are visible from each of the linked records (a link never changes a
record's workflow state):

- **Affected items.** Every quality event — **deviation, complaint, CAPA, change, risk and audit** — can be
  tagged on its Create/Edit screen with the master-data items it concerns: **products, processes, equipment
  (assets), materials and suppliers**. This records *what* an event touched, so the impact of any event can be
  seen from the event and from the affected item.
- **Record-to-record links.** Records also reference one another to keep the cause-and-action chain intact: a
  **CAPA** links back to the source it was raised from; a **change** or a **maintenance** job links to the
  deviation(s) and CAPA(s) it implements; a **supplier** links to the quality records raised against it; a
  **complaint** links to its customer and to a related adverse event. Each module section notes the links it
  supports.

> Links are for traceability only — they are not approvals, and creating one does not require a signature.

---

## 3. Roles & access reference

This section summarises **who can approve what**. "Create/prepare" generally includes Administrator plus the
relevant operational roles; final approval is more tightly restricted. Read-Only can view but never change.

| Module | Typically prepared by | Final approval / key decision by |
|---|---|---|
| Deviations | Quality (QU/QM), AP, Ops, Commercial | **Admin / Quality Manager / AP** |
| Complaints | Quality, AP, Ops, Commercial | **Admin / Quality Manager / AP** (close) |
| CAPA | Quality, AP, Auditor, Ops, Commercial | **Admin / Quality Manager / AP** (accept & approve) |
| Change Control | Quality, AP, Ops, Commercial | **Admin / Quality Manager / AP** (accept & approve) |
| Audits | Quality, Auditor | **Admin / Quality Manager** (approve & close) |
| Risk | broad (any non-Read-Only) | **Admin / Quality Manager / AP** (accept & close) |
| Suppliers | Quality, AP, Ops, Commercial | **Admin / Quality Manager / AP** (approve/qualify) |
| Maintenance | Quality, AP, **Operations** | **Admin / Quality Manager / AP** (review) |
| Batch Release | Quality, AP, Operations | **Admin / Authorised Person** (certify) |
| Disposal | — | **Administrator** (dispose) |
| Pharmacovigilance / RI / Licences | Regulatory (RU/RM) | **Admin / Regulatory Manager / Regulatory User** |
| Documents | any user (author) | **Quality Manager / Authorised Person / Administrator** (approve/issue) |
| Training | Admin/QM/RegM/Trainer/AP (assign); trainees (acknowledge) | Trainer/QM/Admin (verify competency) |
| Reference data (Products, etc.) | Quality User / QM / Admin | **Quality Manager / Administrator** (approve) |
| Users / Roles / Settings | view: Admin/QM/RegM/Auditor | **Administrator only** (manage) |

*(Full per-action role detail is given in each module section below.)*

---

## 4. Quality Events

### 4.1 Deviations

**Purpose & intent.** A deviation records a departure from an approved process, procedure or specification. The
module takes it from first identification, through evaluation and investigation, to documented resolution and
formal approval — with a complete signed history. Where a corrective or preventive action is warranted, a
deviation can raise a linked **CAPA**.

**Who can do what** (Read-Only can view only; every transition below is electronically signed):

| Action | Who can do it |
|---|---|
| Create / record a deviation | Admin, Quality Manager, Quality User, Authorised Person, Operations User, Commercial User |
| Edit (early states only) | Admin, Quality Manager, Quality User, Authorised Person |
| Submit for evaluation; submit evaluation; submit for approval | Admin, Quality Manager, Quality User, Authorised Person |
| **Accept; Approve** | **Admin, Quality Manager, Authorised Person** (Quality User cannot) |
| Return (send back) | Admin, Quality Manager, Authorised Person |
| Delete (early states only) | **Administrator** |
| Administratively re-open | **Administrator** |

**Record lifecycle.** `Identification → Evaluation → Pending Acceptance → Accepted → Resolution → Approved`.
*Approved* is the final state.

| Step | Move | Who | Requirements |
|---|---|---|---|
| Submit for evaluation | Identification → Evaluation | Admin/QM/QU/AP | Event type and identification description completed |
| Submit evaluation | Evaluation → Pending Acceptance | Admin/QM/QU/AP | Evaluation, investigation, root-cause category and corrective action completed |
| Accept | Pending Acceptance → Accepted | **Admin/QM/AP** | — |
| Submit for approval | Accepted → Resolution | Admin/QM/QU/AP | Resolution documented |
| Approve | Resolution → Approved | **Admin/QM/AP** | Resolution documented |

**Step-by-step walkthrough.**
1. **Log it.** Open *Quality Events → Deviation Register → Create*. Enter the title, event type and a clear
   identification description. Save — the deviation starts in **Identification**.
2. **Submit for evaluation.** Once the event type and description are complete, choose **Submit for Evaluation**
   and sign. The deviation moves to **Evaluation**.
3. **Evaluate and investigate.** Complete the evaluation, investigation, root-cause category and the
   corrective/preventive action. Choose **Submit Evaluation** and sign → **Pending Acceptance**. (The system
   records who evaluated, investigated and proposed the action. If a formal CAPA is warranted, use **+ Create
   Linked CAPA** to raise one and link it to this deviation.)
4. **Accept.** A Manager/AP (not the Quality User) reviews and **Accepts** the evaluation package and signs →
   **Accepted**.
5. **Resolve.** Document the resolution, then **Submit for Approval** and sign → **Resolution**.
6. **Approve.** A Manager/AP gives final **Approval** and signs → **Approved**. The retain-until date is set
   automatically.

**Timeliness.** When you log a deviation, MintQMS sets a **target resolution date** automatically — the date
identified plus a standard resolution period (a *System Setting*, default 30 days). The deviation record and the
**Deviation Dashboard** flag it as the target **approaches** and once it is **overdue**, so open deviations are
resolved in good time. A critical deviation should still be escalated immediately, ahead of its target.

**Handling exceptions.**
- **Send back for rework:** a Manager/AP can **Return** the deviation from Evaluation, Pending Acceptance,
  Accepted or Resolution. A reason is required and signed. Returns roll the record back (e.g. Pending
  Acceptance → Evaluation) and clear the relevant sign-offs so the work can be redone.
- **Correcting details:** edit is available only in Identification, Evaluation and Accepted; it is blocked once
  the record is Pending Acceptance, in Resolution, or Approved.
- **Re-opening after approval:** an Administrator can **administratively re-open** an Approved deviation back to
  Resolution (signature + reason; logged). The prior approval remains in the audit trail.
- **Removing a record:** only an Administrator can delete, and only while the deviation is in Identification or
  Evaluation. Deletion is a soft delete (retained, hidden).

**Notes.** A Quality User may prepare and submit but cannot Accept or Approve — a built-in separation of duties.
The same approver may both submit and give final approval (independent approval is not required at this stage).
A deviation can spawn a single linked **CAPA** (via **+ Create Linked CAPA**); once linked, an inspector can
trace the corrective action back to the deviation that triggered it.

---

### 4.2 Complaints

**Purpose & intent.** The complaints module handles customer complaints from receipt through evaluation,
acceptance, resolution and closure. Complaints can be linked to the relevant **Customer** record and to related
**Adverse Events**, and can raise a **CAPA**.

**Who can do what** (Read-Only views only; every transition below is electronically signed):

| Action | Who can do it |
|---|---|
| Create / log a complaint | Admin, Quality Manager, Quality User, Authorised Person, Operations User, Commercial User |
| Edit (early states only) | Admin, Quality Manager, Quality User, Authorised Person |
| Send for evaluation; submit for acceptance; submit for resolution | Admin, Quality Manager, Quality User, Authorised Person |
| **Accept; Close** | **Admin, Quality Manager, Authorised Person** (Quality User cannot) |
| Return (send back) | Admin, Quality Manager, Authorised Person |
| Delete (early states) | **Administrator** |
| Administratively re-open | **Administrator** |

**Record lifecycle.** `Received → Evaluation → Pending Acceptance → Accepted → Resolution → Closed`.

| Step | Move | Who |
|---|---|---|
| Send for evaluation | Received → Evaluation | Admin/QM/QU/AP |
| Submit for acceptance | Evaluation → Pending Acceptance | Admin/QM/QU/AP |
| Accept | Pending Acceptance → Accepted | **Admin/QM/AP** |
| Submit for resolution | Accepted → Resolution | Admin/QM/QU/AP |
| **Close** | Resolution → Closed | **Admin/QM/AP** |

**Step-by-step walkthrough.**
1. **Log it.** *Quality Events → Complaint Register → Create.* Capture the complaint details and, if known, link
   the **Customer**. Save — the complaint starts in **Received**.
2. **Evaluate.** Choose **Send for Evaluation** and sign → **Evaluation**. Record your evaluation/investigation.
3. **Submit for acceptance** and sign → **Pending Acceptance**.
4. **Accept.** A Manager/AP (not the Quality User) reviews and **Accepts** the investigation and signs →
   **Accepted**.
5. **Resolve.** Document the resolution and **Submit for Resolution** and sign → **Resolution**.
6. **Close.** A Manager/AP gives the final **Close** and signs → **Closed**. (Link a CAPA at any point if a
   corrective action is warranted.)

**Timeliness.** MintQMS sets a **target resolution date** automatically when you log a complaint, based on its
**severity** — a shorter period for **Critical** complaints (a *System Setting*, default 7 days) and the standard
period for others (default 30 days). If you change the severity while the complaint is still editable, the target
is recalculated from the date received. The record and the **Complaint Dashboard** flag it as the target
**approaches** and once it is **overdue**.

**Handling exceptions.**
- **Send back for rework:** a Manager/AP can **Return** the complaint to an earlier stage (reason required,
  signed).
- **Correcting details:** edit only in the early states; blocked once submitted/closed.
- **Re-opening:** an Administrator can administratively re-open a Closed complaint back to Resolution
  (signature + reason; logged; prior history retained).
- **Removing a record:** Administrator only, early states, soft delete.

**Notes.** As with deviations, the Quality User can prepare and submit a complaint but cannot **Accept** or
**Close** — those decisions are reserved for Manager/AP/Admin. Complaints can spawn a CAPA and can be linked to Adverse Events for
pharmacovigilance traceability.

---

### 4.3 CAPA — Corrective & Preventive Actions

**Purpose & intent.** CAPA manages corrective and preventive actions through determination, assessment,
implementation and approval. A CAPA can be raised on its own or **from a source record** — a deviation,
complaint, audit, adverse event, safety signal, licence or regulatory item — keeping the full cause-and-action
chain traceable.

**Who can do what** (Read-Only views only; every transition below is electronically signed):

| Action | Who can do it |
|---|---|
| Create / raise a CAPA | Admin, Quality Manager, Quality User, Authorised Person, Auditor, Operations User, Commercial User |
| Edit (status-permitting) | Admin, Quality Manager, Quality User, Authorised Person, Auditor |
| Submit for acceptance; submit for approval | Admin, Quality Manager, Quality User, Authorised Person, Auditor |
| **Accept; Approve; Return** | **Admin, Quality Manager, Authorised Person** |
| Delete | **Administrator** |
| Administratively re-open | **Administrator** |

**Record lifecycle.** `Determination → Pending Assessment → Implementation → Pending Approval → Approved`.

| Step | Move | Who |
|---|---|---|
| Submit for acceptance | Determination → Pending Assessment | Admin/QM/QU/AP/Auditor |
| Accept | Pending Assessment → Implementation | **Admin/QM/AP** |
| Submit for approval | Implementation → Pending Approval | Admin/QM/QU/AP/Auditor |
| Approve | Pending Approval → Approved | **Admin/QM/AP** |

**Step-by-step walkthrough.**
1. **Raise it.** Either create a CAPA directly (*Quality Assurance → CAPA Register → Create*) or raise one from a
   source record (e.g. the "Create CAPA" action on a deviation or complaint), which links them. It starts in
   **Determination**.
2. **Determine and submit.** Document the determination, then **Submit for Acceptance** and sign → **Pending
   Assessment**.
3. **Accept.** A Manager/AP **Accepts**, which starts implementation, and signs → **Implementation**.
4. **Implement and submit.** Carry out and record the action, then **Submit for Approval** and sign → **Pending
   Approval**.
5. **Approve.** A Manager/AP gives final **Approval** and signs → **Approved**.

**Timeliness.** A CAPA carries a **target completion date**. You can set it when you raise the CAPA; if you leave
it blank, MintQMS defaults it to the date determined plus the standard CAPA resolution period (a *System Setting*,
default 30 days). The record and the **CAPA Dashboard** flag it as the target **approaches** and once it is
**overdue**, so actions are driven to completion on time.

**Handling exceptions.**
- **Send back for rework:** a Manager/AP can **Return** the CAPA to *Determination* or *Implementation* (signed).
- **Re-opening:** an Administrator can administratively re-open an Approved CAPA back to Implementation (signature
  + reason; logged).
- **Removing a record:** Administrator only; soft delete.

**Notes.** Quality Users and Auditors may raise and submit CAPAs but cannot Accept, Approve or Return — those are
Manager/AP/Admin decisions. Because CAPAs link to their source records, an inspector can trace any action back to
the event that triggered it.

---

## 5. Quality Assurance

### 5.1 Change Control

**Purpose & intent.** Change Control manages proposed changes through proposal, assessment, acceptance,
implementation and approval — ensuring changes are evaluated and authorised before they take effect. At
assessment each change is classified (**risk classification**, **change type**, and **permanent / temporary /
emergency**), and where the change needs several tracked tasks a built-in **Action Plan** records them and gates
closure until they are done. Supporting files can be attached and every edit is tracked.

**Who can do what** (Read-Only views only; every transition below is electronically signed):

| Action | Who can do it |
|---|---|
| Create / propose a change | Admin, Quality Manager, Quality User, Authorised Person, Operations User, Commercial User |
| Edit (early states only) | Admin, Quality Manager, Quality User, Authorised Person |
| Submit for assessment; submit for acceptance; submit for approval | Admin, Quality Manager, Quality User, Authorised Person |
| **Accept; Approve; Return** | **Admin, Quality Manager, Authorised Person** (Quality User cannot) |
| Delete (early states) | **Administrator** |
| Administratively re-open | **Administrator** |

**Record lifecycle.** `Proposal → Assessment → Pending Assessment → Implementation → Pending Approval → Approved`.

| Step | Move | Who |
|---|---|---|
| Submit for assessment | Proposal → Assessment | Admin/QM/QU/AP |
| Submit for acceptance | Assessment → Pending Assessment | Admin/QM/QU/AP |
| Accept | Pending Assessment → Implementation | **Admin/QM/AP** |
| Submit for approval | Implementation → Pending Approval | Admin/QM/QU/AP |
| Approve | Pending Approval → Approved | **Admin/QM/AP** |

**Step-by-step walkthrough.**
1. **Propose.** *Quality Assurance → Change Register → Create.* Describe the proposed change and its rationale.
   It starts in **Proposal**.
2. **Assess.** **Submit for assessment** and sign → **Assessment**. Set the **Risk Classification**, **Change
   Type** and **Nature** (permanent / temporary / emergency), document the assessment, and — if the change needs
   a structured plan — tick **Detailed Action Plan Required** to open the **Action Plan** tab (see below).
3. **Submit for acceptance** and sign → **Pending Assessment**.
4. **Accept.** A Manager/AP **Accepts**, starting implementation, and signs → **Implementation**.
5. **Implement.** Carry out the change and complete the Action Plan items (if used); then **submit for approval**
   and sign → **Pending Approval**. The system blocks this step until every applicable action is completed.
6. **Approve.** A Manager/AP gives final **Approval** and signs → **Approved**.

**Detailed Action Plan.** When *Detailed Action Plan Required* is ticked, an **Action Plan** tab appears (between
Overview and Attachments) holding a table of pre-implementation tasks — each with an **owner** (a system user or
a typed external name), **target date**, **status**, **evidence / reference** and **completed date**. Use **Load
suggested actions** to pre-fill the typical tasks for the selected Change Type, then edit, add or remove rows. A
row counts as done only when its status is **Complete** *and* it has a completed date (the two must agree); tasks
that don't apply are marked **Not applicable**. The plan is editable through Assessment and Implementation, every
change to it is recorded in the audit trail, and the change **cannot be submitted for approval until all
applicable actions are complete**. A simple change can instead record its plan in the Assessment field, without
the tab.

**Handling exceptions.**
- **Send back for rework:** a Manager/AP can **Return** the change — from Pending Assessment back to Proposal,
  from Pending Approval back to Implementation, or a deep return from Implementation back to Proposal (which
  clears the intermediate sign-offs). A reason is required and signed.
- **Correcting details:** edit is available only in the early states; blocked once submitted/approved.
- **Re-opening:** an Administrator can administratively re-open an Approved change back to Implementation
  (signature + reason; logged).
- **Removing a record:** Administrator only; soft delete.

**Notes.** As elsewhere, a Quality User may prepare and submit but cannot Accept, Approve or Return — those are
Manager/AP/Admin decisions. On the Create/Edit screen a change can be tagged with its **affected items**
(products, processes, assets, materials, suppliers) and linked to the **CAPA(s)** it implements — see §2.8.

---

### 5.2 Audits

**Purpose & intent.** The Audits module plans and runs internal and external audits — from scheduling, through
fieldwork and reporting, to review, approval and closure. Findings can be linked to CAPAs.

**Who can do what** (Read-Only views only; every transition below is electronically signed):

| Action | Who can do it |
|---|---|
| Create / schedule an audit | Admin, Quality Manager, Quality User, Authorised Person, Auditor |
| Edit (before review) | Admin, Quality Manager, Quality User, Authorised Person, Auditor |
| Start; submit report draft; submit for review | **Admin, Quality Manager, Auditor** |
| **Approve; Close** | **Admin, Quality Manager** (Auditor cannot) |
| Return for revision; return to review | Admin, Quality Manager |
| Delete | **Administrator** |
| Administratively re-open | **Administrator** |

**Record lifecycle.** `Scheduled → In Progress → Report Draft → Under Review → Approved → Closed`.

| Step | Move | Who |
|---|---|---|
| Start | Scheduled → In Progress | Admin/QM/Auditor |
| Submit report draft | In Progress → Report Draft | Admin/QM/Auditor |
| Submit for review | Report Draft → Under Review | Admin/QM/Auditor |
| Approve | Under Review → Approved | **Admin/QM** |
| Close | Approved → Closed | **Admin/QM** |

**Step-by-step walkthrough.**
1. **Schedule.** *Quality Assurance → Audit Register → Create.* Set the audit type, scope and date — it starts
   **Scheduled**.
2. **Start** the audit and sign → **In Progress**. Conduct the fieldwork and record findings (attach evidence).
3. **Submit report draft** and sign → **Report Draft**.
4. **Submit for review** and sign → **Under Review**.
5. **Approve.** A Manager (not the Auditor) reviews and **Approves**, signing → **Approved**.
6. **Close** and sign → **Closed**.

**Handling exceptions.**
- **Send back:** a Manager can **Return for Revision** (Under Review → Report Draft) or **Return to Review**
  (Approved → Under Review). A reason is required and signed.
- **Correcting details:** edit is blocked once the audit is Under Review, Approved or Closed.
- **Re-opening:** an Administrator can administratively re-open a Closed audit back to Approved.
- **Removing a record:** Administrator only; soft delete.

**Notes.** Separation of duties is clear: the **Auditor** conducts the audit and can drive it up to *Under
Review*, but **cannot Approve or Close** — those are reserved for a Quality Manager/Administrator, keeping the
approver independent of the auditor.

---

### 5.3 Risk

**Purpose & intent.** The Risk module records the identification, assessment, mitigation and closure of quality
risks, with attachments and a full history.

**Who can do what** (Read-Only views only; every transition below is electronically signed):

| Action | Who can do it |
|---|---|
| Create / Edit a risk | Any signed-in user except Read-Only (broad — includes Quality, Regulatory, Trainer, Auditor) |
| Submit for assessment; submit for review | Any signed-in user except Read-Only |
| **Accept assessment; Close; Return** | **Admin, Quality Manager, Authorised Person** |
| Delete | **Administrator** |
| Administratively re-open | **Administrator** |

**Record lifecycle.** `Identification → Pending Assessment → Mitigation → Pending Review → Closed`.

| Step | Move | Who |
|---|---|---|
| Submit for assessment | Identification → Pending Assessment | any non-Read-Only |
| Accept assessment | Pending Assessment → Mitigation | **Admin/QM/AP** |
| Submit for review | Mitigation → Pending Review | any non-Read-Only |
| Close | Pending Review → Closed | **Admin/QM/AP** |

**Step-by-step walkthrough.**
1. **Identify.** *Quality Assurance → Risk Register → Create.* Describe the risk. It starts in **Identification**.
2. **Submit for assessment** and sign → **Pending Assessment**.
3. **Accept assessment.** A Manager/AP accepts, starting mitigation, and signs → **Mitigation**.
4. **Record mitigation, then submit for review** and sign → **Pending Review**.
5. **Close.** A Manager/AP reviews and **Closes** the risk, signing → **Closed**.

**Handling exceptions.**
- **Send back:** a Manager/AP can **Return** from Pending Assessment to Identification, or from Pending Review
  to Mitigation (signed).
- **Re-opening:** an Administrator can administratively re-open a Closed risk back to Mitigation.
- **Removing a record:** Administrator only; soft delete.

**Notes.** Anyone (except Read-Only) can raise and progress a risk, but **acceptance and closure are reserved for
Admin/Quality Manager/Authorised Person** — broad participation, controlled decisions.

---

## 6. Quality Operations

### 6.1 Suppliers

**Purpose & intent.** The Suppliers module maintains approved and qualified suppliers through assessment,
approval, qualification, periodic re-qualification, suspension and cancellation — the backbone of a controlled
supply chain.

**Who can do what** (Read-Only views only; every transition below is electronically signed):

| Action | Who can do it |
|---|---|
| Create / Edit a supplier (Edit while *Pending*) | Admin, Quality Manager, Quality User, Authorised Person, Operations User, Commercial User |
| Start assessment | Admin, Quality Manager, Quality User, Authorised Person |
| Add / remove addresses (while *Pending*) | Admin, Quality Manager, Quality User, Authorised Person, Operations User, Commercial User |
| Upload / remove supplier documents (assessment stages only) | Admin, Quality Manager, Quality User, Authorised Person, Operations User, Commercial User |
| Link / unlink quality records (assessment stages only) | Admin, Quality Manager, Authorised Person |
| **Approve; Qualify; Suspend; Cancel; Reactivate; Return to assessment; Start New Review** | **Admin, Quality Manager, Authorised Person** |
| Delete (early states) | **Administrator** |

**Record lifecycle.** Main path: `Pending → Under Assessment → Approved → Qualified`. Exception/terminal states:
`Pending Suspension → Suspended`, `Pending Cancellation → Cancelled`, and `Rejected`.

| Step | Move | Who |
|---|---|---|
| Start assessment | Pending → Under Assessment (or → Pending Suspension / Pending Cancellation when recommending) | Admin/QM/QU/AP |
| Approve | Under Assessment → Approved | **Admin/QM/AP** |
| Qualify | Approved → Qualified | **Admin/QM/AP** |
| Return to assessment | Under Assessment / Pending Suspension / Pending Cancellation → Pending | **Admin/QM/AP** |
| Suspend | Approved / Qualified / Pending Suspension → Suspended | **Admin/QM/AP** |
| Cancel | Approved / Qualified / Pending Cancellation → Cancelled | **Admin/QM/AP** |
| Reactivate | Suspended → Pending (approval cleared) | **Admin/QM/AP** |
| Start New Review | Approved / Qualified / Suspended / Cancelled → Pending | **Admin/QM/AP** |

The system accepts only these moves; any other transition is refused, whatever buttons are shown.

**Step-by-step walkthrough.**
1. **Add the supplier.** *Quality Operations → Supplier Register → Create.* Capture the supplier details — it
   starts **Pending**.
2. **Start assessment** and sign → **Under Assessment**. Record the qualification assessment (attach evidence).
3. **Approve.** A Manager/AP **Approves** the supplier and signs → **Approved**. Approval needs a **future next
   review date** (from the review frequency, or set in Edit for *Discretional*). The approval dialog also offers
   an optional **Approval Expiry** date — for a time-limited approval — which must be a future date and needs a
   reason. Left blank, the approval stands until the next review.
4. **Qualify** (where a further qualification step applies) and sign → **Qualified**. Qualifying also needs a
   future next review date, and may set its own Approval Expiry (with reason), replacing the one set at
   approval.
5. **Maintain.** Keep the supplier current through **re-qualification reviews** — see *Review cycle & history*
   below.

**Review cycle & history.** A qualified supplier is kept current by re-qualification reviews, which may be
*scheduled* or *unscheduled*:

- **Scheduled.** When a supplier is approved, the system sets its **next review date** from the chosen **review
  frequency** — *Annual*, *Biannual*, *Three-yearly*, or *Discretional* (a date you set manually). The home
  dashboard flags suppliers whose review is **overdue** or **due within 30 days**, so reviews are prompted
  rather than relying on memory.
- **Unscheduled.** A review can be started at any time in response to an event; its **trigger** is recorded as
  *Deviation*, *Complaint*, *Audit*, *Risk*, *Change* or *Other* (as opposed to *Scheduled*).

Starting a review (**Start New Review** on the supplier) records the trigger and a summary, then returns the
supplier to **Pending** so it runs through the assessment → approval → qualification cycle again; the next
review date is recalculated from the frequency when it is re-approved, and **Last Review** shows the date the
review started. A review can be started from Approved, Qualified, Suspended or Cancelled.

**Approval stays valid during a re-review.** While an Approved or Qualified supplier is being re-reviewed, it
keeps its **standing approval** and may still be used. The supplier page shows a *Re-review in progress* banner
(trigger, start date, current stage), the register shows *Re-review: <stage>*, and the dashboard lists it under
**Re-reviews in Progress**. Any Approval Expiry still applies.

**Approval expiry.** When an Approval Expiry is set, the supplier page and dashboard warn **30 days** before it
and flag it once **expired**. The status does not change automatically — start a review or suspend the supplier.

**Documents and quality records are locked outside assessment.** Supplier documents and linked quality records
can be changed only while the supplier is Pending, Under Assessment, Pending Suspension or Pending Cancellation.
Once it is Approved, Qualified, Suspended or Cancelled they are read-only; start a new review (or reactivate a
suspended supplier) to change them. Uploads, deletions, links and unlinks are recorded in the supplier's audit
trail; removing a document asks for a reason and keeps it under **Deleted documents**.

**How previous instances are kept.** Each completed cycle is preserved as a **snapshot** — a full, read-only
copy of the supplier's state at that point (status, addresses, documents and linked quality records), together
with the review trigger, who reviewed it, and the next-review date that applied. The **Review History** page
lists every snapshot newest-first; open one to see exactly what the supplier looked like at that cycle. The live
record always shows the current cycle, while the snapshots form the inspection-ready trail of prior
re-qualifications.

**Handling exceptions.**
- **Send back:** **Return to Assessment** sends the supplier back to *Pending* for rework (signed).
- **Suspend / Cancel:** a Manager/AP can suspend or cancel a supplier (a reason is required and signed); these
  may route via *Pending Suspension* / *Pending Cancellation* when an assessment is needed first.
- **Reactivate:** a suspended supplier can be **Reactivated** back to *Pending* (signed); its previous approval
  is cleared, so it must be re-assessed and approved before use.
- **Removing a record:** Administrator only, early states, soft delete.

**Notes.** Creating suppliers and maintaining their addresses and documents is open to operational and
commercial roles, but every
**qualification, approval, suspension and cancellation decision is reserved for Admin/Quality
Manager/Authorised Person**. A supplier record can be linked to the **quality records raised against it** —
deviations, complaints, CAPAs, changes, risks and audits — giving a single view of a supplier's quality
history (see §2.8).

---

### 6.2 Maintenance

**Purpose & intent.** The Maintenance module schedules and records equipment maintenance and calibration. It
separates **doing the work** (operations) from **reviewing it** (quality), automatically schedules the next
occurrence on review, and flags overdue items.

**Who can do what** (Read-Only views only; the work transitions are electronically signed):

| Action | Who can do it |
|---|---|
| Create / schedule | Broad (Quality, Authorised Person, Auditor, Trainer, Operations User) |
| Edit | Admin, Quality Manager, Authorised Person |
| **Start work; complete work** | **Admin, Quality Manager, Quality User, Authorised Person, Operations User** |
| **Review work** (and auto-schedule next); suspend; reactivate | **Admin, Quality Manager, Authorised Person** |
| Delete | **Administrator** |
| Administratively re-open | **Administrator** |

**Record lifecycle.** `Scheduled → In Progress → Completed → Reviewed → Approved`, plus **Overdue** (auto-flagged
when past due) and **Suspended** (on hold).

| Step | Move | Who |
|---|---|---|
| Start work | Scheduled/Overdue → In Progress | Admin/QM/QU/AP/Operations |
| Complete work | In Progress → Completed | Admin/QM/QU/AP/Operations |
| Review work | Completed → Reviewed (next occurrence auto-scheduled) | **Admin/QM/AP** |

**Step-by-step walkthrough.**
1. **Schedule.** *Quality Operations → Maintenance Register → Create.* Set the equipment, task and due date — it
   starts **Scheduled** (and becomes **Overdue** automatically if the due date passes).
2. **Start work.** Operations (or quality) **Start** the maintenance and sign → **In Progress**.
3. **Complete work** and sign → **Completed** (attach calibration certificates etc.).
4. **Review.** A Manager/AP **Reviews** the completed work and signs → **Reviewed**; the next occurrence is
   scheduled automatically.

**Handling exceptions.**
- **Send back:** **Send Back to Scheduled / In Progress** (operations roles) or **Send Back to Completed**
  (Manager/AP) returns the record for rework (signed).
- **Suspend / Reactivate:** a Manager/AP can put maintenance on hold and bring it back (signed).
- **Re-opening:** an Administrator can administratively re-open a record back to Reviewed.
- **Removing a record:** Administrator only; soft delete.

**Notes.** Operations staff carry out the work (Start/Complete); **the quality review and approval are reserved
for Admin/Quality Manager/Authorised Person** — execution is separated from quality oversight. A maintenance
record can be linked to the **deviation(s) and CAPA(s)** that prompted or resulted from it; the linked records
appear under *Linked Records* on the maintenance (see §2.8).

---

### 6.3 Batch Release

**Purpose & intent.** Batch Release is the formal disposition of product batches, with a legally-defined
**Authorised Person (AP)** certification step (PIC/S Annex 16). Quality prepares the dossier, operations enter
test data, and the AP makes the certification decision. Released records are locked.

**Who can do what** (Read-Only views only; every transition below is electronically signed):

| Action | Who can do it |
|---|---|
| Create / Edit; enter checklist, test results, specs, documents, cross-references | Admin, Quality Manager, Quality User, Authorised Person, **Operations User** |
| Submit for review; submit to AP | Admin, Quality Manager, Quality User, Authorised Person |
| Place on hold; lift hold | Admin, Quality Manager, Authorised Person |
| **Certify; Reject; Issue Certificate** | **Admin, Authorised Person only** |
| Confirm release | Admin, Authorised Person, Quality Manager |
| Close | Admin, Quality Manager, Quality User, Authorised Person |
| Cancel | Admin, Quality Manager, Quality User |
| Administratively re-open | **Administrator** |

**Record lifecycle.** `Draft → Under Review → Pending AP Decision → Certified → Released`, with branches **On
Hold** and **Rejected**, and terminal states **Closed** and **Cancelled**. From *Pending AP Decision* the AP can
move the record to **On Hold**, **Certified**, **Rejected**, or back to **Under Review**.

| Step | Move | Who |
|---|---|---|
| Submit for review | Draft → Under Review | Admin/QM/QU/AP |
| Submit to AP | Under Review → Pending AP Decision | Admin/QM/QU/AP |
| **Certify** | Pending AP Decision → Certified | **Admin/AP** |
| **Reject** | Pending AP Decision → Rejected | **Admin/AP** |
| Confirm release | Certified → Released | Admin/AP/QM |

**Step-by-step walkthrough.**
1. **Create the release record.** *Quality Operations → Release Register → Create.* It starts **Draft**.
2. **Build the dossier.** Enter the checklist, test results and specifications (operations and quality can enter
   data); attach documents. Import specs from the product where available.
3. **Submit for review** and sign → **Under Review**; then **Submit to AP** and sign → **Pending AP Decision**.
4. **AP decision.** The **Authorised Person** reviews and either **Certifies** (→ Certified) or **Rejects**
   (→ Rejected), signing the decision. The AP can also place the record **On Hold** or send it back to review.
5. **Release.** After certification, **Confirm Release** and sign → **Released**. Issue the **release
   certificate(s)** — CoA / CoC, see below — then **Close** when complete.

**Release certificates (CoA / CoC).** The formal release documentation is generated from the record once a batch
is **Certified** (or **Released**):

- **Types.** A **Certificate of Analysis (CoA)** — product and batch details with the **test results against
  their specifications**; a **Certificate of Conformity (CoC)** — the conformance statement; or a **Combined**
  CoA + CoC.
- **Who & how.** Only an **Administrator or the Authorised Person** can issue a certificate, and issuance is
  **electronically signed**. Each certificate is given a unique **certificate number**, and the AP can add a
  **certification/issuance statement** in their name.
- **Point-in-time snapshot.** Issuing freezes a **snapshot** of everything the certificate shows — product and
  batch details, manufacturing and packaging sites with their GMP references, testing laboratories, the
  releasing company and its licence reference, the test results, and the AP's statement — and renders it to a
  **PDF** that can be viewed and downloaded. Because the snapshot is frozen, the certificate keeps reflecting the
  data as it stood at issue even if the record is later re-opened.
- **Re-issue & supersession.** Issuing a new certificate of the same type **supersedes** the previous one; the
  earlier certificate is retained (marked *Superseded*, with a version number) so the certificate history stays
  auditable.

**Handling exceptions.**
- **Hold:** **Place On Hold / Lift Hold** (Manager/AP) pauses and resumes the disposition (signed).
- **Send back:** **Return for Review** (Quality) or **Return to Under Review** (AP); a rejected batch can be
  re-entered to review via **Return From Rejection** (AP override).
- **Cancel:** the record can be **Cancelled** (Quality), or **Administratively re-opened** by an Administrator.

**Notes.** This is the strictest separation of duties in the system: **only an Administrator or the Authorised
Person may Certify or Reject** a batch. Quality staff prepare; operations enter test data; the AP certifies.

---

## 7. Regulatory

> **Access pattern for the whole Regulatory group.** Viewing the regulatory registers is open broadly (most
> roles can read them). **Performing actions — creating, editing and moving records through their workflow — is
> restricted to the regulatory roles: Administrator, Regulatory Manager and Regulatory User** (Signal Review and
> Periodic Reports also allow Quality Manager). Read-Only can view but never change. All workflow transitions are
> electronically signed.

### 7.1 Pharmacovigilance — Adverse Events

**Purpose & intent.** Manages adverse-event safety cases from intake through medical review, approval and
closure. It generates the internationally recognised **CIOMS** safety form and records regulatory (e.g. TGA)
submissions. Adverse-event records are locked once finalised, and can be linked to complaints.

**Who can do what.** Viewing: broad. **All case actions: Administrator, Regulatory Manager, Regulatory User.**
Delete of supporting attachments retains the file (soft delete).

**Record lifecycle.** `Draft → Under Medical Review → Pending Approval → Approved → Closed`, with **Voided** as an
exception/void outcome.

| Step | Move | Who |
|---|---|---|
| Submit for review | Draft → Under Medical Review | Admin/RegM/RegU |
| Complete evaluation | Under Medical Review → Pending Approval | Admin/RegM/RegU |
| Approve | Pending Approval → Approved | Admin/RegM/RegU |
| Close case | Approved → Closed | Admin/RegM/RegU |

**Step-by-step walkthrough.**
1. **Log the case.** *Regulatory → Pharmacovigilance → Adverse Events → Create.* Capture the patient, reporter,
   product and event details. It starts in **Draft**.
2. **Submit for medical review** and sign → **Under Medical Review**. Record the medical assessment.
3. **Complete the evaluation** and sign → **Pending Approval**.
4. **Approve** and sign → **Approved** (generate the CIOMS form; log any regulatory submission).
5. **Close the case** and sign → **Closed**.

**Handling exceptions.**
- **Send back:** **Send Back** returns the case to an earlier stage for rework (signed).
- **Reject / Void:** a case can be rejected from Pending Approval, or **Voided** where it should not proceed.
- **Linking:** link the case to a related complaint for traceability. A case also carries its **suspect
  product(s)** and can reference a **parent case** where it follows on from an earlier adverse event.

**Notes.** Because the record locks when finalised, post-approval changes follow the controlled regulatory
workflow rather than a free edit. (A known limitation when editing a case that already has child records is
tracked separately by the project team.)

---

### 7.2 PV Literature Monitoring

**Purpose & intent.** A register for tracking published-literature monitoring for safety signals. It is a simple
log — there is no multi-step approval workflow.

**Who can do what.** Viewing: broad. **Create / Edit / Delete: Administrator, Regulatory Manager, Regulatory
User.** Every entry is audit-logged.

**Step-by-step walkthrough.** *Regulatory → Literature Monitoring → Create.* Record the literature reference and
your monitoring assessment, and save. Edit or remove entries as the monitoring record requires. Entries document
ongoing surveillance and can support signal and case work.

**Notes.** No states or transitions — this is a record-keeping register, not a workflow.

---

### 7.3 PV Signal Review

**Purpose & intent.** Manages the formal review and disposition of potential safety signals.

**Who can do what.** Viewing: broad. Create/Edit: Admin/RegM/RegU. **Moving a signal through its workflow:
Administrator, Quality Manager, Regulatory Manager, Regulatory User.**

**Record lifecycle.** `Open → Under Review → {Confirmed | Refuted} → Closed`.

**Step-by-step walkthrough.**
1. **Open a signal.** *Regulatory → Signal Reviews → Create.* It starts **Open**.
2. **Move to Under Review** and assess the signal.
3. **Confirm or Refute** the signal based on the review. (If a confirmed signal needs action, use **Create
   CAPA** to raise and link one.)
4. **Close** the signal review.

**Notes.** Confirmation/refutation and closure are recorded with the actor and time; signals can draw on
literature and adverse-event data. A signal review can raise a single linked **CAPA** when a confirmed signal
warrants a corrective or preventive action.

---

### 7.4 PV Periodic Reports (PSUR)

**Purpose & intent.** Manages periodic safety reports (PSUR/PBRER) — preparation, review, submission and
acceptance — with linked adverse-event cases, attachments and deadline monitoring.

**Who can do what.** Viewing: broad. Create/Edit/Link cases: Admin/RegM/RegU. **Moving a report through its
workflow (including Submit and Accept): Administrator, Quality Manager, Regulatory Manager, Regulatory User.**
Transitions are electronically signed; attachments are retained on deletion (soft delete).

**Record lifecycle.** `In Preparation → Under Review → Submitted → Accepted`.

**Step-by-step walkthrough.**
1. **Create the report.** *Regulatory → PSUR / Periodic Reports → Create.* Link the relevant adverse-event cases and
   attach the report. It starts **In Preparation**.
2. **Move to Under Review** and review the content.
3. **Submit the report** (to the authority) and sign → **Submitted**.
4. **Accept** once acknowledged → **Accepted**.

**Notes.** Deadline monitoring helps ensure periodic reports are not missed.

---

### 7.5 Regulatory Intelligence

**Purpose & intent.** Tracks product registrations and applications across their full regulatory life — from
strategy and dossier preparation, through submission and authority review, to approval, conditions, variations
and renewals. Approved records are locked. The module also manages conditions of approval, agency milestones
(with templates), variations, renewals, access pathways and periodic reports.

**Who can do what.** Viewing: broad. **All actions and transitions: Administrator, Regulatory Manager,
Regulatory User.** Delete: Administrator. All transitions are electronically signed.

**Record lifecycle (main path).** `Strategy → Preparation → Submitted → Under Review → Approved`. From **Under
Review**, the authority decision can lead to **Approved**, **Conditions Active** (when conditions apply),
**Rejected**, a **Clock Stop** (review paused), or **Withdrawn**. After **Approved**, the record can move to
**Variation Required** (a post-approval change), **Renewal Due** (approaching expiry), **Suspended** or
**Withdrawn**.

| Key step | Move | Who |
|---|---|---|
| Prepare dossier | Strategy → Preparation → Submitted | Admin/RegM/RegU |
| Authority review | Submitted → Under Review | Admin/RegM/RegU |
| Approve | Under Review → Approved (locks) | Admin/RegM/RegU |
| Manage conditions | Under Review → Conditions Active → Approved (when all conditions closed) | Admin/RegM/RegU |
| Variation | Approved → Variation Required → Under Review/Approved | Admin/RegM/RegU |
| Renewal | Approved → Renewal Due → Submitted | Admin/RegM/RegU |

**Step-by-step walkthrough.**
1. **Start the application.** *Regulatory → RI Register → Create.* It starts in **Strategy**.
2. **Prepare** the dossier (→ Preparation), then **Submit** to the authority (→ Submitted).
3. **Under Review.** Track the authority's review; add milestones (manually or from an agency template) and
   record any clock stops.
4. **Decision.** **Approve** (→ Approved, record locks) or record **Conditions Active** if conditions apply —
   manage and close each condition (a condition can raise a CAPA), then approve once all are closed.
5. **Maintain.** Raise **Variations** for post-approval changes and **Renewals** as expiry approaches — each
   re-enters the review cycle and stamps an approval snapshot for that cycle.

**Handling exceptions.**
- **Withdraw / Suspend / Reject:** these require a documented reason and are signed; they move the record to the
  corresponding state.
- The full, exact list of permitted moves is in **Appendix B — State-transition reference**.

**Notes.** A registration's entire regulatory history — every cycle, condition and supporting document (with its
integrity fingerprint) — is preserved, supporting periodic evaluation and inspection.

---

### 7.6 Licences & Certifications

**Purpose & intent.** Tracks the company's own licences and certificates through their life — application,
assessment, issue, renewal, variation, suspension, withdrawal and expiry — including conditions, dependencies on
regulatory applications, and lead-time renewal alerts. Records lock when issued/expired/suspended/withdrawn.

**Who can do what.** Viewing: broad. **All actions and transitions: Administrator, Regulatory Manager,
Regulatory User.** Delete: Administrator. All transitions are electronically signed.

**Record lifecycle (main path).** `Draft → Applied → Under Assessment → Issued / Active`. From **Issued/Active**,
the record can move to **Renewal Due**, **Expired**, **Variation Required**, **Suspended** or **Withdrawn**. A
record can also be marked **Not Applicable** (with justification) where a licence is confirmed not required.
Active records carry any post-issue **conditions** as a flag while remaining Issued/Active.

| Key step | Move | Who |
|---|---|---|
| Apply | Draft → Applied | Admin/RegM/RegU |
| Assessment | Applied → Under Assessment | Admin/RegM/RegU |
| Issue | Under Assessment → Issued/Active (locks) | Admin/RegM/RegU |
| Renew | Issued/Active → Renewal Due → Applied | Admin/RegM/RegU |
| Suspend / Withdraw | Issued/Active → Suspended / Withdrawn (reason required) | Admin/RegM/RegU |

**Step-by-step walkthrough.**
1. **Create the record.** *Regulatory → LC Register → Create.* It starts in **Draft**.
2. **Apply** (→ Applied), then move to **Under Assessment** while the authority/cert body reviews.
3. **Issue** the licence (→ Issued/Active; the record locks). Record any conditions; complete or waive each. A
   condition can **raise a CAPA** (**Raise CAPA**) where it needs a tracked corrective action.
4. **Maintain.** Link dependencies on regulatory applications; raise **Variations**; initiate **Renewal** as
   expiry approaches (lead-time alerts warn you in advance).

**Handling exceptions.**
- **Suspend / Withdraw:** require a documented reason and signature; the record locks.
- **Reinstate:** a suspended record re-enters via *Applied → Under Assessment → Issued/Active* (each approval
  cycle is snapshotted).
- **Not Applicable:** requires a documented justification.
- The full, exact list of permitted moves is in **Appendix B — State-transition reference**.

**Notes.** Lead-time monitoring is designed to ensure the business never unknowingly operates on a lapsed
licence.

---

## 8. Documents & Training

### 8.1 Document Management

**Purpose & intent.** Controls the lifecycle of quality documents (SOPs, policies, etc.) and ties document
approval to **mandatory training**: a document only becomes effective once the people who must be trained on it
have completed that training. Effective documents are published to a company-wide **Document Library**, and every
download is watermarked with the document's status.

**Who can do what** (this module uses an author-based model; Read-Only views only; key actions are signed):

| Action | Who can do it |
|---|---|
| Create a document | Any signed-in user (you become the document's **author**) |
| Edit a document | The document's **author**, or an Administrator |
| Submit for review | The author / Administrator |
| **Approve; Reject; Issue** | **Quality Manager, Authorised Person or Administrator** |
| Return for rework | Quality Manager / Authorised Person / Administrator |
| Obsolete | The author / Quality Manager / Authorised Person / Administrator |
| Initiate periodic review | Quality Manager / Authorised Person / Administrator |
| Reassign author (in-progress drafts) | Quality Manager / Authorised Person / Administrator |
| Delete | Administrator |

**Record lifecycle.** `Draft → Under Review → Approved → Effective`, plus **Rejected** and **Obsolete**.

| Step | Move | Who |
|---|---|---|
| Submit for review | Draft → Under Review | Author/Admin |
| Approve (assigns training) | Under Review → Approved | **QM/AP/Admin** |
| Return for rework | Under Review → Draft | QM/AP/Admin |
| Reject | Under Review → Rejected | **QM/AP/Admin** |
| Issue (manual) | Approved → Effective | **QM/AP/Admin** |
| Becomes effective automatically | Approved → Effective when all assigned training is complete | system |
| Initiate periodic review | Effective → Under Review | QM/AP/Admin |
| Obsolete | Approved/Effective → Obsolete | Author/QM/AP/Admin |

**Step-by-step walkthrough.**
1. **Draft.** *Documents → Document Register → Create.* Author the document and upload the file. It starts in
   **Draft**; as its author you can edit it.
2. **Submit for review** and sign → **Under Review**.
3. **Approve.** A Quality Manager/Administrator **Approves** the document and signs. During approval, **training
   is assigned** to the relevant groups (by role or named users), as read-and-understand or competency
   assessment, with a due date. The document moves to **Approved**.
4. **Training completes.** As assigned people complete their training (see §8.2), the document **automatically
   becomes Effective**. A Quality Manager can also **Issue** it manually to make it Effective despite
   outstanding training.
5. **Use.** Effective documents appear in the **Document Library** for everyone to read; downloads are
   watermarked "Approved copy — valid at time of download".

**Versions, periodic review & history.** A document keeps a stable **document number** (its ID) across every
revision, plus a **version number** that advances each time it is revised. All versions sharing that number are
retained and listed newest-first in the document's **version history**, so superseded versions are never lost
and you can always see what was effective at any point.

- **How a review is prompted.** If a document is marked for **periodic review**, a **review-due date** is set
  when it is approved. The home dashboard then flags documents whose review is **overdue** or **due within 30
  days**, so the cycle is prompted rather than relying on memory.
- **Running a review / revision.** A Quality Manager, Authorised Person or Administrator uses **Initiate periodic review** on the
  Effective document (available once its review-due date has passed). This opens a **new version** as a draft
  and runs it through Draft → Under Review → Approved/Effective again. While in draft the new version carries a
  minor number; on approval it is rounded up to the next whole version, the newly effective version supersedes
  the previous one, and the previous version is kept in the history.

**Handling exceptions.**
- **Return for rework:** a reviewer can send the document back to Draft for revision (signed).
- **Reject:** a document can be rejected during review.
- **Periodic review:** see *Versions, periodic review & history* above.
- **Obsolete:** supersede or retire a document; this cancels any outstanding training for it.
- **Author unavailable (e.g. left the company).** A draft is editable only by its author or a Quality
  Manager/Administrator, so work never becomes stranded. To hand a draft over cleanly, a Quality Manager or
  Administrator uses **Reassign Author** on the document (*Workflow Actions*): pick the new author, give a
  reason, and sign. Only **in-progress** documents (Draft or Under Review) can be reassigned — an
  approved/effective version's authorship is fixed — and the handover is recorded in the audit log
  (*"Author reassigned from … to …"*). Reassign to someone **other than** the intended approver to preserve
  separation of duties. Departing users are normally **deactivated, not deleted**, so existing records and
  signatures stay intact.

**Notes.** The approval and issue steps are reserved for Quality Manager/Authorised Person/Administrator; authors manage their own
drafts. Coupling effectiveness to training completion is the core control of this module.

---

### 8.2 Training

**Purpose & intent.** Manages training — both **read-and-understand** acknowledgements and **competency
assessments** — most often created automatically when a document is approved. Each assigned person's progress is
tracked individually and acknowledged with an electronic signature; a personal **My Training** view shows
everyone what they owe, and compliance views let managers see who is outstanding.

**Who can do what** (Read-Only views only):

| Action | Who can do it |
|---|---|
| Create / Edit / Complete / Cancel a training | Admin, Quality Manager, Regulatory Manager, Trainer, Authorised Person |
| **Acknowledge reading** (sign off your own training) | The **assigned trainee** (own record only) |
| **Verify competency** (confirm a competency assessment) | **Admin, Quality Manager, Trainer** |
| Delete | **Administrator** |
| Administratively re-open | **Administrator** |

**Two lifecycles.**
- **Each trainee's record:** `Scheduled → In Progress → [Pending Trainer Confirmation] → Completed` (or *Failed*,
  *Cancelled*).
- **The overall training:** `Scheduled → Completed` once everyone assigned has finished.

**Step-by-step walkthrough.**
1. **Assignment.** Training is usually created automatically when a document is approved (or created manually by a
   trainer/manager). Each assigned person sees it in **Training → My Training**.
2. **Acknowledge (read-and-understand).** The trainee opens their assignment, reads the material and
   **Acknowledges Reading**, signing electronically. Their record moves to **Completed**.
3. **Competency assessment.** For competency-type training, acknowledging moves the record to **Pending Trainer
   Confirmation**; a **Trainer** (or Quality Manager/Administrator) then **Verifies Competency**, marking it
   **Completed** or **Failed**.
4. **Roll-up.** When all assigned people are **Completed**, the overall training is **Completed** — and any linked
   document automatically becomes **Effective**.

**Handling exceptions.**
- **Cancel:** a training can be cancelled (→ Cancelled).
- **Failed:** a competency assessment can be recorded as not met.
- **Re-opening:** an Administrator can re-open a completed training (it returns to Scheduled and the completion
  is cleared, but the prior completion remains in the audit trail).

**Notes.** A trainee can only act on **their own** assignment — no one can acknowledge training on someone else's
behalf. Trainers and managers confirm competency; administrators manage exceptions.

---

## 9. Reference / Master Data

This covers six registers that underpin the quality records: **Products, Materials, Processes, Assets, Services
and Customers**. They behave identically, so they are documented together.

**Purpose & intent.** Each register holds approved reference records (with their own document library) that other
records refer to — for example a deviation about a product, or a release against a product specification. Records
**lock once approved**, so approved reference data cannot be silently changed.

**Who can do what** (Read-Only views only; these registers do **not** require an electronic signature):

| Action | Who can do it |
|---|---|
| Create / Edit / Submit for review | Quality User, Quality Manager, Administrator |
| Send back to draft | Quality Manager, Authorised Person, Administrator |
| **Approve; Make Obsolete** | **Quality Manager, Authorised Person, Administrator** |
| Upload / remove documents on the record | Quality User, Quality Manager, Administrator |
| Delete record (early states) | Quality Manager, Administrator |

**Record lifecycle.** `Draft → Under Review → Approved → Obsolete`.

| Step | Move | Who |
|---|---|---|
| Submit for review | Draft → Under Review | QU/QM/Admin |
| Approve | Draft/Under Review → Approved (locks) | **QM/AP/Admin** |
| Send back to draft | Under Review → Draft | QM/AP/Admin |
| Make obsolete | Approved → Obsolete | **QM/AP/Admin** |

**Step-by-step walkthrough.**
1. **Create.** *Reference Data → (Products / Materials / …) → Create.* Enter the record's details and attach any
   controlling documents. It starts in **Draft**.
2. **Submit for review** (→ Under Review) where a review step is used.
3. **Approve.** A Quality Manager/Administrator **Approves** the record; it **locks** and is stamped with the
   approver and date.
4. **Maintain.** Edit early-state records as needed; **Make Obsolete** when a record is superseded or retired.

**Per-register notes.** *Products* carry specifications, strengths and types; *Customers* add a dashboard and link
to complaints; *Services, Assets, Processes* and *Materials* carry linked documents and connect to the quality
records that affect them. Behaviour is otherwise identical.

**Versioning & change history.** Each reference record carries a **version number** that advances as it is
revised — a minor increment while in draft, rounded up to the next whole number when it is re-approved. Because
an approved record is **locked**, changing one means unlocking it, editing and re-approving. Unlike controlled
documents, reference data does not keep a separate copy of each prior version; its change history is the
record's **audit log**, which captures every field-level change with who made it and when.

**Notes.** Unlike the quality and regulatory workflows, approving reference data is **role-gated but not
electronically signed** — appropriate to lower-ceremony reference records (a point to capture in SOPs/validation).

---

## 10. Reporting & Oversight

### 10.1 Reports

**Purpose & intent.** A read-only reporting area that draws together information across modules. No records are
changed here.

**Who can do what.** Viewing and exporting is open to Admin, Quality Manager, Quality User, Authorised Person,
Auditor, Trainer and Read-Only.

**Available reports** (each can be exported to Excel):
- **Management Review** — a consolidated quality overview for management-review meetings.
- **Training Compliance** — who is up to date and who is outstanding on training.
- **Overdue Items** — records past their due dates across modules.
- **Supporting Document Expiry** — supporting documents approaching or past expiry.

**How to use.** *Reports → (choose a report).* Apply any filters, review on screen, and **Export** to Excel for
distribution or records.

---

### 10.2 Dashboards

**Purpose & intent.** Most modules open with a **dashboard** — an at-a-glance summary of what is open, overdue or
awaiting action — and the **Home** page aggregates highlights across the system. Dashboards are read-only.

**Who can do what.** Each dashboard follows its module's view access; the **Training dashboard** is limited to
Admin, Quality Manager, Regulatory Manager, Trainer, Authorised Person and Auditor.

**How to use.** Open a module and choose its **Dashboard** view, or start from **Home** for the cross-module
summary. Use the figures to find work needing attention, then click through to the records.

---

## 11. Administration

> Administration is tightly restricted. **Only the Administrator** can create users, manage user roles and change
> system settings; other senior roles get **view-only** access to user and audit information. All administration
> pages are under the **Settings** menu.

### 11.1 Users & Roles

**Purpose & intent.** Create user accounts, view them and their sign-in history, and manage which roles each
user holds.

**Who can do what.**
- **View** the user list and a user's history: Administrator, Quality Manager, Regulatory Manager, Auditor.
- **Create a user** and **manage a user's roles** (assign/remove): **Administrator only**.
- The **Roles** list is view-only (roles are pre-defined in the system, not created through the interface).

**How to use.** *Settings → Users.*
1. **Create a user.** Choose **Create New User**, enter the person's first and last name, a user name, their
   e-mail address and an initial password, and save. The new user must **confirm their e-mail address** before they can sign in: MintQMS
   e-mails them a confirmation link. If e-mail is not yet set up (§11.2), the confirmation page shows the link
   instead, for the Administrator to pass on.
2. **Give the user roles.** Open the user's **Manage Roles**, tick the roles they need (see §3), and save.
   Roles decide what each person can do; give each user only the roles their job needs.
3. **Review.** Open a user to see their details and sign-in history.

**Notes.** Granting/removing roles is the key access-control action and is reserved for Administrators; other
senior roles can review users and their login history but cannot change permissions.

---

### 11.2 System Settings & Release Certification Statements

**Purpose & intent.** Configure system-wide settings — including **retention periods** (which drive disposal
eligibility), password policy and cool-down periods — and the **batch-release certification statements**.

**Who can do what.**
- **System Settings:** **view** — Administrator, Quality Manager, Regulatory Manager, Authorised Person, Auditor;
  **save changes** — **Administrator only**. Changes are logged.
- **Release Certification Statements:** **view** — Administrator, Quality Manager, Auditor; **edit** —
  **Administrator only**. Changes are logged.

**How to use.** *Settings → System Settings* to review configuration; an Administrator makes and saves changes,
which are recorded in the settings audit log. The page covers the organisation's name, address and logo; display
preferences; warning periods and default resolution times; password expiry, password history and idle
time-out; the e-mail (SMTP) server used for account confirmation and password resets; and retention periods.
**Display Preferences → Site Time Zone** sets the time zone in which every date/time is shown — screens, audit
trails, exports and watermarks — and the "today" used for due and overdue flags; stored time-stamps are not
changed. Set it to your site's own time zone (the initial value is Brisbane). Release-certification statement
templates are maintained under *Settings → Release Certification Statements*.

---

### 11.3 Login History

**Purpose & intent.** A read-only record of sign-in activity, separate from the functional audit trails.

**Who can do what.** View access: Administrator, Quality Manager, Regulatory Manager, Auditor.

**How to use.** *Settings → Login History* to review who signed in and when — useful for security oversight
and inspections.

---

### 11.4 Data Disposal

**Purpose & intent.** The Data Disposal module manages **end-of-retention disposal** of records. When a record is
finalised it is stamped with a retain-until date (based on a configurable retention period for its type). After
that date it becomes eligible for disposal here. Disposal does not erase data — it is a controlled **soft
delete** (the record is retained but removed from active views), and every action is permanently logged. It is
found under **Settings → Data Disposal**, alongside the other administrative functions.

This module is **not** a record lifecycle — it is a set of controlled actions performed on records from across
**eleven** modules (CAPA, Deviation, Change, Complaint, Risk, Audit, Maintenance, Training, Document, Regulatory
Intelligence, Licences & Certifications).

**Who can do what** (Read-Only views only):

| Action | What it does | Who | Signed? |
|---|---|---|---|
| View register / audit log / export | See records eligible for disposal and the disposal history | Admin, Quality Manager, Regulatory Manager | — |
| **Set Hold** | Place a retention hold (with reason) to extend retention | Admin, Quality Manager, Regulatory Manager | — |
| **Remove Hold** | Remove a hold; starts a cool-down period before disposal | **Administrator** | **Yes** |
| **Dispose** | Soft-delete the selected eligible records | **Administrator** | **Yes** |

**Step-by-step walkthrough.**
1. **Review eligible records.** *Settings → Data Disposal* lists records past their retain-until date. Records
   under a retention hold or in a cool-down period are shown but cannot yet be disposed.
2. **Apply a hold if needed.** To keep a record longer, **Set Hold** with a documented reason. To later release
   it, **Remove Hold** — this requires your electronic signature and starts a cool-down period before the record
   becomes disposable.
3. **Dispose.** Select the eligible records and choose **Dispose Selected**. Confirm in the dialog, **enter your
   electronic signature (email + password)** and tick the authorisation box. The selected records are
   soft-deleted; any with an active hold are skipped. The disposal — and who signed it — is recorded in the
   disposal audit log.

**Handling exceptions.** Disposal is final-but-recoverable: because it is a soft delete, records can be restored
with direct database access if ever required. Records with active holds or cool-downs are automatically skipped.

**Notes.** Disposal and hold-removal are **Administrator-only** and both require an **electronic signature**;
setting a hold is open to Admin/Quality Manager/Regulatory Manager. The retention periods that drive eligibility
are configured in **Settings → System Settings**.

---

### 11.5 Setting up a new installation

After MintQMS has been installed ([DEPLOYMENT.md](../DEPLOYMENT.md)), an Administrator completes these steps
before anyone else uses it:

1. **Sign in** as `admin@trial.local` with the password chosen at installation. You are asked to set a new
   password immediately.
2. **System Settings** (*Settings → System Settings*): enter the organisation name, address and logo; set the
   site time zone and date format; enter the e-mail (SMTP) server and sender address; review password expiry,
   idle time-out, warning periods, default resolution times and retention periods against your procedures.
3. **Release Certification Statements** (*Settings → Release Certification Statements*): review the wording used
   on batch-release certificates.
4. **Users and roles** (*Settings → Users*): create a personal account for each person (§11.1) and give each the
   roles they need (§3). Create a personal Administrator account for yourself and keep `admin@trial.local` for
   emergencies only.
5. **Reference data** (*Reference Data*): enter your products, materials, processes, assets, services and
   customers, so quality records can be linked to them (§9).
6. **Check** that a test user can sign in, that a password-reset e-mail arrives, and that a test document
   approval produces a signed PDF.

---

## 12. Appendices

### Appendix A — Design notes

Points of behaviour that are deliberate and worth knowing when you write procedures or validate MintQMS.

| Topic | Behaviour |
|---|---|
| Independent approval | The system does not stop the same person from both submitting and giving final approval. If your procedures require a different approver, enforce it through role assignment and review. |
| Regulatory modules (PV, Regulatory Intelligence, Licences & Certifications) | Most roles can view these records; workflow actions are limited to Administrator, Regulatory Manager and Regulatory User. |
| Supplier approval | Approve and Qualify are limited to Administrator, Quality Manager and Authorised Person. |
| Data disposal | Viewing and setting holds: Administrator, Quality Manager, Regulatory Manager. Removing a hold and disposing: Administrator only, with an electronic signature. |
| Users and roles | Creating users and changing roles is Administrator-only; other senior roles can view. |
| Older status values | A few status values (e.g. CAPA "Assessment"/"Accepted", Change "Accepted") exist only on records created before the current workflows. They are kept so those records still display, and are not part of the workflows described in this guide. |

### Appendix B — State-transition reference

The exact states and permitted moves per module. Where a formal state machine exists (Batch Release,
Pharmacovigilance, Regulatory Intelligence, Licences & Certifications, Documents), this is taken from that
definition and is authoritative for testing.

**Deviations:** Identification → Evaluation → Pending Acceptance → Accepted → Resolution → Approved.
Returns: Evaluation→Identification; Pending Acceptance→Evaluation; Accepted→Evaluation; Resolution→Accepted.
Admin re-open: Approved→Resolution.

**Complaints:** Received → Evaluation → Pending Acceptance → Accepted → Resolution → Closed.
Admin re-open: Closed→Resolution.

**CAPA:** Determination → Pending Assessment → Implementation → Pending Approval → Approved.
Returns: to Determination or Implementation. Admin re-open: Approved→Implementation.

**Change Control:** Proposal → Assessment → Pending Assessment → Implementation → Pending Approval → Approved.
Returns: Pending Assessment→Proposal; Pending Approval→Implementation; Implementation→Proposal. Admin re-open:
Approved→Implementation.

**Audits:** Scheduled → In Progress → Report Draft → Under Review → Approved → Closed.
Returns: Under Review→Report Draft; Approved→Under Review. Admin re-open: Closed→Approved.

**Risk:** Identification → Pending Assessment → Mitigation → Pending Review → Closed.
Returns: Pending Assessment→Identification; Pending Review→Mitigation. Admin re-open: Closed→Mitigation.

**Suppliers:** Pending → Under Assessment → Approved → Qualified. Suspend (via Pending Suspension)→Suspended;
Cancel (via Pending Cancellation)→Cancelled; Rejected; Reactivate: Suspended→Pending.

**Maintenance:** Scheduled → In Progress → Completed → Reviewed → Approved; plus Overdue (auto) and Suspended.
Send-backs to Scheduled / In Progress / Completed; Admin re-open → Reviewed.

**Batch Release (state machine):** Draft → Under Review → Pending AP Decision → Certified → Released; terminal
Closed, Cancelled. From **Pending AP Decision**: → On Hold, → Certified, → Rejected, → Under Review. On Hold ↔
Under Review/Draft; Rejected → Under Review (AP override).

**Pharmacovigilance — Adverse Events (state machine):** Draft → Under Medical Review → Pending Approval →
Approved → Closed; Voided. Send-back to a prior stage; Reject from Pending Approval.

**PV Signal Review:** Open → Under Review → {Confirmed | Refuted} → Closed.

**PV Periodic Reports (PSUR):** In Preparation → Under Review → Submitted → Accepted.

**Regulatory Intelligence (state machine — full permitted moves):**
Strategy↔Preparation; Preparation→Submitted; Submitted→{Preparation, Under Review, Withdrawn};
Under Review→{Clock Stop, Approved, Conditions Active, Rejected, Withdrawn}; Clock Stop→Under Review;
Approved→{Variation Required, Renewal Due, Withdrawn, Suspended}; Conditions Active→{Approved, Withdrawn,
Suspended}; Variation Required→{Under Review, Approved}; Renewal Due→{Submitted, Withdrawn};
Suspended→{Under Review, Withdrawn}.

**Licences & Certifications (state machine — full permitted moves):**
Draft→{Applied, Not Applicable}; Applied→{Draft, Under Assessment, Withdrawn};
Under Assessment→{Applied, Issued/Active, Suspended, Withdrawn};
Issued/Active→{Renewal Due, Expired, Variation Required, Suspended, Withdrawn};
Variation Required→{Under Assessment, Issued/Active}; Renewal Due→{Applied, Expired, Suspended};
Expired→Applied; Suspended→Applied; Withdrawn→Draft; Not Applicable→Draft.

**Documents (state machine):** Draft → Under Review → Approved → Effective; plus Rejected and Obsolete.
Approved→Effective is automatic on training completion, or manual via Issue. Effective→Under Review (periodic
review). Approved/Effective→Obsolete.

**Reference data (Products, Materials, Processes, Assets, Services, Customers):** Draft → Under Review → Approved
→ Obsolete. Send back: Under Review→Draft.

**Disposal / Reports / Dashboards / Administration:** no record state machine (action sets / read-only views).

### Appendix C — Glossary

| Term | Meaning |
|---|---|
| **State / status** | A defined stage in a record's lifecycle (e.g. Draft, Approved, Closed). |
| **Transition** | A permitted move from one state to the next (e.g. "Submit for approval"). |
| **Electronic signature** | Re-entering your own login email and password to sign an action; binds signer, meaning and time. |
| **Soft delete** | "Deleting" hides a record from active views but retains it, so the history is preserved. |
| **Immutable / locked** | A finalised record whose content cannot be silently changed; it moves only via signed workflow actions. |
| **Audit trail** | The permanent, tamper-resistant history of every action on a record (who, what, when, what changed). |
| **Retain-until date** | The date a finalised record becomes eligible for disposal, set from a configurable retention period. |
| **Retention hold** | An extension that keeps a record beyond its retain-until date. |
| **Watermark** | Status/identity markings automatically stamped on controlled files when downloaded. |
| **Authorised Person (AP)** | The legally defined role that certifies batch release (PIC/S Annex 16). |
| **CAPA** | Corrective & Preventive Action. |
| **Deviation** | A departure from an approved process, procedure or specification. |
| **PSUR / PBRER** | Periodic safety report submitted to regulators. |
| **CIOMS** | An internationally recognised adverse-event reporting form. |
| **Separation of duties (SoD)** | Splitting a process so that key approvals are made by someone other than the preparer. |
| **Read-Only** | A view-only access level; cannot create or change anything. |

---

*End of guide. This document is generated from a code-level review of MintQMS and reflects the application's
behaviour at the time of writing; verify against the running system during validation.*
