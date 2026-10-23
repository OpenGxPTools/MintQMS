# MintQMS end-to-end tests

Python (`requests` + `beautifulsoup4`) scripts that drive a running MintQMS over HTTP and
prepare/inspect state directly in its SQLite database. **Always use a throwaway database** —
`e2e_lib.py` refuses `OpenQMS.db` / `app.db`.

## Run

```bash
# 1. start the app on a fresh throwaway DB (migrations + admin seed run at startup)
ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://localhost:5099 \
ConnectionStrings__DefaultConnection="Data Source=/tmp/e2e.sqlite" \
SeedAdminPW='E2e!Passw0rd#1' dotnet bin/Debug/net10.0/OpenQMS.dll

# 2. run the suites
E2E_DB=/tmp/e2e.sqlite ./run_all.sh
```

Env: `E2E_BASE` (default `http://localhost:5099`), `E2E_DB` (required), `E2E_APP_ROOT` (the running app's content root; `test_file_integrity.py` only), `E2E_PW` (default
`E2e!Passw0rd#1`, must equal `SeedAdminPW`). Test users are created on demand with the admin's
password hash. The concurrent-login guard is cleared (`ActiveSession`) before each login.

| Script | Covers |
|---|---|
| `test_masterdata_edit.py` | Master-data Edit ignores posted lock/status/system fields; locked/obsolete/under-review refused |
| `test_xss_supplier_items.py` | Stored-XSS sites (PV, Release scripts parse), supplier address role gate, doc hash + audit, quality-record lock/audit |
| `test_low_priority.py` | AppUsers/Create, Complaints Accept roles, CreateReview LastReviewDate, shared upload rules |
| `test_sitetime.py` | Stored UTC shown in the site time zone (Settings), zone label, setting change audited |
| `test_file_integrity.py` | Attachment downloads/views verify the upload SHA-256 (all 19 types); tampered files blocked + audited; uploads record the hash (needs `E2E_APP_ROOT`) |
| `test_release_prep.py` | Account-confirmation page is Administrator-only; links use https behind a reverse proxy; image watermark banner/footer/diagonal drawn; only PNG/JPEG/GIF content decoded (needs `E2E_APP_ROOT`; optional `E2E_OUT` saves the images) |
| `test_pv_workflow.py` | Adverse-event signed workflow incl. Reject (Pending Approval → Under Medical Review) and wrong-password refusal |
| `test_settings_ap_maint.py` | Dashboards follow the LC/maintenance/training warning periods; Date format removed; Authorised Person approves all reference data; maintenance work recorded by the doer while In Progress |
| `test_detail_reports.py` | Every module's per-record PDF detail report and CSV detail export |
| `test_exports.py` | Every Export link returns a file (ClosedXML/CSV) |
| `crawl.py` | BFS crawl of all GET-reachable pages; fails on any 5xx / exception page |
