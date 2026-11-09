"""Interface language: the menu, the cookie it sets, and what it must NOT change.

Run twice:
  * default configuration (English only)  →  no menu anywhere; choosing another language refused.
  * E2E_LANGS=1, app started with Localization__Languages__1=es and Localization__Languages__2=pt-BR
                                           →  menu offered; the choice is remembered; dates, numbers
                                              and every record stay exactly as they were.
"""
import os, re, uuid
from bs4 import BeautifulSoup
from e2e_lib import *

ENABLED = os.environ.get("E2E_LANGS") == "1"


def menu(html):
    sel = BeautifulSoup(html, "html.parser").find("select", {"id": "mintLanguage"})
    return [o["value"] for o in sel.find_all("option")] if sel else None


def set_lang(s, culture, page="/Identity/Account/Login", return_url="/", with_token=True):
    r = s.get(BASE + page)
    data = {"culture": culture, "returnUrl": return_url}
    if with_token:
        data["__RequestVerificationToken"] = token(r.text)
    return s.post(BASE + "/Language/Set", data=data, allow_redirects=False)


def visible(html):
    soup = BeautifulSoup(html, "html.parser")
    for x in soup(["script", "style"]):
        x.decompose()
    for x in soup.find_all("form", class_="mint-language-menu"):
        x.decompose()   # the menu itself shows which language is selected
    for x in soup.select('input[name="__RequestVerificationToken"]'):
        x.decompose()
    return " ".join(soup.get_text(" ").split())


anon = requests.Session()
login_page = anon.get(BASE + "/Identity/Account/Login").text

# --- always: the endpoint's guards -------------------------------------------------------------
r = set_lang(requests.Session(), "en", with_token=False)
check("language: POST without anti-forgery token refused", r.status_code == 400, r.status_code)
r = set_lang(requests.Session(), "fr")
check("language: unknown language refused", r.status_code == 400, r.status_code)
r = set_lang(requests.Session(), "en", return_url="https://evil.example/x")
check("language: never redirects off-site", r.status_code == 302 and r.headers.get("Location") == "/",
      (r.status_code, r.headers.get("Location")))
r = set_lang(requests.Session(), "en", return_url="//evil.example/x")
check("language: protocol-relative return URL refused", r.headers.get("Location") == "/", r.headers.get("Location"))

if not ENABLED:
    # --- English only (the shipped default) --------------------------------------------------------
    check("default: no language menu on the sign-in page", menu(login_page) is None)
    s = login()
    check("default: no language menu in the menu bar", menu(get(s, "/").text) is None)
    r = set_lang(requests.Session(), "es")
    check("default: Spanish cannot be chosen while not enabled", r.status_code == 400, r.status_code)
    check("default: and no language cookie is set", ".MintQMS.Language" not in r.headers.get("Set-Cookie", ""))
