# Changelog

## Unreleased

**Fixes (also in English)**

- Suppliers register: the *Under Assessment* filter always showed an empty list. It searched the
  Approval Status column, but "Under Assessment" is a workflow stage shown in the Workflow column. It now
  filters the Workflow column and finds both first assessments and re-reviews.
- Option names that showed as code fragments now read properly: CoA, CoC (were "Co A", "Co C"),
  CoA Template ("Co ATemplate"), ISO / Private Certification ("ISOPrivate Certification"), GMP Evidence
  Package ("GMPEvidence Package"), TGA On-Site Inspection ("TGAInspection"), sNDA / sBLA ("sNDA_sBLA"),
  SAS Category A / B / C ("SAS_A" …). Stored values are unchanged. Note for audit trails: a supplier
  document upload records the document type's name, so new entries read "(CoA Template)" while earlier
  entries keep "(Co ATemplate)" — history is never rewritten.

**Spanish and Portuguese (Brazil) — shared parts translated**

- Translated: menu bar, page layout, sign-in and account pages, every state/type/role name, the 417
  words and phrases shared across screens (buttons, column headings, common labels), table controls
  (search, paging), built-in validation messages and password rules — 1,035 entries in each language.
  Module-specific page text follows in later releases; until then it shows in English.
- Records never change language: audit trails, record fields, PDFs and stored values stay English
  whatever language the user has chosen (tested).
- The menu bar wraps onto a second row when it does not fit, instead of pushing *Logout* off-screen
  (this also affected English on screens narrower than about 1600 pixels).
- Status filters on registers keep working in every language (tested in a browser on all 15 registers).
- Tools for contributors: `tools/i18n.py` (build/check/report/review) and `tools/i18n_wrap.py`;
  see CONTRIBUTING.md.

**Groundwork for Spanish and Portuguese**

- Interface languages can be offered with the `Localization:Languages` setting (`en`, `es`, `pt-BR`); a
  language menu then appears in the menu bar and on the sign-in page, and the choice is remembered in the
  browser. With English alone (the default) nothing changes on screen.
- The chosen language affects the words shown only. Dates and numbers keep the server's format, so a
  typed number is always read the same way; stored records, audit trails and e-signatures are unchanged.
- ReadOnly users may change their own language; every other change remains refused for them.
- Field labels: "DocType" now reads "Document Type", "HolderType" reads "Holder Type".
- Tests: `tests/e2e/test_language.py` (run as-is, and with `E2E_LANGS=1` against an instance with the
  languages enabled).

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
