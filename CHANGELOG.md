# Changelog

## Unreleased

**Four-eyes rule on signed workflow steps**

- A review, acceptance, approval, closure or certification must now be signed by someone who did not do the work
  being reviewed — for example, whoever submitted a deviation's evaluation cannot accept it, and a document's
  author cannot approve it. The step is refused, nothing changes, and the message says why ("Four-eyes rule: you
  submitted the evaluation, so another person must sign this step."). The person who raised a record may approve
  or close it when its acceptance was signed by someone else.
- Applies to Deviations, Complaints, CAPA, Change Control, Audits, Risk, Maintenance, Documents and Suppliers
  (15 steps; the rules are in `Resources/Workflow/workflow-rules.json`, built into the program).
- **On by default, also on upgrade.** An Administrator can turn it off in *System Settings → Security Policy →
  Enforce Four-Eyes Rule* (logged in the settings audit trail), for a site too small to separate these duties.
  After upgrading, a record already half-way through may need a different person for its next acceptance or
  approval. Database change: one settings field.
**Fixes**

- Maintenance: an Administrator could never re-open a reviewed maintenance record (the re-open required the
  Approved state, which the workflow never reaches). *Administratively Reopen* now appears on a Reviewed record
  and returns it to Completed for re-review, clearing the review and cancelling the next occurrence that was
  scheduled automatically at review — as *Send Back to Completed* does.

**Tests**

- `test_workflow_baseline.py` records every signed step of these nine modules and who may sign it (the
  behaviour of 1.1.0, unchanged with the rule off, except the Maintenance re-open fix); `test_four_eyes.py` checks
  every rule; `test_maintenance_reopen.py` the re-open fix.

## MintQMS 1.1.0 (2026-11-15)

MintQMS now runs in **English, Spanish and Portuguese (Brazil)**, and can be used outside Australia.

**Before you upgrade:** take a backup (DEPLOYMENT.md §5) — this version changes the database (three changes,
applied automatically at start-up). After the upgrade the site stays in English until an Administrator
installs and selects another language. To go back to 1.0.1, stop the service, put back the 1.0.1 program and
restore the backup taken before the upgrade (DEPLOYMENT.md §6); work entered after the upgrade is then lost.

**Languages**

- Languages are installed on the server with the `Localization:Languages` setting (`en`, `es`, `pt-BR`; only
  English by default). An Administrator sets the **Site Language** for everyone in *Settings → System
  Settings*. **Let Users Choose Their Language** (off by default) adds a language menu for each user, for
  example an English-speaking auditor at a Spanish-speaking site. Both settings are logged in the settings
  audit trail. ReadOnly users may change their own language; every other change remains refused for them.
- Every module is translated: pages, guidance texts, signature dialogs, option lists, dashboard charts,
  messages, validation errors, sign-in and account pages. Each page declares its language (`<html lang>`).
  The About page stays in English: it holds the licence and disclaimer. A text without a translation is
  shown in English, so a screen never breaks.
- **The language changes what is shown, never what is stored.** Workflow states, options and stored values
  are kept in English and shown translated; free text is kept exactly as typed; the audit trail is written
  in English. Numbers keep the server's format, so a typed number is always read the same way. Dates on
  screens follow the site language ("lunes, 9 de noviembre de 2026"); tables still sort them by date.
- Translations were prepared for this release and reviewed by the maintainer. If your site uses Spanish or
  Portuguese, check the wording — especially signature and workflow steps — as part of your own validation.

**Generated records and issued documents**

- Exports (detail PDF and CSV, Excel registers, reports, the CIOMS-I form) and the text stamped on downloaded
  files are produced in the **site language** at the moment they are made, not in a user's own choice. With
  the site language English they are exactly as before.
- Release certificates, training certificates and attendance lists are now **stored exactly as issued** (site
  language at issue, organisation name and logo embedded, SHA-256 checksum) and always shown from that copy, so
  a later change of language, organisation name, logo, user name or software version never alters them. A copy
  that fails its checksum is not shown, and the event is audited. Certificates issued earlier are fixed once,
  in English, on the first start after upgrading. A training certificate is issued when that trainee
  completes; the attendance list is marked NOT FINAL until the training is completed; re-opening supersedes it.

**Usable outside Australia**

- **Site time zone:** System Settings offers every time zone in the world, grouped by continent (before:
  Australian cities, Auckland and UTC only). A new installation starts on UTC until the Administrator chooses;
  an existing installation keeps the zone it was using, including one that never saved its settings (Brisbane).
- **Adverse-event reporting deadlines** are System Settings (calendar days after Day 0: serious, default 15;
  fatal or life-threatening, default 7) instead of fixed in the program. An existing case keeps its deadline
  when the settings change; it is recalculated only when its Day 0 or seriousness changes, and that change is
  in the case's audit trail.
- **General wording instead of Australian terms**, in every language: *ARTG Number / Reference / Entry* →
  **Registration Number**; *Reportable to TGA, TGA Deadline, TGA Submission…* → **Authority** wording and
  **Reporting Deadline**; *TGA ADRS Reference* → **Authority Case Reference**; *AEMS Portal* → **Authority
  Online Portal**; PSUR *TGA Reference* → **Authority Reference**; *Releasing Company TGA Licence / Clearance
  No.* → **Releasing Company Manufacturing Licence No.**; *Legal ID (ABN/ACN/Other)* → **Company Registration
  / Tax ID**; *GMP Clearance Pathway* → **GMP Evidence Pathway**. Australian-only access pathways are marked
  **(AU)** (SAS A/B/C, Authorised Prescriber, CTN). The default release checklist and the Consumer Health
  certification statement no longer name the TGA. Database fields are unchanged (no data conversion); export
  column headings use the new wording; audit entries written before keep the old field names; certificates
  already issued keep their wording.

**Fixes**

- Training: a trainee could open a "certificate" for training they had not completed; the certificate's "Date of
  Issue" was the day it was viewed; release certificates showed the current organisation name and logo instead
  of those at issue. All fixed by the stored copies above.
- Suppliers register: the *Under Assessment* filter always showed an empty list. It now filters the Workflow
  column and finds both first assessments and re-reviews.
- Excel exports: the Export Info sheet said "Exported On (UTC)" but showed the site time; it now says "Exported
  On". Its note no longer calls MintQMS "a validated QMS" (validation is each user's responsibility).
- CSV exports start with the UTF-8 mark, so Excel shows accented text (e.g. "José") correctly.
- Times labelled "UTC" but shown in the site time zone now name the site's zone (Settings audit log, Data
  Disposal audit log, attachment expiry register).