else:
    # --- languages enabled --------------------------------------------------------------------------
    check("enabled: menu on the sign-in page offers en, es, pt-BR", menu(login_page) == ["en", "es", "pt-BR"],
          menu(login_page))
    s = login()
    home_en = get(s, "/").text
    check("enabled: menu in the menu bar", menu(home_en) == ["en", "es", "pt-BR"], menu(home_en))

    r = set_lang(s, "es", page="/", return_url="/AppDocuments")
    check("enabled: choosing Spanish redirects back to the page", r.status_code == 302 and
          r.headers.get("Location") == "/AppDocuments", (r.status_code, r.headers.get("Location")))
    ck = r.headers.get("Set-Cookie", "")
    check("enabled: the choice is kept in the language cookie", ".MintQMS.Language=" in ck and "uic%3Des" in ck, ck)
    check("enabled: cookie is HttpOnly and SameSite=Lax", "httponly" in ck.lower() and "samesite=lax" in ck.lower(), ck)

    home_es = get(s, "/").text
    sel = BeautifulSoup(home_es, "html.parser").find("select", {"id": "mintLanguage"})
    check("enabled: the menu now shows Spanish as selected",
          sel is not None and (sel.find("option", selected=True) or {}).get("value") == "es")

    # Dates and numbers keep the server's format. The home page heading spells out the weekday and
    # month with CurrentCulture — the same culture model binding uses to read typed numbers — so an
    # unchanged heading proves the number format is unchanged too.
    day = re.compile(r"(Monday|Tuesday|Wednesday|Thursday|Friday|Saturday|Sunday), \d{1,2} \w+ \d{4}")
    m_en, m_es = day.search(visible(home_en)), day.search(visible(home_es))
    check("enabled: date heading unchanged with Spanish selected (format culture pinned)",
          m_en and m_es and m_en.group(0) == m_es.group(0), (m_en and m_en.group(0), m_es and m_es.group(0)))

    # Translated pages: Spanish menus in Spanish, English untouched, never a raw key on screen.
    s_en = requests.Session(); s_en.cookies.update({k: v for k, v in s.cookies.items() if k != ".MintQMS.Language"})
    for path in ["/", "/AppDocuments", "/AppDocuments/Create", "/Deviations", "/Deviations/Create", "/Trainings", "/Settings"]:
        es_html, en_html = get(s, path).text, s_en.get(BASE + path).text
        nav = BeautifulSoup(es_html, "html.parser").find("nav")
        nav_text = " ".join(nav.get_text(" ").split()) if nav else ""
        check(f"enabled: {path} shows the menu in Spanish", "Eventos de calidad" in nav_text and "Quality Events" not in nav_text, nav_text[:120])
        check(f"enabled: {path} stays English without the cookie", "Quality Events" in en_html and "Eventos de calidad" not in en_html)
        leaks = re.findall(r'@?\b[LH]\["[^"]{0,40}', visible(es_html) + visible(en_html))
        check(f"enabled: {path} shows no raw translation key", not leaks, leaks[:3])
    check("enabled: the home date heading stays the server format in Spanish", m_en and m_es and m_en.group(0) == m_es.group(0))

    r = set_lang(s, "pt-br", page="/")
    check("enabled: language codes accepted in any case", r.status_code == 302 and "uic%3Dpt-BR" in r.headers.get("Set-Cookie", ""),
          r.headers.get("Set-Cookie"))

    # ReadOnly users may change their own language, and still nothing else.
    ensure_user("readonly-lang@e2e.local", ["ReadOnly"])
    ro = login("readonly-lang@e2e.local")
    r = set_lang(ro, "es", page="/")
    check("enabled: ReadOnly user can choose a language", r.status_code == 302, r.status_code)
    # Deviations/Create is open to every signed-in user, so only ReadOnlyActionFilter stops this.
    before = q1("select count(*) n from Deviation")["n"]
    r = post_form(ro, "/Deviations/Create", "/Deviations", {"Title": "should-not-exist", "Description": "x"})
    after = q1("select count(*) n from Deviation")["n"]
    check("enabled: ReadOnly user still cannot create a record (filter intact)",
          r.status_code in (302, 403) and after == before and
          (r.status_code == 403 or "AccessDenied" in r.headers.get("Location", "")),
          (r.status_code, r.headers.get("Location"), before, after))

    # Records stay English whatever the screen language: create a deviation with Spanish selected,
    # then scan every audit-trail table for any Spanish or Portuguese wording.
    import json as _json
    es = login()
    es.cookies.set(".MintQMS.Language", "c%3Des%7Cuic%3Des")
    page = get(es, "/Deviations/Create").text
    form = form_fields(page, "/Deviations/Create")
    title = "lang-record-check " + uuid.uuid4().hex[:6]
    form.update({"Title": title, "Identification": "Raised from the language test"})
    r = es.post(BASE + "/Deviations/Create", data=form, allow_redirects=False)
    dev = q1("select Id from Deviation where Title=?", (title,))
    check("records: a deviation can be created with Spanish selected", r.status_code == 302 and dev is not None,
          (r.status_code, r.headers.get("Location")))
    if dev:
        log = q("select Action, FromStatus, ToStatus from DeviationAuditLog where ParentId=?", (dev["Id"],))
        check("records: its audit entry is written in English codes", any(row["Action"] == "Created" for row in log),
              [dict(x) for x in log])
    tr = _json.load(open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..",
                                       "Resources", "Localization", "translations.json"), encoding="utf-8"))
    foreign = {v for k, e in tr.items() for lang, v in e.items()
               if lang in ("es", "pt-BR") and v and v != k and len(v) >= 6 and not v.isupper()}
    found = []
    for (tbl,) in [tuple(x) for x in q("select name from sqlite_master where type='table' and name like '%Audit%Log%'")]:
        cols = [c["name"] for c in q(f"pragma table_info('{tbl}')") if c["type"].upper().startswith(("TEXT", "NVARCHAR", "VARCHAR"))]
        for c in cols:
            for (val,) in [tuple(x) for x in q(f'select distinct "{c}" from "{tbl}" where "{c}" is not null')]:
                for f in foreign:
                    if re.search(r"(?<![\w])" + re.escape(f) + r"(?![\w])", str(val)):
                        found.append((tbl, c, f, str(val)[:60]))
    check("records: no Spanish/Portuguese wording in any audit trail", not found, found[:5])

sys.exit(summary())
