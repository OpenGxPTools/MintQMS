"""Helpers for walking a module's workflow through its own pages: open the record page, find the form a button
opens, fill in the e-signature and empty text fields, and submit — so a walk also proves the step is offered on
the page in that state."""
from bs4 import BeautifulSoup
from e2e_lib import *


def find_form(html, action, match=None):
    """(post URL, fields) of the first form whose action path ends with `action` (e.g. "/Release/Certify") and,
    when `match` is given, whose fields hold those values (e.g. {"newStatus": "Applied"})."""
    soup = BeautifulSoup(html, "html.parser")
    for f in soup.find_all("form"):
        url = (f.get("action") or "").split("?")[0]
        if match and not all(any(i.get("name") == k and str(i.get("value")) == str(v) for i in f.find_all(["input", "button"]))
                             for k, v in match.items()):
            continue
        if url.rstrip("/").lower().endswith(action.lower()) or ("/" + action.strip("/").lower() + "/") in (url.lower() + "/"):
            fields = {}
            for el in f.find_all(["input", "select", "textarea"]):
                n, t = el.get("name"), (el.get("type") or "text").lower()
                if not n or t in ("submit", "button", "file"):
                    continue
                if el.name == "select":
                    opt = el.find("option", selected=True) or next((o for o in el.find_all("option") if o.get("value")), None)
                    fields[n] = opt.get("value", opt.text) if opt else ""
                elif el.name == "textarea":
                    fields[n] = el.text.lstrip("\n")
                elif t in ("checkbox", "radio"):
                    if el.has_attr("checked"):
                        fields[n] = el.get("value", "on")
                else:
                    fields.setdefault(n, el.get("value", ""))
                fields.setdefault("__types", {})[n] = "textarea" if el.name == "textarea" else ("select" if el.name == "select" else t)
            return f.get("action"), fields
    return None, None


def step(s, page, action, signer=ADMIN, extra=None, fill=True, match=None):
    """Submit the form for `action` found on `page`. Returns (response, posted fields) or (None, None) when the
    page offers no such form."""
    url, f = find_form(get(s, page).text, action, match)
    if url is None:
        return None, None
    types = f.pop("__types", {})
    for k, t in types.items():
        kl = k.lower()
        if "password" in kl or kl in ("pwd", "inputpassword"):
            f[k] = PW
        elif kl.endswith("email") and t in ("email", "text", "hidden"):
            f[k] = signer
        elif fill and t in ("text", "textarea") and not f.get(k) and k != "__RequestVerificationToken":
            f[k] = "e2e"
    f.update(extra or {})
    r = s.post(BASE + url, data=f, allow_redirects=False)
    return r, f


def page_message(s, r):
    """The error/success alert text on the page the step redirected to."""
    loc = r.headers.get("Location") if r is not None else None
    if not loc:
        return ""
    soup = BeautifulSoup(get(s, loc).text, "html.parser")
    return " | ".join(" ".join(e.get_text(" ").split()) for e in soup.select(".alert-danger, .alert-success, .alert-warning"))
