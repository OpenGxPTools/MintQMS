# Changelog

## MintQMS 1.2.0 (2026-11-16)

MintQMS now enforces the **four-eyes rule** on signed workflow steps and lets you name **who is responsible for
the next step** of a record, with work lists on the home page, an *Assigned to* column in the registers, a Task
Assignments report and optional e-mails.

**Before you upgrade:** take a backup (DEPLOYMENT.md §5) — this version changes the database (three changes,
applied automatically at start-up: a four-eyes settings field, a TaskAssignments table, an EmailOutbox table with
two e-mail settings). Also keep a copy of the data-protection keys folder (`DataProtectionKeysPath`): the SMTP
password is now encrypted with those keys at the first start. **After the upgrade the four-eyes rule is on**: a
record already half-way through may need a different person for its next acceptance or approval (an
Administrator can turn the rule off in *System Settings → Security Policy*). Assignment e-mails stay **off**
until an Administrator turns them on. The home page's "My Items" cards and the managers' "Pending My Action"
tables are replaced by the *My Tasks / Waiting for my roles / Raised by me* tabs. To go back to 1.1.0, stop the
service, put back the 1.1.0 program and restore the backup taken before the upgrade (DEPLOYMENT.md §6); work
entered after the upgrade is then lost.

**Four-eyes rule on signed workflow steps**

