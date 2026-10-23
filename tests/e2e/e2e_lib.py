"""Shared helpers for the MintQMS end-to-end test scripts.

The scripts drive a running MintQMS instance over HTTP (requests + BeautifulSoup) and
inspect/prepare state directly in its SQLite database. ALWAYS point them at a throwaway
database — never the live OpenQMS.db.

Environment:
  E2E_BASE   base URL of the running app            (default http://localhost:5099)
  E2E_DB     path to the throwaway SQLite database   (required)
  E2E_PW     password used for every test user       (default E2e!Passw0rd#1 — also pass it
             to the app as SeedAdminPW so admin@trial.local is created with it)
"""
import os, re, sqlite3, sys, uuid
import requests
from bs4 import BeautifulSoup

BASE = os.environ.get("E2E_BASE", "http://localhost:5099")
DB = os.environ.get("E2E_DB")
PW = os.environ.get("E2E_PW", "E2e!Passw0rd#1")
ADMIN = "admin@trial.local"

if not DB or os.path.basename(DB).lower() in ("openqms.db", "app.db"):
    sys.exit("E2E_DB must point at a throwaway database copy")

_results = []


def db():
    con = sqlite3.connect(DB)
    con.row_factory = sqlite3.Row
    return con


def q(sql, args=()):
    with db() as con:
        return con.execute(sql, args).fetchall()


def q1(sql, args=()):
    rows = q(sql, args)
    return rows[0] if rows else None


def exec_sql(sql, args=()):
    with db() as con:
        con.execute(sql, args)
        con.commit()


def check(name, cond, detail=""):
    _results.append((name, bool(cond)))
    print(("PASS " if cond else "FAIL ") + name + ("" if cond else f"  -- {detail}"))
    return cond


def summary():
    passed = sum(1 for _, ok in _results if ok)
    print(f"\n{passed}/{len(_results)} passed")
    return 0 if passed == len(_results) else 1


def token(html):
    el = BeautifulSoup(html, "html.parser").find("input", {"name": "__RequestVerificationToken"})
    return el["value"] if el else None


def ensure_user(email, roles, first="Test", last="User"):
    """Create (via the admin Identity password hash) a user with the given roles, if absent."""
    if not q1("select 1 from AspNetUsers where NormalizedEmail=?", (email.upper(),)):
        admin = q1("select * from AspNetUsers where NormalizedEmail=?", (ADMIN.upper(),))
        exec_sql("""insert into AspNetUsers (UserName, NormalizedUserName, Email, NormalizedEmail,
                    EmailConfirmed, PasswordHash, SecurityStamp, ConcurrencyStamp, PhoneNumberConfirmed,
                    TwoFactorEnabled, LockoutEnabled, AccessFailedCount, FirstName, LastName, PasswordChangedOn)
                    values (?,?,?,?,1,?,?,?,0,0,1,0,?,?,datetime('now'))""",
                 (email, email.upper(), email, email.upper(), admin["PasswordHash"],
                  str(uuid.uuid4()), str(uuid.uuid4()), first, last))
    uid = q1("select Id from AspNetUsers where NormalizedEmail=?", (email.upper(),))["Id"]
    for r in roles:
        rid = q1("select Id from AspNetRoles where Name=?", (r,))["Id"]
        exec_sql("insert or ignore into AspNetUserRoles (UserId, RoleId) values (?,?)", (uid, rid))
    return uid


def login(email=ADMIN):
    """Log in and return a requests.Session. Clears the concurrent-login guard first."""
    exec_sql("update AspNetUsers set PasswordChangedOn=datetime('now') where PasswordChangedOn is null")
    exec_sql("delete from ActiveSession")
    s = requests.Session()
    r = s.get(f"{BASE}/Identity/Account/Login")
    r = s.post(f"{BASE}/Identity/Account/Login", data={
        "Input.Email": email, "Input.Password": PW, "Input.RememberMe": "false",
        "__RequestVerificationToken": token(r.text)}, allow_redirects=True)
    if "Log out" not in r.text and "Logout" not in r.text and "logout" not in r.text:
        sys.exit(f"login failed for {email}: {r.url}")
    return s


def get(s, path, **kw):
    return s.get(BASE + path, allow_redirects=kw.pop("allow_redirects", True), **kw)


def post_form(s, path, form_page, data, files=None, allow_redirects=False):
    """POST `data` to `path` with an antiforgery token scraped from GET `form_page`."""
    page = get(s, form_page)
    data = dict(data)
    data["__RequestVerificationToken"] = token(page.text)
    return s.post(BASE + path, data=data, files=files, allow_redirects=allow_redirects)


def form_fields(html, action_substr):
    """Return the name->value dict of the first <form> whose action contains action_substr."""
    soup = BeautifulSoup(html, "html.parser")
    for f in soup.find_all("form"):
        if action_substr.lower() in (f.get("action") or "").lower():
            out = {}
            for el in f.find_all(["input", "select", "textarea"]):
                n = el.get("name")
                if not n:
                    continue
                if el.name == "select":
                    opt = el.find("option", selected=True) or el.find("option")
                    out[n] = opt.get("value", opt.text) if opt else ""
                elif el.name == "textarea":
                    # browsers drop the single newline Razor emits after <textarea>
                    out[n] = el.text[1:] if el.text.startswith("\n") else el.text
                elif el.get("type") in ("checkbox", "radio"):
                    if el.has_attr("checked"):
                        out[n] = el.get("value", "on")
                    else:
                        out.setdefault(n, "false") if el.get("type") == "checkbox" else None
                else:
                    out.setdefault(n, el.get("value", ""))
            return out
    return None
