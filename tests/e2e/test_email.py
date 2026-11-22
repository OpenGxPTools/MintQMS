"""E-mail (1.2.0 phase 4): outbox, assignment e-mails, test e-mail, SMTP password protection.

  * Send Assignment E-mails is off on a new site; it cannot be turned on without an SMTP host and From address; the
    Site Web Address must be a full http(s) address;
  * the SMTP password is stored protected ("dp1:…"), never shown on the settings page, never in the settings log;
  * assigning a task queues one e-mail in the same save (not when the setting is off, not to oneself); the e-mail holds
    the type of record, number, the task, due date, who assigned it, the note and a link — and NO record content
    (title, description); written in the site language;
  * with a mail server: the message is delivered over STARTTLS with sign-in; when the server refuses, the workflow is
    unaffected, the error is kept and shown in System Settings, retried after a back-off, and marked Failed after the
    last attempt; "Send test e-mail to me" reports success or the error, and is logged.
The delivery checks need a mail server certificate the application trusts: E2E_SMTP_CERT_DIR (cert.pem, key.pem) with
the application started with SSL_CERT_FILE including that certificate. Without it they are skipped (said so).
"""
import os, time
from workflow_lib import *
from smtp_test_server import SmtpTestServer

QM1, QU = "em-qm1@e2e.local", "em-qu@e2e.local"
ids = {QM1: ensure_user(QM1, ["QualityManager"], "Elsa", "Manager"), QU: ensure_user(QU, ["QualityUser"], "Omar", "User")}
four_eyes(True)


def settings_save(s, **changes):
    page = get(s, "/Settings").text
    f = form_fields(page, "/Settings/Save")
    f.pop("logoFile", None)
    for req in ("OrganisationName", "OrganisationAddress"):
        if not f.get(req):
            f[req] = "E2E Test Organisation"
    f.update({k: v for k, v in changes.items()})
    return s.post(BASE + "/Settings/Save", data=f, allow_redirects=False)


def setting(field):
    return q1(f'select "{field}" v from SystemSettings')["v"]


def outbox(rid=None):
    return q("select * from EmailOutbox" + (" where RecordId=? order by Id" if rid else " order by Id"), (rid,) if rid else ())


m = module("Deviations")


def new_dev():
    rid = new_record(m["table"], m["uniq"], m["prefix"], {**m["init"], "Status": 1, "Title": "SECRET-TITLE-e2e",
                                                         "Identification": "SECRET-DESCRIPTION-e2e"})
    return rid


def assign(s, rid, uid, note="please check", due="2026-12-24"):
    return post_form(s, "/TaskAssignments/Assign", f"/Deviations/Details/{rid}",
                     {"module": "Deviations", "recordId": rid, "userId": uid, "dueDate": due, "note": note})


admin = login()
# ── defaults and validation ───────────────────────────────────────────────────────────────────────────
get(admin, "/Settings")
exec_sql("update SystemSettings set SendAssignmentEmails=0, SmtpHost=null, EmailFromAddress=null, SmtpPassword=null, SiteWebAddress=null")
exec_sql("delete from EmailOutbox")
check("assignment e-mails are off by default (new column)", setting("SendAssignmentEmails") == 0)
r = settings_save(admin, SendAssignmentEmails="true")
check("cannot be turned on without an SMTP host and From address", r.status_code == 200 and setting("SendAssignmentEmails") == 0)
r = settings_save(admin, SiteWebAddress="qms.example.com")
check("site web address must be a full http(s) address", r.status_code == 200 and setting("SiteWebAddress") is None)
dev = new_dev()
assign(login(QM1), dev, ids[QU])
check("with the setting off, assigning queues no e-mail", not outbox(dev) and q("select * from TaskAssignments where Module='Deviations' and RecordId=? and IsCurrent=1", (dev,)))