- A review, acceptance, approval, closure or certification must now be signed by someone who did not do the work
  being reviewed — for example, whoever submitted a deviation's evaluation cannot accept it, and a document's
  author cannot approve it. The step is refused, nothing changes, and the message says why ("Four-eyes rule: you
  submitted the evaluation, so another person must sign this step."). The person who raised a record may approve
  or close it when its acceptance was signed by someone else.
- Applies to Deviations, Complaints, CAPA, Change Control, Audits, Risk, Maintenance, Documents and Suppliers
  (the rules are in `Resources/Workflow/workflow-rules.json`, built into the program). For suppliers this includes
  the decision on a recommended suspension or cancellation: whoever recommended it in the assessment cannot also
  decide it. Risk records still in the older *Review* state are covered too.
- **On by default, also on upgrade.** An Administrator can turn it off in *System Settings → Security Policy →
  Enforce Four-Eyes Rule* (logged in the settings audit trail), for a site too small to separate these duties.
  After upgrading, a record already half-way through may need a different person for its next acceptance or
  approval. Database change: one settings field.

**Task assignment: who is responsible for the next step**

- Each record of Deviations, Complaints, CAPA, Change Control, Audits, Risk, Maintenance, Documents and Suppliers
  shows **Responsible for the next step** under its workflow actions, with **Assign**, **Reassign** and
  **Unassign** (optional due date and note). Assigning is information for accountability, not a lock: anyone
  allowed to sign the step still can, and the assignment then ends, recording who actually did it.
- Only people who can do the next step are offered: a role that can sign it, an active account, and not excluded
  by the four-eyes rule (a document's author for "submit for review"; supplier qualification only when required).
- Each signature dialog has an optional **Assign the next step to** list, so the next person can be named while
  signing — no extra step. The person signing is never offered for a step that reviews their own work.
- Who may assign: anyone who can do the stage's next step, and Administrators, Quality Managers and Regulatory
  Managers. Every assignment change is written to the record's audit trail; the history of assignments is kept
  (never deleted). Deactivating a user unassigns their tasks, noted in each record's audit trail.
- Also covers the supplier *Pending Suspension* / *Pending Cancellation* decisions, and records left in older
  states (Risk *Assessment* / *Review*, Maintenance *Overdue*). Starting a new supplier review ends the assignment
  of the stage the supplier leaves.
- **Home page: your work in three tabs.** *My Tasks* (assigned to you, overdue in red, plus trainings you attend),
  *Waiting for my roles* (unassigned records whose next step you may sign, four-eyes respected; Regulatory Managers
  also see adverse events pending approval, confirmed signals and licence renewals here) and *Raised by me*. They
  replace the "My Items" cards and the managers' "Pending My Action" tables.
- **Registers:** an *Assigned to* column (with the due date) and an *Assigned to me* button in all nine registers;
  the column is included in the register's PDF/Excel buttons.
- **Reports → Task Assignments** (Administrator, Quality Manager, Authorised Person, Auditor): open and overdue tasks
  by person, tasks no one is named for by module and stage, filters and Excel export. *Overdue Items* shows who is
  assigned to each record.
- **Assignment e-mails** (System Settings → *Send Assignment E-mails*, off by default; needs SMTP): the person named
  gets an e-mail in the site language with the type of record, number, next step, stage, due date, who assigned it,
  the note and a link (new setting *Site Web Address*) — no record content. E-mails are written to an outbox in the
  same save as the assignment and sent in the background with retries (1, 5, 15, 60, 240 minutes, then failed); the
  settings page shows waiting / sent / failed and the last problem. **Send test e-mail to me** (Administrator,
  logged).
- **SMTP password stored encrypted** (ASP.NET Data Protection, keys in `DataProtectionKeysPath`); an existing plain
  password is encrypted at the first start. After restoring a database without its keys folder, re-enter it.
- Database change: a new TaskAssignments table; a new EmailOutbox table and two settings fields.

**Fixes**

- Maintenance: an Administrator could never re-open a reviewed maintenance record (the re-open required the
  Approved state, which the workflow never reaches). *Administratively Reopen* now appears on a Reviewed record
  and returns it to Completed for re-review, clearing the review and cancelling the next occurrence that was
  scheduled automatically at review — as *Send Back to Completed* does.
- Data Disposal: the page failed to open once a reviewed maintenance record passed its retention date (it looked
  for an approval date, which the maintenance workflow never sets), and likewise for any record with a retention
  date but no closing date. Maintenance records are now listed as *Reviewed* with their review date, and a missing
  date no longer breaks the page.
- Account pages (e-mail, password, two-factor): their form checks loaded scripts that the application does not
  serve, or from an outside website in production; they now use the application's own copies.

**Browser libraries: no code from other websites**

- Charts on the module dashboards, the searchable drop-down lists and the Licences & Certifications calendar
  loaded their libraries from an outside website (cdn.jsdelivr.net) on 21 pages, so they failed without internet
  access and ran code MintQMS did not control. Chart.js 4.4.0, Select2 4.1.0-rc.0 and FullCalendar 6.1.11 are now
  bundled (exact files from the npm registry, fingerprints checked; MIT licences included). MintQMS no longer
  contacts any outside website (apart from your mail server).
- Security updates of two bundled libraries with known vulnerabilities: jQuery Validation 1.17.0 → 1.22.1
  (cross-site scripting and slow-pattern issues) and the older Chart.js 2.3.0 → 2.9.4 (prototype pollution).
- `wwwroot/lib/libraries.json` lists every bundled library (version, licence, source, file fingerprints).
  `tools/check_libraries.py` checks the files are unchanged, that no page loads outside code, and — online —
  newer versions and known vulnerabilities; a GitHub workflow runs it every Monday.

**Documentation**

- User guide 1.2: the four-eyes rule (§2.9) and task assignment, work lists and e-mails (§2.10); the reports list
  is corrected (only Management Review, Training Compliance and Task Assignments export to Excel).
- DEPLOYMENT.md: Site Web Address, test e-mail, and keeping the keys folder with the backups.
- New texts in Spanish and Portuguese (Brazil) are first translations, to be reviewed by native speakers.

**For contributors**

- Who may sign each step, and the four-eyes rules, are in `Resources/Workflow/workflow-rules.json` (embedded); the
  tests check it against the real role checks of every step.
- Tests: `test_workflow_baseline.py` records every signed step of the nine modules and who may sign it (the
  behaviour of 1.1.0, unchanged with the rule off, except the Maintenance re-open fix); `test_four_eyes.py` checks
  every rule (stages the baseline cannot reach are tried live with every role); `test_maintenance_reopen.py`,
  `test_assignment.py`, `test_work_lists.py` (including each register's DataTables column list) and
  `test_email.py` with a small STARTTLS test mail server (`tests/e2e/smtp_test_server.py`; delivery checks need
  `E2E_SMTP_CERT_DIR`, see the test); `test_module_walks.py` walks Batch Release, Licences & Certifications,
  Regulatory Intelligence, pharmacovigilance, Data Disposal and the remaining quality-module actions through
  their pages; `test_libraries.py` guards the bundled libraries.

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
