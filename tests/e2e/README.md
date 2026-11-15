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

Env: `E2E_BASE` (default `http://localhost:5099`), `E2E_DB` (required), `E2E_APP_ROOT` (the running app's content root; for the tests that plant files), `E2E_PW` (default
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
| `test_issued_documents.py` | Release certificates, training certificates and attendance lists are stored copies (site language at issue, SHA-256): unchanged by later language / organisation / logo / user-name changes and by the viewer's own language; tampering refused + audited; superseded/withdrawn shown outside the copy; pre-existing certificates fixed in English; training certificate only once complete, attendance list live until complete then fixed, reopen supersedes; failed attempts get none; typed markup escaped; audit stays English (needs `E2E_APP_ROOT`) |
| `test_record_language.py` | Exports and download stamps follow the SITE language (not the viewer's): English exports unchanged; Spanish CSV/Excel/PDF; every Excel register opens in es/pt-BR; export audit stays English; watermark text translated (needs `E2E_LANGS=1`, `E2E_APP_ROOT`) |
| `test_workflows_language.py` | Deviation, CAPA, change and complaint taken through every signed step in the suite's language: same states as in English, wrong password and empty required field refused without change, pages render at each step, audit trail English |
| `test_language.py` | Language settings and menu. English only: nothing can be chosen or set. With `E2E_LANGS=1` (app started with `Localization__Languages__1=es`, `__2=pt-BR`): Site Language applies to everyone incl. sign-in and is audited; only an Administrator can change it; the menu exists only while user choice is on and a cookie is ignored when it is off; anti-forgery, no off-site redirect; the home date heading follows the language and numbers keep the server format; ReadOnly users may change their own language but still cannot write; records stay English. Ends with user choice on. Detects installed languages itself, so the whole suite can also run with the site in es or pt-BR (`any_language()` in e2e_lib accepts a text in any language) |
| `test_workflow_baseline.py` | Every signed step and send-back/re-open route of Deviations, Complaints, CAPA, Change Control, Audits, Risk, Maintenance, Documents and Suppliers, walked as Administrator (state reached, record fields changed, audit-trail entries); then every step tried by each of the 11 roles from an exact copy of the record. Compared with `baseline/workflow_baseline.json` (the behaviour of 1.1.0). `E2E_WRITE_BASELINE=1` rewrites the baseline (only on a reference version, and only when every check passes); `E2E_MODULES=Deviations,CAPA` limits the run. Same result in every site language |
| `test_exports.py` | Every Export link returns a file (ClosedXML/CSV) |
| `crawl.py` | BFS crawl of all GET-reachable pages; fails on any 5xx / exception page |
