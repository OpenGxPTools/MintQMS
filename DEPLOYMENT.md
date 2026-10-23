# MintQMS — Installation & Deployment Guide

This guide explains how to try MintQMS on your own computer and how to install it on a server for
day-to-day use, including HTTPS, backups, upgrades and a go-live checklist.

> **Before you start.** MintQMS is free software provided **without warranty** and is **not validated**
> for any regulated use. If you use it for GMP/GxP records, your organisation is responsible for
> validating it (for example under PIC/S Annex 11, GAMP 5 or 21 CFR Part 11), and for its configuration,
> data, backups and compliance. See [DISCLAIMER.md](DISCLAIMER.md).

**Contents**

1. [What you need](#1-what-you-need)
2. [Try it on your own computer](#2-try-it-on-your-own-computer)
3. [Install on a server (production)](#3-install-on-a-server-production)
4. [First start and first login](#4-first-start-and-first-login)
5. [Backups (mandatory)](#5-backups-mandatory)
6. [Upgrading to a new version](#6-upgrading-to-a-new-version)
7. [Configuration reference](#7-configuration-reference)
8. [Security notes](#8-security-notes)
9. [Validation](#9-validation)
10. [Pre-go-live checklist](#10-pre-go-live-checklist)

---

## 1. What you need

| | Trying it out | Production server |
|---|---|---|
| Operating system | Linux, Windows or macOS | Linux (examples use Ubuntu 24.04 LTS) |
| .NET | .NET 10 **SDK** | .NET 10 **ASP.NET Core Runtime** (the SDK is only needed where you build) |
| Database | SQLite (built in — nothing to install) | SQLite (built in), plus the `sqlite3` tool for backups |
| Web server | Not needed | nginx or Caddy, to provide HTTPS |
| Other | — | A host name (e.g. `qms.example.com`), an HTTPS certificate (Caddy obtains one automatically), and fonts (`fonts-liberation`) for reports and image watermarks |

Install .NET from <https://dotnet.microsoft.com/download/dotnet/10.0>. On Ubuntu:

```bash
sudo apt install dotnet-sdk-10.0            # build machine / evaluation
sudo apt install aspnetcore-runtime-10.0    # production server (runtime only)
```

---

## 2. Try it on your own computer

This gives you a local copy with an empty database, for evaluation only.

```bash
git clone https://github.com/OpenGxPTools/MintQMS.git
cd MintQMS
```

Choose a password for the built-in administrator. It must be **at least 10 characters with upper case,
lower case, a digit and a symbol** (otherwise the administrator is silently not created):

```bash
# Linux / macOS
SeedAdminPW='Choose-A-Strong-Pw1!' dotnet run

# Windows (PowerShell)
$env:SeedAdminPW='Choose-A-Strong-Pw1!'; dotnet run
```

The first start builds the application and creates the database (`OpenQMS.db` in the project folder).
Open the address shown in the console (normally <https://localhost:7188>; if your browser warns about
the certificate, run `dotnet dev-certs https --trust` once). Log in as **admin@trial.local** with the
password you chose — you will be asked to set a new password straight away. Then follow
[section 4](#4-first-start-and-first-login).

Approving controlled documents signs the PDF digitally and needs a signing certificate. To try that
locally, create one as in [section 3.4](#34-pdf-signing-certificate), put `cert.pfx` in the project folder
and also set `CertPassword` before `dotnet run`.

---

## 3. Install on a server (production)

The layout used below keeps the **program** and the **data** apart, so that upgrades can replace the
program without touching your records:

| Path | Contents |
|---|---|
| `/opt/mintqms` | The program (replaced on every upgrade) |
| `/var/lib/mintqms/OpenQMS.db` | The database |
| `/var/lib/mintqms/uploads` | Uploaded files (attachments, controlled documents, logo) |
| `/var/lib/mintqms/keys` | Sign-in and form-protection keys |
| `/etc/mintqms/mintqms.env` | Settings and secrets (readable only by root) |
| `/var/backups/mintqms` | Backups |

### 3.1 Service account and folders

```bash
sudo apt install aspnetcore-runtime-10.0 sqlite3 fonts-liberation
sudo useradd --system --no-create-home --shell /usr/sbin/nologin mintqms
sudo install -d -o mintqms -g mintqms -m 750 /opt/mintqms /var/lib/mintqms \
     /var/lib/mintqms/uploads /var/lib/mintqms/keys /var/backups/mintqms
sudo install -d -m 750 /etc/mintqms
```

### 3.2 Build and copy the program

On a machine with the .NET 10 SDK (this can be the server itself):

```bash
git clone https://github.com/OpenGxPTools/MintQMS.git && cd MintQMS
dotnet publish -c Release -o ./publish
sudo rsync -a --delete --exclude 'wwwroot/uploads' ./publish/ /opt/mintqms/
sudo cp -r Scripts /opt/mintqms/
sudo ln -sfn /var/lib/mintqms/uploads /opt/mintqms/wwwroot/uploads
sudo chown -R mintqms:mintqms /opt/mintqms
```

The symbolic link makes the program store uploads in `/var/lib/mintqms/uploads`.

### 3.3 Settings file

Create `/etc/mintqms/mintqms.env` (`sudo nano /etc/mintqms/mintqms.env`):

```ini
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://127.0.0.1:5000
ConnectionStrings__DefaultConnection=Data Source=/var/lib/mintqms/OpenQMS.db
AllowedHosts=qms.example.com
DataProtectionKeysPath=/var/lib/mintqms/keys
BackupStatusFile=/var/backups/mintqms/backup_status.json
CertPassword=<the password of cert.pfx>
# First start only — remove after the first login (section 4):
SeedAdminPW=<strong temporary password>
```

```bash
sudo chmod 600 /etc/mintqms/mintqms.env
```

- `AllowedHosts` must be your real host name. With `*` a forged request could put another web address
  into password-reset e-mails; MintQMS logs a warning at start-up if it is left as `*`.
- `ASPNETCORE_URLS` keeps MintQMS listening on the server itself only; the web server (3.6) is the only
  way in from outside.
- The "Source code" link on every page points to the MintQMS project by default. The licence (AGPLv3)
  requires you to offer the source code to your users — if you change MintQMS, add
  `SourceCodeUrl=<address of your modified source>` to this file.

### 3.4 PDF signing certificate

Approved controlled documents are signed with the certificate in `/opt/mintqms/cert.pfx`. Use your
organisation's document-signing certificate if you have one; otherwise create a self-signed one:

```bash
openssl req -x509 -newkey rsa:3072 -sha256 -days 1825 -nodes \
  -keyout signing.key -out signing.crt -subj "/O=Your Organisation/CN=MintQMS Document Signing"
openssl pkcs12 -export -inkey signing.key -in signing.crt -out cert.pfx   # asks for the password
sudo install -o mintqms -g mintqms -m 400 cert.pfx /opt/mintqms/cert.pfx
shred -u signing.key
```

Put the password you chose in `CertPassword` (3.3). Keep a copy of `cert.pfx` and its password in a safe
place — upgrades (section 6) must put it back.

### 3.5 Run MintQMS as a service

Create `/etc/systemd/system/mintqms.service`:

```ini
[Unit]
Description=MintQMS
After=network.target

[Service]
User=mintqms
Group=mintqms
WorkingDirectory=/opt/mintqms
EnvironmentFile=/etc/mintqms/mintqms.env
ExecStart=/usr/bin/dotnet /opt/mintqms/OpenQMS.dll
Restart=on-failure
RestartSec=10
KillSignal=SIGINT
NoNewPrivileges=true
PrivateTmp=true

[Install]
WantedBy=multi-user.target
```

`WorkingDirectory` must be the program folder: MintQMS finds `cert.pfx` and `wwwroot/uploads` there.

```bash
sudo systemctl daemon-reload
sudo systemctl enable --now mintqms
sudo journalctl -u mintqms -f        # watch the start-up messages; Ctrl+C to stop watching
```

### 3.6 HTTPS with a web server

Use **one** of the two options. Both pass the original address and protocol to MintQMS, which trusts
these details only from a web server on the same machine.

**Option A — Caddy** (obtains and renews the HTTPS certificate automatically). Install Caddy
(<https://caddyserver.com/docs/install>) and set `/etc/caddy/Caddyfile` to:

```
qms.example.com {
    reverse_proxy 127.0.0.1:5000
    request_body {
        max_size 30MB
    }
}
```

```bash
sudo systemctl reload caddy
```

**Option B — nginx** (with your own certificate, or one from Let's Encrypt via `certbot`):

```nginx
server {
    listen 443 ssl;
    server_name qms.example.com;
    ssl_certificate     /etc/ssl/certs/qms.example.com.crt;
    ssl_certificate_key /etc/ssl/private/qms.example.com.key;
    client_max_body_size 30m;

    location / {
        proxy_pass         http://127.0.0.1:5000;
        proxy_set_header   Host $host;
        proxy_set_header   X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header   X-Forwarded-Proto $scheme;
    }
}
server {
    listen 80;
    server_name qms.example.com;
    return 301 https://$host$request_uri;
}
```

The upload limit (`30MB`) is a little above MintQMS's own 25 MB limit per file. MintQMS only works over
HTTPS in production — its sign-in cookie is never sent over plain HTTP.

### 3.7 Firewall

Allow only SSH and the web server from outside:

```bash
sudo ufw allow OpenSSH && sudo ufw allow 80,443/tcp && sudo ufw enable
```

---

## 4. First start and first login

1. Browse to `https://qms.example.com` and log in as **admin@trial.local** with the `SeedAdminPW`
   password. You are asked to choose a new password straight away.
2. **Remove `SeedAdminPW`** from `/etc/mintqms/mintqms.env` and run `sudo systemctl restart mintqms`.
3. Under **Settings → System Settings**, set at least:
   - organisation name, address and logo (the logo appears on certificates and reports);
   - **site time zone** — every date and time is shown in this zone;
   - **e-mail (SMTP)** server and sender address — needed for account confirmation and password resets;
   - retention periods, password expiry, idle time-out and the warning periods used by the dashboards, to
     match your procedures.
4. Under **Settings → Release Certification Statements**, review the batch-release certification statements.
5. Create user accounts: **Settings → Users → Create New User**, then **Manage Roles** to give each
   person the roles they need. Each new user confirms their e-mail address before first login (while
   e-mail is not yet set up, the confirmation page shows the link for the Administrator to pass on).
6. Use a personal Administrator account for administration and keep `admin@trial.local` for
   emergencies only.

The [User & Operations Guide](docs/MintQMS_User_and_Operations_Guide.md) explains every setting, role and
module.

---

## 5. Backups (mandatory)

A backup that is never made, never copied off the server or never test-restored will not save you.

### 5.1 Configure the backup script

`Scripts/backup_mintqms.sh` copies the database and archives the uploaded files, as a matching pair:

| Variable | Default | Set to (this guide's layout) |
|---|---|---|
| `DB_SOURCE` | `/opt/mintqms/OpenQMS.db` | `/var/lib/mintqms/OpenQMS.db` |
| `UPLOADS_SOURCE` | `/opt/mintqms/wwwroot/uploads` | `/var/lib/mintqms/uploads` (the real folder, not the link) |
| `BACKUP_DIR` | `/var/backups/mintqms` | `/var/backups/mintqms` |
| `STATUS_FILE` | `/var/backups/mintqms/backup_status.json` | same as `BackupStatusFile` in 3.3 |
| `RETAIN_COPIES` | `7` | e.g. `30` for a month of daily backups |

Each run writes `OpenQMS_<timestamp>.db` and `OpenQMS_uploads_<timestamp>.tar.gz`, checks both, and
deletes copies older than `RETAIN_COPIES`. The run counts as failed if either part fails. The
**Settings → System Settings** page shows the result of the last run.

### 5.2 Schedule it

`sudo crontab -u mintqms -e` and add:

```cron
0 2 * * * DB_SOURCE=/var/lib/mintqms/OpenQMS.db UPLOADS_SOURCE=/var/lib/mintqms/uploads /opt/mintqms/Scripts/backup_mintqms.sh >> /var/backups/mintqms/backup.log 2>&1
```

After the first run, `cat /var/backups/mintqms/backup_status.json` should show `"status":"success"`.

### 5.3 Copy backups off the server

A backup on the same disk does not survive disk failure or ransomware. Copy `/var/backups/mintqms` to
another location at least daily, for example:

```cron
30 2 * * * rsync -a --delete /var/backups/mintqms/ backup-host:/srv/mintqms-backups/
```

(or `rclone` / `aws s3 sync` to cloud storage). Also keep `cert.pfx`, its password and
`/etc/mintqms/mintqms.env` somewhere safe.

### 5.4 Test a restore

At least every three months, restore the latest pair (same timestamp) on a test machine, log in, and open
a few attachments and a controlled document. Every download is checked against the fingerprint
(SHA-256) recorded at upload, so a damaged file is blocked and reported.

```bash
sudo systemctl stop mintqms
sudo -u mintqms cp /var/backups/mintqms/OpenQMS_<timestamp>.db /var/lib/mintqms/OpenQMS.db
sudo -u mintqms tar -xzf /var/backups/mintqms/OpenQMS_uploads_<timestamp>.tar.gz -C /var/lib/mintqms
sudo systemctl start mintqms
```

---

## 6. Upgrading to a new version

1. Read the release notes in [CHANGELOG.md](CHANGELOG.md).
2. Run a backup (`sudo -u mintqms … backup_mintqms.sh` as in 5.2) and check it succeeded.
3. Build the new version (3.2, `git pull` then `dotnet publish`).
4. `sudo systemctl stop mintqms`
5. Copy the program, keeping the uploads link and the certificate:
   ```bash
   sudo rsync -a --delete --exclude 'wwwroot/uploads' --exclude 'cert.pfx' ./publish/ /opt/mintqms/
   sudo cp -r Scripts /opt/mintqms/
   sudo chown -R mintqms:mintqms /opt/mintqms
   ```
6. `sudo systemctl start mintqms` — database changes are applied automatically at start-up. Watch
   `journalctl -u mintqms` for errors, log in and check a few records.

If anything goes wrong, stop the service, put back the previous program and restore the backup from
step 2.

---

## 7. Configuration reference

Settings can go in `/etc/mintqms/mintqms.env` (use `__` for nested names, as shown) or in
`appsettings.json`.

| Setting | Purpose |
|---|---|
| `ConnectionStrings__DefaultConnection` | Database location, e.g. `Data Source=/var/lib/mintqms/OpenQMS.db` |
| `AllowedHosts` | The host name(s) users type, separated by `;` |
| `DataProtectionKeysPath` | Folder that keeps users signed in across restarts |
| `SourceCodeUrl` | Link to the source code of the version you run (AGPLv3); defaults to the MintQMS project |
| `CertPassword` | Password of `cert.pfx` |
| `SeedAdminPW` | First start only: creates `admin@trial.local` with this password |
| `BackupStatusFile` | Where the Settings page reads the backup result |
| `ASPNETCORE_URLS` | Where MintQMS listens (keep it on `127.0.0.1`) |
| `ASPNETCORE_ENVIRONMENT` | `Production` on servers |

Everything else (organisation details, e-mail, time zone, retention, password rules) is set in the
application under **Settings → System Settings**.

**Database.** MintQMS supports SQLite only. Its database set-up scripts (migrations) are written for
SQLite, so moving to PostgreSQL or SQL Server is a development project, not a configuration change.
Protect the SQLite file as described in sections 3 and 5.

---

## 8. Security notes

- Keep the server patched: `sudo apt update && sudo apt upgrade` regularly; this also updates .NET.
- Only the web server is reachable from outside; MintQMS listens on the server itself.
- Never run MintQMS as root. The files in `/var/lib/mintqms` and `/etc/mintqms` must be readable only by
  the service account (and root).
- Never run destructive commands against the live database (deleting the file, dropping the database).
  Make changes on a copy.
- Uploaded files are served only through MintQMS (with sign-in, audit trail and watermark); direct
  `/uploads/...` addresses are blocked, except the organisation logo.
- Report suspected security problems as described in [SECURITY.md](SECURITY.md).

---

## 9. Validation

MintQMS is not supplied validated. If you use it for regulated records, plan and document a validation
proportionate to your risk (for example a GAMP 5 category 4/5 approach: requirements, risk assessment,
installation/operational/performance qualification, and procedures for backup, access control and
change control). The automated tests in `tests/e2e` show what was checked during development and can
support — but do not replace — your own testing.

---

## 10. Pre-go-live checklist

- [ ] Server patched; .NET 10 runtime, `sqlite3` and fonts installed.
- [ ] MintQMS runs as the `mintqms` service account, not root; starts on boot.
- [ ] Database, uploads and keys are in `/var/lib/mintqms`; settings file is `chmod 600`.
- [ ] `AllowedHosts` and `DataProtectionKeysPath` set (and `SourceCodeUrl` if you changed MintQMS); no `AllowedHosts` warning in the log.
- [ ] HTTPS works; plain HTTP redirects to HTTPS; only ports 22, 80 and 443 are open.
- [ ] `cert.pfx` installed (`chmod 400`); a test document approval produces a signed PDF.
- [ ] First login done, `SeedAdminPW` removed, personal Administrator accounts created.
- [ ] Settings completed: organisation, logo, time zone, e-mail (a password-reset e-mail arrives), retention.
- [ ] Server clock synchronised (`timedatectl` shows "System clock synchronized: yes").
- [ ] Backup scheduled; first run successful and shown on the Settings page.
- [ ] Off-server backup copy working; a test restore done.
- [ ] A direct `/uploads/...` address (other than the logo) returns nothing.
- [ ] Validation activities completed and approved, if used for regulated records.
