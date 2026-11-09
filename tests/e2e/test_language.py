"""Interface language: the administrator's settings, the user menu, and what language must NOT change.

Run twice:
  * default configuration (English only installed) → no menu; nothing else can be chosen or set.
  * E2E_LANGS=1, app started with Localization__Languages__1=es and Localization__Languages__2=pt-BR:
      - Site Language (System Settings) applies to everyone, incl. the sign-in page; audited;
        only an Administrator can change it;
      - users get a menu only when "Let Users Choose Their Language" is on; off again → ignored;
      - dates, numbers and every record stay exactly as they were.
    The run ENDS with site language English and user choice ON, so later browser checks can use the
    language cookie.
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



def settings_save(s, **changes):
    page = get(s, "/Settings").text
    f = form_fields(page, "/Settings/Save")
    f.pop("logoFile", None)
    for req in ("OrganisationName", "OrganisationAddress"):      # required; empty on a fresh site
        if not f.get(req):
            f[req] = "E2E Test Organisation"
    f.update({k: v for k, v in changes.items()})
    return s.post(BASE + "/Settings/Save", data=f, allow_redirects=False)


def site_settings():
    return dict(q1("select SiteLanguage, AllowUserLanguageChoice from SystemSettings where Id=1"))


def audited(field, old, new):
    return q1("select 1 from SettingsAuditLog where FieldName=? and OldValue=? and NewValue=?", (field, old, new)) is not None


def nav_text(html):
    nav = BeautifulSoup(html, "html.parser").find("nav")
    return " ".join(nav.get_text(" ").split()) if nav else ""


s = login()
get(s, "/Settings")                                   # a fresh site creates its settings row here

if not ENABLED:
    # --- English only installed (the shipped default) -------------------------------------------
    check("default: no language menu on the sign-in page", menu(requests.get(BASE + "/Identity/Account/Login").text) is None)
    check("default: no language menu in the menu bar", menu(get(s, "/").text) is None)
    for lang in ("es", "en"):
        r = set_lang(requests.Session(), lang)
        check(f"default: choosing a language ({lang}) is refused", r.status_code == 400, r.status_code)
    opts = [o.get("value") for o in BeautifulSoup(get(s, "/Settings").text, "html.parser").select("select[name=SiteLanguage] option")]
    check("default: Site Language offers English only", opts == ["en"], opts)
    r = settings_save(s, SiteLanguage="es")
    check("default: a language that is not installed cannot be set", site_settings()["SiteLanguage"] == "en", (r.status_code, site_settings()))
else:
    # --- A. fresh site: English for everyone, no choice ------------------------------------------
    exec_sql("update SystemSettings set SiteLanguage='en', AllowUserLanguageChoice=0 where Id=1")
    check("A: no menu on the sign-in page while users may not choose", menu(requests.get(BASE + "/Identity/Account/Login").text) is None)
    check("A: no menu in the menu bar while users may not choose", menu(get(s, "/").text) is None)
    r = set_lang(requests.Session(), "es")
    check("A: choosing a language is refused while users may not choose", r.status_code == 400, r.status_code)
    c = requests.Session(); c.cookies.update(s.cookies); c.cookies.set(".MintQMS.Language", "c%3Des%7Cuic%3Des")
    check("A: a language cookie is ignored while users may not choose", "Quality Events" in nav_text(get(c, "/").text))
    opts = [o.get("value") for o in BeautifulSoup(get(s, "/Settings").text, "html.parser").select("select[name=SiteLanguage] option")]
    check("A: Site Language offers the installed languages", opts == ["en", "es", "pt-BR"], opts)

    # --- B. the administrator sets the site language ---------------------------------------------
    r = settings_save(s, SiteLanguage="es")
    check("B: Administrator sets Site Language to Spanish", r.status_code == 302 and site_settings()["SiteLanguage"] == "es", (r.status_code, site_settings()))
    check("B: the change is in the settings audit trail (en → es)", audited("SiteLanguage", "en", "es"))
    anon_login = requests.get(BASE + "/Identity/Account/Login").text
    check("B: the sign-in page is Spanish for everyone", "Iniciar sesión" in visible(anon_login) and menu(anon_login) is None, visible(anon_login)[:160])
    check("B: the menu bar is Spanish, with no language menu", "Eventos de calidad" in nav_text(get(s, "/").text) and menu(get(s, "/").text) is None)
    c = requests.Session(); c.cookies.update(s.cookies); c.cookies.set(".MintQMS.Language", "c%3Dpt-BR%7Cuic%3Dpt-BR")
    check("B: a user's own cookie cannot override the site language", "Eventos de calidad" in nav_text(get(c, "/").text))
    r = settings_save(s, SiteLanguage="fr")
    check("B: a language that is not installed is refused", site_settings()["SiteLanguage"] == "es", site_settings())

    # --- C. only an Administrator can change it --------------------------------------------------
    ensure_user("qm-lang@e2e.local", ["QualityManager"])
    qm = login("qm-lang@e2e.local")
    f = form_fields(get(qm, "/Settings").text, "/Settings/Save") or {}
    f.update({"SiteLanguage": "pt-BR", "AllowUserLanguageChoice": "true",
              "__RequestVerificationToken": token(get(qm, "/Settings").text)})
    r = qm.post(BASE + "/Settings/Save", data=f, allow_redirects=False)
    check("C: a Quality Manager cannot change the language settings (refused / Access Denied)",
          r.status_code == 403 or (r.status_code == 302 and "AccessDenied" in (r.headers.get("Location") or "")),
          (r.status_code, r.headers.get("Location")))
    check("C: … and nothing changed", site_settings() == {"SiteLanguage": "es", "AllowUserLanguageChoice": 0}, site_settings())
    s = login()

    # --- D. the administrator lets users choose (site back to English) ----------------------------
    r = settings_save(s, SiteLanguage="en", AllowUserLanguageChoice="true")
    check("D: Administrator allows user choice", site_settings() == {"SiteLanguage": "en", "AllowUserLanguageChoice": 1}, site_settings())
    check("D: the change is in the settings audit trail (False → True)", audited("AllowUserLanguageChoice", "False", "True"))
    login_page = requests.get(BASE + "/Identity/Account/Login").text

    # --- E. the endpoint's guards (only reachable while users may choose) -------------------------
    r = set_lang(requests.Session(), "en", with_token=False)
    check("E: POST without anti-forgery token refused", r.status_code == 400, r.status_code)
    r = set_lang(requests.Session(), "fr")
    check("E: unknown language refused", r.status_code == 400, r.status_code)
    r = set_lang(requests.Session(), "en", return_url="https://evil.example/x")
    check("E: never redirects off-site", r.status_code == 302 and r.headers.get("Location") == "/",
          (r.status_code, r.headers.get("Location")))
    r = set_lang(requests.Session(), "en", return_url="//evil.example/x")
    check("E: protocol-relative return URL refused", r.headers.get("Location") == "/", r.headers.get("Location"))

    # --- F. users choose ---------------------------------------------------------------------------
    check("F: menu on the sign-in page offers en, es, pt-BR", menu(login_page) == ["en", "es", "pt-BR"], menu(login_page))
    home_en = get(s, "/").text
    check("F: menu in the menu bar", menu(home_en) == ["en", "es", "pt-BR"], menu(home_en))
    r = set_lang(s, "es", page="/", return_url="/AppDocuments")
    check("F: choosing Spanish redirects back to the page", r.status_code == 302 and
          r.headers.get("Location") == "/AppDocuments", (r.status_code, r.headers.get("Location")))
    ck = r.headers.get("Set-Cookie", "")
    check("F: the choice is kept in the language cookie", ".MintQMS.Language=" in ck and "uic%3Des" in ck, ck)
    check("F: cookie is HttpOnly and SameSite=Lax", "httponly" in ck.lower() and "samesite=lax" in ck.lower(), ck)
    home_es = get(s, "/").text
    sel = BeautifulSoup(home_es, "html.parser").find("select", {"id": "mintLanguage"})
    check("F: the menu now shows Spanish as selected",
          sel is not None and (sel.find("option", selected=True) or {}).get("value") == "es")
    day = re.compile(r"(Monday|Tuesday|Wednesday|Thursday|Friday|Saturday|Sunday), \d{1,2} \w+ \d{4}")
    m_en, m_es = day.search(visible(home_en)), day.search(visible(home_es))
    check("F: date heading unchanged with Spanish selected (format culture pinned)",
          m_en and m_es and m_en.group(0) == m_es.group(0), (m_en and m_en.group(0), m_es and m_es.group(0)))
    s_en = requests.Session(); s_en.cookies.update({k: v for k, v in s.cookies.items() if k != ".MintQMS.Language"})
    for path in ["/", "/AppDocuments", "/AppDocuments/Create", "/Deviations", "/Deviations/Create", "/Trainings", "/Settings"]:
        es_html, en_html = get(s, path).text, s_en.get(BASE + path).text
        nt = nav_text(es_html)
        check(f"F: {path} shows the menu in Spanish", "Eventos de calidad" in nt and "Quality Events" not in nt, nt[:120])
        check(f"F: {path} stays English without the cookie", "Quality Events" in en_html and "Eventos de calidad" not in en_html)
        leaks = re.findall(r'@?\b[LH]\["[^"]{0,40}', visible(es_html) + visible(en_html))
        check(f"F: {path} shows no raw translation key", not leaks, leaks[:3])
    r = set_lang(s, "pt-br", page="/")
    check("F: language codes accepted in any case", r.status_code == 302 and "uic%3Dpt-BR" in r.headers.get("Set-Cookie", ""),
          r.headers.get("Set-Cookie"))
    ensure_user("readonly-lang@e2e.local", ["ReadOnly"])
    ro = login("readonly-lang@e2e.local")
    r = set_lang(ro, "es", page="/")
    check("F: ReadOnly user can choose a language", r.status_code == 302, r.status_code)
    before = q1("select count(*) n from Deviation")["n"]
    r = post_form(ro, "/Deviations/Create", "/Deviations", {"Title": "should-not-exist", "Description": "x"})
    after = q1("select count(*) n from Deviation")["n"]
    check("F: ReadOnly user still cannot create a record (filter intact)",
          r.status_code in (302, 403) and after == before and
          (r.status_code == 403 or "AccessDenied" in r.headers.get("Location", "")),
          (r.status_code, r.headers.get("Location"), before, after))

    # --- G. choice switched off again: personal choices are ignored --------------------------------
    s = login()
    settings_save(s, AllowUserLanguageChoice="false")
    c = requests.Session(); c.cookies.update(s.cookies); c.cookies.set(".MintQMS.Language", "c%3Dpt-BR%7Cuic%3Dpt-BR")
    check("G: with choice off again, a Portuguese cookie is ignored (site language shown)", "Quality Events" in nav_text(get(c, "/").text))
    check("G: … and the menu is gone", menu(get(c, "/").text) is None)
    settings_save(s, AllowUserLanguageChoice="true")      # leave users free to choose for later checks

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
