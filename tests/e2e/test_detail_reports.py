"""Per-record detail exports: every module's PDF detail report (RDLC / iText) and CSV detail export.

A minimal record is inserted per module and its exports are fetched. Covers the report engines
(ReportViewerCore, iText) that the register exports and the page crawl do not reach.
"""
import re, sqlite3
from e2e_lib import *

run = uuid.uuid4().hex[:6]
NOW = "2026-09-01 00:00:00"
DATE_COL = re.compile(r"(On|Date|At|Time|Since|Until|Due)$|Date")

# controller, table, PDF action, CSV action (None = no CSV export)
MODULES = [
    ("Deviations", "Deviation", "ExportDeviationDetail", "ExportDetailInCSV"),
    ("Complaints", "Complaint", "ExportComplaintDetail", "ExportDetailInCSV"),
    ("Capas", "Capa", "ExportCAPADetail", "ExportDetailInCSV"),
    ("Changes", "Change", "ExportChangeDetail", "ExportDetailInCSV"),
    ("Audits", "Audit", "ExportAuditDetail", "ExportDetailInCSV"),
    ("Risks", "Risk", "ExportRiskDetail", "ExportDetailInCSV"),
    ("Maintenances", "Maintenance", "ExportMaintenanceDetail", "ExportDetailInCSV"),
    ("Suppliers", "Supplier", "ExportSupplierDetail", "ExportDetailInCSV"),
    ("Assets", "Asset", "ExportAssetDetail", "ExportDetailInCSV"),
    ("Customers", "Customer", "ExportCustomerDetail", "ExportDetailInCSV"),
    ("Materials", "Material", "ExportMaterialDetail", "ExportDetailInCSV"),
    ("Processes", "Process", "ExportProcessDetail", "ExportDetailInCSV"),
    ("Products", "Product", "ExportProductDetail", "ExportDetailInCSV"),
    ("Services", "Service", "ExportServiceDetail", "ExportDetailInCSV"),
    ("Release", "ReleaseRecords", "ExportDetailPdf", "ExportDetailCsv"),
]


def raw_insert(table, values=None):
    """Insert a row, filling every other NOT NULL column without a default with a plausible value."""
    con = sqlite3.connect(DB)
    row = dict(values or {})
    for _, name, ctype, notnull, default, pk in con.execute(f'pragma table_info("{table}")').fetchall():
        if pk or name in row or not notnull or default is not None:
            continue
        t = (ctype or "").upper()
        if name == "IsDeleted" or "INT" in t or "REAL" in t or "NUM" in t:
            row[name] = 0
        elif "BLOB" in t:
            row[name] = b""
        elif DATE_COL.search(name):
            row[name] = NOW
        elif name.endswith("Version"):
            row[name] = "1.0"
        else:
            row[name] = f"E2E-{run}-{name}"[:40]
    keys = ", ".join(f'"{k}"' for k in row)
    cur = con.execute(f'insert into "{table}" ({keys}) values ({", ".join("?" * len(row))})', list(row.values()))
    con.commit()
    rid = cur.lastrowid
    con.close()
    return rid


s = login()
for ctrl, table, pdf, csv in MODULES:
    # a maintenance record always belongs to an asset (the report joins it)
    rid = raw_insert(table, {"AssetId": raw_insert("Asset")} if table == "Maintenance" else None)
    r = s.get(BASE + f"/{ctrl}/{pdf}/{rid}", allow_redirects=False, timeout=120)
    check(f"{ctrl}: PDF detail report", r.status_code == 200 and r.content[:4] == b"%PDF",
          (r.status_code, r.headers.get("Content-Type"), r.content[:60]))
    if csv:
        r = s.get(BASE + f"/{ctrl}/{csv}/{rid}", allow_redirects=False, timeout=60)
        check(f"{ctrl}: CSV detail export", r.status_code == 200 and len(r.content) > 0,
              (r.status_code, r.headers.get("Content-Type")))

summary()
