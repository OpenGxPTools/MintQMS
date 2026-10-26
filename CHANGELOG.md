# Changelog

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
