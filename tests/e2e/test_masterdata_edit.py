"""Master-data Edit POST must take lock/status/system fields from the stored record, not the form.

For each of Assets, Materials, Services, Products, Customers, Processes:
  1. Draft record: a tampered post (Status/IsLocked/Version/Approved*/code) saves only the
     editable change; system fields stay as stored.
  2. Stored IsLocked=1 (posted IsLocked=false)  -> 403, no change.
  3. Stored Status=Obsolete (posted Draft)      -> 403, no change.
  4. Stored Status=UnderReview (posted Draft)   -> redirect to Details with the error, no change.
"""
from e2e_lib import *

# module, table, code field, name field, extra create fields
MODULES = [
    ("Assets",    "Asset",    "AssetId",    "Name",      {"Description": "e2e"}),
    ("Materials", "Material", "MaterialId", "Name",      {"Description": "e2e"}),
    ("Services",  "Service",  "ServiceId",  "Name",      {}),
    ("Products",  "Product",  "ProductId",  "Name",      {"Description": "e2e"}),
    ("Customers", "Customer", "CustomerId", "LegalName", {}),
    ("Processes", "Process",  "ProcessId",  "Name",      {"Description": "e2e"}),
]
DRAFT, APPROVED, OBSOLETE, UNDER_REVIEW = 0, 1, 2, 3

s = login()
run = uuid.uuid4().hex[:6]

for mod, table, code_f, name_f, extra in MODULES:
    code = f"E2E-{run}-{mod[:3].upper()}"
    create = get(s, f"/{mod}/Create")
    data = form_fields(create.text, "Create")
    for k, v in list(data.items()):
        if k in ("__RequestVerificationToken",):
            continue
        if not v and k not in ("ContractExpiryDate",):
            data[k] = "e2e"
    if code_f in data:
        data[code_f] = code
    data[name_f] = f"E2E {mod} {run}"
    data.update(extra)
    # choose first real option for selects that ended up as 'e2e'
    from bs4 import BeautifulSoup
    soup = BeautifulSoup(create.text, "html.parser")
    for sel in soup.find_all("select"):
        opts = [o.get("value") for o in sel.find_all("option") if o.get("value")]
        if opts and sel.get("name") in data:
            data[sel["name"]] = opts[0]
    data.pop("ContractExpiryDate", None)
    data["Website"] = "https://example.com" if "Website" in data else None
    data = {k: v for k, v in data.items() if v is not None}
    if "ContactEmail" in data:
        data["ContactEmail"] = "e2e@example.com"
    r = s.post(f"{BASE}/{mod}/Create", data=data, allow_redirects=False)
    row = q1(f"select * from {table} where {name_f}=?", (f"E2E {mod} {run}",))
    if not check(f"{mod}: create", row is not None, f"status {r.status_code} {r.text[:300] if r.status_code != 302 else r.headers.get('Location')}"):
        continue
    rid = row["Id"]
    code = row[code_f]

    def edit_post(new_name, tamper=True):
        page = get(s, f"/{mod}/Edit/{rid}")
        f = form_fields(page.text, "Edit")
        if f is None:
            # GET Edit refused (locked/obsolete) — post a form built from the last good snapshot
            f = dict(base_form)
            f["__RequestVerificationToken"] = token(get(s, f"/{mod}/Details/{rid}").text)
        f[name_f] = new_name
        if tamper:
            f.update({"Status": str(DRAFT), "IsLocked": "false", "Version": "99",
                      "ApprovedBy": "attacker", "ApprovedOn": "2020-01-01", code_f: "HACKED",
                      "GeneratedFrom": "hacked", "IsDeleted": "false"})
        return s.post(f"{BASE}/{mod}/Edit/{rid}", data=f, allow_redirects=False)

    base_form = form_fields(get(s, f"/{mod}/Edit/{rid}").text, "Edit")

    # 1. Draft: tamper towards Approved/locked
    before = q1(f"select * from {table} where Id=?", (rid,))
    page = get(s, f"/{mod}/Edit/{rid}")
    f = form_fields(page.text, "Edit")
    f[name_f] = f"E2E {mod} {run} v2"
    f.update({"Status": str(APPROVED), "IsLocked": "true", "Version": "99", "ApprovedBy": "attacker",
              "ApprovedOn": "2020-01-01", code_f: "HACKED", "GeneratedFrom": "hacked"})
    r = s.post(f"{BASE}/{mod}/Edit/{rid}", data=f, allow_redirects=False)
    after = q1(f"select * from {table} where Id=?", (rid,))
    check(f"{mod}: draft edit saved name", after[name_f] == f"E2E {mod} {run} v2", f"status {r.status_code}, name={after[name_f]}")
    # Version legitimately bumps by 0.01 on every draft edit — it must not take the posted 99
    same = all(str(after[c]) == str(before[c]) for c in ("Status", "IsLocked", "ApprovedBy", "ApprovedOn", code_f, "GeneratedFrom")) \
        and abs(float(after["Version"]) - float(before["Version"]) - 0.01) < 1e-9
    check(f"{mod}: draft edit kept system fields", same,
          {c: (before[c], after[c]) for c in ("Status", "IsLocked", "Version", "ApprovedBy", code_f, "GeneratedFrom")})

    # 2. Stored lock
    exec_sql(f"update {table} set IsLocked=1 where Id=?", (rid,))
    r = edit_post(f"E2E {mod} {run} locked")
    after = q1(f"select * from {table} where Id=?", (rid,))
    check(f"{mod}: locked -> refused", r.status_code in (403, 302) and after[name_f] == f"E2E {mod} {run} v2" and after["IsLocked"] == 1,
          f"status {r.status_code} loc={r.headers.get('Location')} name={after[name_f]} locked={after['IsLocked']}")
    exec_sql(f"update {table} set IsLocked=0 where Id=?", (rid,))

    # 3. Stored Obsolete
    exec_sql(f"update {table} set Status=? where Id=?", (OBSOLETE, rid))
    r = edit_post(f"E2E {mod} {run} obsolete")
    after = q1(f"select * from {table} where Id=?", (rid,))
    check(f"{mod}: obsolete -> refused", after[name_f] == f"E2E {mod} {run} v2" and after["Status"] == OBSOLETE,
          f"status {r.status_code} name={after[name_f]} st={after['Status']}")
    check(f"{mod}: obsolete Edit GET refused", get(s, f"/{mod}/Edit/{rid}", allow_redirects=False).status_code != 200)

    # 4. Stored UnderReview
    exec_sql(f"update {table} set Status=? where Id=?", (UNDER_REVIEW, rid))
    r = edit_post(f"E2E {mod} {run} review")
    after = q1(f"select * from {table} where Id=?", (rid,))
    loc = r.headers.get("Location", "")
    ok = r.status_code == 302 and "/Details/" in loc and after[name_f] == f"E2E {mod} {run} v2" and after["Status"] == UNDER_REVIEW
    msg = get(s, loc).text if loc else ""
    check(f"{mod}: under review -> redirect + no change", ok, f"status {r.status_code} loc={loc} name={after[name_f]}")
    check(f"{mod}: under review message shown", "currently under review and cannot be edited" in msg)
    exec_sql(f"update {table} set Status=? where Id=?", (DRAFT, rid))

    # 5. Draft edit still works after all that
    r = edit_post(f"E2E {mod} {run} v3", tamper=False)
    after = q1(f"select * from {table} where Id=?", (rid,))
    check(f"{mod}: normal edit works", after[name_f] == f"E2E {mod} {run} v3" and after[code_f] == code,
          f"status {r.status_code} name={after[name_f]}")

sys.exit(summary())