# ── configure; the password is protected ──────────────────────────────────────────────────────────────
cert_dir = os.environ.get("E2E_SMTP_CERT_DIR")
server = SmtpTestServer(os.path.join(cert_dir, "cert.pem"), os.path.join(cert_dir, "key.pem")) if cert_dir else None
port = server.port if server else 2525
PLAIN = "s3cret-Pw!"
r = settings_save(admin, SmtpHost="localhost", SmtpPort=str(port), SmtpUsername="mailer", SmtpPassword=PLAIN,
                  EmailFromAddress="qms@e2e.local", EmailFromName="MintQMS e2e", SiteWebAddress="http://localhost:5099/",
                  SendAssignmentEmails="true")
check("settings saved (SMTP, site address, e-mails on)", r.status_code == 302 and setting("SendAssignmentEmails") == 1
      and setting("SiteWebAddress") == "http://localhost:5099", (r.status_code, setting("SiteWebAddress")))
stored = setting("SmtpPassword") or ""
check("SMTP password stored protected, not as typed", stored.startswith("dp1:") and PLAIN not in stored)
page = get(admin, "/Settings").text
check("settings page never shows the password (plain or protected)", PLAIN not in page and "dp1:" not in page)
log = " ".join(f"{r_['OldValue']} {r_['NewValue']}" for r_ in q("select * from SettingsAuditLog where FieldName='SmtpPassword'"))
check("settings log records only that the password was set", PLAIN not in log and "dp1:" not in log and "set" in log, log)
r = settings_save(admin)                      # saving again with the password field blank keeps it
check("saving with the password left blank keeps it", setting("SmtpPassword") == stored)

# ── assignment e-mail content ─────────────────────────────────────────────────────────────────────────
dev = new_dev()
assign(login(QM1), dev, ids[QU], note="see batch 7")
rows = outbox(dev)
check("assigning queues one e-mail", len(rows) == 1 and rows[0]["Status"] == "Pending" and rows[0]["Kind"] == "TaskAssigned"
      and rows[0]["ToAddress"] == QU, rows)
mail = rows[0] if rows else {"Subject": "", "HtmlBody": ""}
num = row(m["table"], dev)["DeviationId"]
body = mail["HtmlBody"]
check("e-mail: record type and number, the task", num in mail["Subject"] and "Deviations" in mail["Subject"]
      and "Evaluate" in mail["Subject"] and "Evaluate" in body, mail["Subject"])
check("e-mail: due date, who assigned it, the note", "24 Dec 2026" in body and "Elsa Manager" in body and "see batch 7" in body)
check("e-mail: link to the record", f"http://localhost:5099/Deviations/Details/{dev}" in body)
check("e-mail: no record content (title, description)", "SECRET-TITLE" not in body + mail["Subject"] and "SECRET-DESCRIPTION" not in body)
dev2 = new_dev()
s = login(QM1)
assign(s, dev2, ids[QM1])
check("assigning yourself sends no e-mail", not outbox(dev2) and q("select * from TaskAssignments where Module='Deviations' and RecordId=? and IsCurrent=1", (dev2,)))
exec_sql("update SystemSettings set SiteLanguage='es'")
dev3 = new_dev()
assign(login(QM1), dev3, ids[QU])
es = outbox(dev3)
check("e-mail in the site language (Spanish)", es and "Desviaciones" in es[0]["Subject"] and "Tarea" in es[0]["HtmlBody"] and "Evaluar" in es[0]["Subject"],
      es[0]["Subject"] if es else None)
exec_sql("update SystemSettings set SiteLanguage='en'")

# ── delivery, retries, failure, test e-mail ───────────────────────────────────────────────────────────
def wait(cond, secs=75):
    end = time.time() + secs
    while time.time() < end:
        if cond():
            return True
        time.sleep(1)
    return False


if not server:
    print("SKIP delivery checks: E2E_SMTP_CERT_DIR not set (no trusted test mail server)")
