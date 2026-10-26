# MintQMS

**A free, open-source Quality Management System for small pharmaceutical and life-science companies.**

MintQMS keeps your quality records in one place: deviations, complaints, CAPA, change control, audits, risk,
suppliers, batch release, pharmacovigilance, regulatory submissions, licences, controlled documents and
training. It takes each record through a defined, electronically signed workflow and keeps a complete,
tamper-resistant history. It runs in an ordinary web browser and you host it yourself.

> **Disclaimer.** MintQMS is provided free, "as is", without warranty of any kind, under the GNU AGPL
> version 3. It is **not validated** for any regulated use. Each organisation that uses it is solely
> responsible for its own computerised system validation (for example PIC/S Annex 11, GAMP 5,
> 21 CFR Part 11), configuration, data, backups and regulatory compliance. It is not endorsed by, or
> affiliated with, the TGA, any regulator, or C-realize IT Services SRL. See [DISCLAIMER.md](DISCLAIMER.md).

---

## What it does

| Area | Modules |
|---|---|
| Quality Events | Deviations · Complaints · CAPA |
| Quality Assurance | Change Control · Audits · Risk |
| Quality Operations | Suppliers (approval, qualification, periodic review) · Maintenance & calibration · Batch Release (Authorised Person certification) |
| Regulatory | Pharmacovigilance: adverse events, literature monitoring, signal review, periodic reports (PSUR) · Regulatory Intelligence (submissions, access pathways) · Licences & Certifications |
| Documents & Training | Controlled documents (review, approval, effective dates, periodic review) · Training linked to documents, with read-and-understood acknowledgement and competency sign-off |
| Reference data | Products · Materials · Processes · Assets · Services · Customers |
| Oversight | Dashboards per module · Management review, training compliance and overdue-items reports · Excel, CSV and PDF exports |
| Administration | Users and 11 pre-defined roles · System settings · Login history · Retention and controlled disposal |

**Built for regulated work:**

- **Electronic signatures.** Users re-enter their e-mail and password to sign workflow steps; the signature is
  bound to the signed-in user.
- **Audit trails.** Every record keeps an append-only history of who changed what, and when, with field-level
  detail. The database itself blocks edits to audit entries.
- **Locked records.** Approved and closed records cannot be silently changed, only moved on by a defined,
  signed action.
- **Soft deletion only.** Nothing is truly deleted. Deleting an attachment needs a reason, and the deleted file
  stays available, marked as deleted.
- **File integrity.** Every uploaded file is fingerprinted (SHA-256), and a file altered on disk is refused and
  reported.
- **Watermarks.** Downloads of PDFs, Office documents and images carry the document status, who downloaded them
  and when.
- **Retention.** Retention periods are set per record type, with holds and e-signed disposal.
- **Time zone.** Times are stored in UTC and displayed in your site's time zone.

## Who it is for

Small pharmaceutical, biotech and life-science companies that want a capable QMS without a subscription.
They need someone who can run a Linux server (or a virtual machine) and a quality team prepared to validate
and control the system themselves.

## Try it

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

```bash
git clone https://github.com/OpenGxPTools/MintQMS.git
cd MintQMS
SeedAdminPW='Choose-A-Strong-Pw1!' dotnet run
```

Open the address shown in the console. Sign in as `admin@trial.local` with the password you chose; you will be
asked to change it straight away. The password must have at least 10 characters, with upper case, lower case,
a digit and a symbol. Windows users, and anyone installing on a server, should see
[DEPLOYMENT.md](DEPLOYMENT.md).

## Documentation

- **[Installation & Deployment Guide](DEPLOYMENT.md):** trying it locally, installing on a Linux server with
  HTTPS, backups, upgrades and a go-live checklist.
- **[User & Operations Guide](docs/MintQMS_User_and_Operations_Guide.md):** every module, role, workflow state
  and e-signature, with step-by-step walkthroughs.
- **[Changelog](CHANGELOG.md):** what changed in each version.
- **Tests:** [`tests/e2e`](tests/e2e) holds the automated end-to-end tests used during development.

## Feedback and support

MintQMS is a community project, supported on a best-effort basis, with no obligation to provide support.

- **Questions and ideas:** use GitHub Discussions.
- **Bugs and feature requests:** open a GitHub Issue (there are forms to help).
- **Security problems:** report them privately, as described in [SECURITY.md](SECURITY.md), and never in a
  public issue.
- **Contributions:** welcome; see [CONTRIBUTING.md](CONTRIBUTING.md).

## Origins and licence

MintQMS is an independent fork of the free AGPLv3 release of
[OpenQMS](https://github.com/C-realize/OpenQMS) by C-realize IT Services SRL. It has been substantially
extended with new modules, workflows and controls. It is not affiliated with C-realize or derived from its
commercial edition. "OpenQMS" is the name of C-realize's project; MintQMS is a separate project.

Copyright © 2022–2025 C-realize IT Services SRL (original OpenQMS).
Copyright © 2026 MintQMS contributors.

This program is free software: you can redistribute it and/or modify it under the terms of the GNU Affero
General Public License as published by the Free Software Foundation, either version 3 of the License, or (at
your option) any later version. This program is distributed in the hope that it will be useful, but WITHOUT
ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See
the [GNU Affero General Public License](LICENSE) for more details.

If you run a modified version of MintQMS for other people to use over a network, the AGPL requires you to
offer them your modified source code. Set `SourceCodeUrl` (see [DEPLOYMENT.md](DEPLOYMENT.md)) so that the
application's "Source code" link points to it.

### Can my company use MintQMS?

Yes. A plain-English summary for IT and Legal teams follows. It is not legal advice; the [licence](LICENSE)
itself is what applies.

- **Installing and using MintQMS unchanged**, for your own quality system, costs nothing and puts no
  obligation on you. You do not have to publish anything about your company, your data or your
  configuration.
- **Your data stays yours.** Records, documents, users and settings held in MintQMS are not covered by the
  licence and never have to be shared.
- **Configuring it is not modifying it.** Settings, roles, users, reference data and your logo are all
  changed through the application, not by editing its code.
- **If you change the source code** and people use your changed version over a network, which can include
  your own staff, you must offer those users the changed source code under the same licence. For internal use,
  that means making it available to them, not publishing it to the world.
- **You may charge for services** around MintQMS, such as hosting, installation, validation or support.

Many organisations already run AGPL-licensed self-hosted software, for example Nextcloud and Grafana.