- Option, type and status names that showed as code now read properly in every module (e.g. "InProgress" →
  "In Progress", "Co ATemplate" → "CoA Template", "sNDA_sBLA" → "sNDA / sBLA", "SAS_A" → "SAS Category A",
  maintenance types such as "Preventive Maintenance"), including dashboard charts, refused workflow steps and
  training reminders. Stored values are unchanged. In audit trails, new entries use the proper name while earlier
  entries keep theirs — history is never rewritten.
- Browser tabs on registers and on create, edit and delete pages read e.g. "Suppliers", "New Asset", "Edit
  Asset" instead of "Index", "Create", "Edit".
- The menu bar wraps onto a second row when it does not fit, instead of pushing *Logout* off-screen.
- Field labels: "DocType" now reads "Document Type", "HolderType" reads "Holder Type".
- Create user page: a leftover developer note no longer links to an ASP.NET setup article.

**For contributors**

- `tools/i18n.py` (build, check, report, review) and `tools/i18n_wrap.py`; see CONTRIBUTING.md.
- Tests: the whole e2e suite runs in any site language (`E2E_LANGS=1`); new suites `test_language`,
  `test_record_language`, `test_workflows_language`, `test_issued_documents`, `test_regional_neutral`.

## MintQMS 1.0.1 (2026-11-15)

Fixes since 1.0.0. No database changes: replace the program files and restart.

- Batch Release: the help buttons in the workflow dialogs open again.
- Audits: the edit page no longer shows a script error at the Scheduled stage.
- Change Control: the Return dialog names the stage the change actually returns to (Assessment).
- CAPA dashboard: the Status Distribution chart is the same size as on the other dashboards.
- User guide: new note in §1 not to use the browser's translate feature for records.

## MintQMS 1.0.0 — first public release (2026-10-26)

MintQMS was originally developed from the free AGPLv3 release of OpenQMS 1.3.0 (see the bottom of this file), which provided
Deviations, CAPA, Change Control, controlled documents, training, products, materials, processes, assets, and
users and roles. Compared with that release, MintQMS adds the following.

**New modules**

- Complaints, Audits, Risk, and Maintenance & calibration.
- Suppliers: approval, optional qualification, approval expiry, periodic re-review and history snapshots.
- Batch Release, with Authorised Person certification and configurable certification statements.
- Pharmacovigilance: adverse events with reporting clocks, literature monitoring, signal review and periodic
  reports (PSUR).
- Regulatory Intelligence (submissions and access pathways) and Licences & Certifications (with calendar and
  renewal alerts).
- Customers and Services master data.
- Reports: management review, training compliance, overdue items and the attachment expiry register.
- Data disposal: retention periods, holds and e-signed disposal.
- System settings: organisation details and logo, e-mail, time zone, warning periods, password rules and
  retention.

**Existing modules extended**

- Deviations, CAPA and Change Control reworked into full signed workflows, with resolution targets, linked
  records and dashboards. Change Control gains an in-system action plan.
- Controlled documents linked to mandatory training, with read-and-understood acknowledgement and
  competency verification.
- Products, Materials, Processes and Assets extended with versioning and linked documents.

**Controls for regulated use**

- Electronic signatures on workflow steps, bound to the signed-in user.
- Field-level audit trails on every module; database triggers make audit entries append-only.
- Locked records after approval; soft deletion everywhere. Deleting an attachment requires a reason, and the
  deleted file stays retrievable, clearly marked.
- SHA-256 fingerprint for every uploaded file, checked on every download.
- Status and identity watermarks on PDF, Word, Excel, OpenDocument and image downloads.
- 11 pre-defined roles with permission checks on every action; a read-only role.
- Password policy, password history, password expiry, account lockout, idle time-out, a single active
  session per user, and login history.
- Times stored in UTC and shown in the site's configured time zone.

**Platform**

- .NET 10 (LTS).
- Works behind a reverse proxy (nginx, Caddy) with HTTPS.
- Data-protection keys can be kept in a configured folder.
- Backup script for the database and uploaded files, with backup status shown in the application.
- Installation & Deployment Guide, User & Operations Guide, and automated end-to-end tests.

---

## OpenQMS release history (C-realize IT Services SRL)

**Release 1.3.0**
- Remove DataType.Date limitation in models
- Change "N/A" in not yet completed fields to "-"
- Simplify Delete views
- Enhance Details views

**Release 1.2.0**
- Migrate from .NET 6.0 to 8.0 and iText 7 to 8
- Switch from SQL Server database to SQLite
- Minor fixes, code quality and style

**Release 1.1.0**
- Enable document files to be saved in the database
- Addition of a Training Attendance Report and a Training Certificate Report
- Changing the license to GNU AFFERO GENERAL PUBLIC LICENSE version 3 to be compatible with other third
  party components
- Minor fixes/uniformizations

**Release 1.0.0**
- First release