else:
    check("delivered: the outbox row is marked Sent", wait(lambda: outbox(dev)[0]["Status"] == "Sent"), outbox(dev)[0]["LastError"])
    got = [x for x in server.messages if num in x["subject"]]
    check("delivered to the assignee over STARTTLS with sign-in", got and got[0]["to"] == [QU] and got[0]["tls"] and got[0]["authed"], got[:1])
    check("…the received message is the queued one", got and f"/Deviations/Details/{dev}" in got[0]["html"])

    server.refuse = True
    dev4 = new_dev()
    before = time.time()
    assign(login(QM1), dev4, ids[QU])
    check("server refusing: the assignment is saved anyway", q("select * from TaskAssignments where Module='Deviations' and RecordId=? and IsCurrent=1", (dev4,)))
    ok = wait(lambda: outbox(dev4)[0]["Attempts"] >= 1)
    r4 = outbox(dev4)[0]
    check("server refusing: attempt counted, error kept, still waiting", ok and r4["Status"] == "Pending" and "451 Temporary failure" in (r4["LastError"] or ""), dict(r4))
    page = get(admin, "/Settings").text
    soup = BeautifulSoup(page, "html.parser")
    check("settings page shows the outbox problem", soup.select_one("#outbox-last-error") is not None and "Temporary failure" in soup.select_one("#outbox-last-error").get_text())
    exec_sql("update EmailOutbox set Attempts=5, NextAttemptOn=datetime('now','-1 minute') where Id=?", (r4["Id"],))
    check("after the last retry the message is marked Failed", wait(lambda: outbox(dev4)[0]["Status"] == "Failed"))
    server.refuse = False
    # back-off: a fresh failure waits about a minute before the next attempt
    server.refuse = True
    dev5 = new_dev()
    assign(login(QM1), dev5, ids[QU])
    wait(lambda: outbox(dev5)[0]["Attempts"] >= 1)
    r5 = outbox(dev5)[0]
    nxt = q1("select (julianday(NextAttemptOn) - julianday('now')) * 1440 m from EmailOutbox where Id=?", (r5["Id"],))["m"]
    check("retry waits about 1 minute after the first failure", 0.3 < nxt <= 1.05, nxt)
    server.refuse = False
    check("…and is then delivered", wait(lambda: outbox(dev5)[0]["Status"] == "Sent", 120))

    n0 = len(server.messages)
    r = post_form(admin, "/Settings/SendTestEmail", "/Settings", {})
    soup = BeautifulSoup(get(admin, "/Settings").text, "html.parser")
    check("test e-mail: sent to the Administrator, success shown", any(x["subject"] == "MintQMS test e-mail" and x["to"] == [ADMIN]
          for x in server.messages[n0:]) and soup.select_one(".alert-success") is not None)
    exec_sql("update SystemSettings set SmtpPort=1")
    post_form(admin, "/Settings/SendTestEmail", "/Settings", {})
    soup = BeautifulSoup(get(admin, "/Settings").text, "html.parser")
    check("test e-mail: a failure is reported on the page", soup.select_one("#settings-error") is not None)
    logs = [x["NewValue"] for x in q("select NewValue from SettingsAuditLog where FieldName='TestEmail' order by Id")]
    check("test e-mail: both attempts in the settings log", len(logs) >= 2 and "sent" in logs[-2] and "failed" in logs[-1], logs[-2:])
    exec_sql("update SystemSettings set SmtpPort=?", (port,))
    server.stop()

r = post_form(login(QU), "/Settings/SendTestEmail", "/", {})
check("test e-mail: only an Administrator", r.status_code == 403 or "AccessDenied" in r.headers.get("Location", ""), r.headers.get("Location"))

# leave the site as found: e-mails off, no SMTP (other suites run after this one)
exec_sql("update SystemSettings set SendAssignmentEmails=0, SmtpHost=null, EmailFromAddress=null, SmtpPassword=null, SiteWebAddress=null, SmtpUsername=null, EmailFromName=null, SmtpPort=587")
sys.exit(summary())
