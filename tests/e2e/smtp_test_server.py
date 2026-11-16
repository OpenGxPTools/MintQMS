"""A minimal SMTP server for the e2e tests: STARTTLS (certificate given), AUTH PLAIN / LOGIN, keeps received messages
in memory. `refuse=True` answers every message with a temporary failure (to test retries). Not for any other use."""
import base64, email, socket, ssl, threading
from email import policy


class SmtpTestServer:
    def __init__(self, certfile, keyfile, user="mailer", password="s3cret-Pw!"):
        self.user, self.password = user, password
        self.ctx = ssl.create_default_context(ssl.Purpose.CLIENT_AUTH)
        self.ctx.load_cert_chain(certfile, keyfile)
        self.messages, self.refuse, self.logins = [], False, []
        self.sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        self.sock.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
        self.sock.bind(("127.0.0.1", 0))
        self.port = self.sock.getsockname()[1]
        self.sock.listen(5)
        self._stop = False
        threading.Thread(target=self._serve, daemon=True).start()

    def stop(self):
        self._stop = True
        try:
            self.sock.close()
        except OSError:
            pass

    def _serve(self):
        while not self._stop:
            try:
                conn, _ = self.sock.accept()
            except OSError:
                return
            threading.Thread(target=self._session, args=(conn,), daemon=True).start()

    def _session(self, conn):
        f = conn.makefile("rb")
        def send(line): conn.sendall((line + "\r\n").encode())
        def read(): return f.readline().decode("utf-8", "replace").rstrip("\r\n")
        tls, authed, mail_from, rcpt = False, False, None, []
        try:
            send("220 localhost ESMTP e2e")
            while True:
                line = read()
                cmd = line.upper()
                if not line and f.closed:
                    return
                if cmd.startswith(("EHLO", "HELO")):
                    send("250-localhost")
                    if not tls:
                        send("250-STARTTLS")
                    send("250 AUTH PLAIN LOGIN")
                elif cmd == "STARTTLS":
                    send("220 Ready to start TLS")
                    conn = self.ctx.wrap_socket(conn, server_side=True)
                    f = conn.makefile("rb")
                    tls = True
                elif cmd.startswith("AUTH PLAIN"):
                    parts = line.split()
                    if len(parts) < 3:
                        send("334 "); parts.append(read())
                    _, u, p = base64.b64decode(parts[2]).decode().split("\0")
                    authed = tls and u == self.user and p == self.password
                    self.logins.append((u, authed))
                    send("235 OK" if authed else "535 Authentication failed")
                elif cmd.startswith("AUTH LOGIN"):
                    send("334 VXNlcm5hbWU6"); u = base64.b64decode(read()).decode()
                    send("334 UGFzc3dvcmQ6"); p = base64.b64decode(read()).decode()
                    authed = tls and u == self.user and p == self.password
                    self.logins.append((u, authed))
                    send("235 OK" if authed else "535 Authentication failed")
                elif cmd.startswith("MAIL FROM"):
                    if self.refuse:
                        send("451 Temporary failure (e2e)"); continue
                    mail_from, rcpt = line[10:].strip(), []
                    send("250 OK")
                elif cmd.startswith("RCPT TO"):
                    rcpt.append(line[8:].strip().strip("<>")); send("250 OK")
                elif cmd == "DATA":
                    send("354 End with <CRLF>.<CRLF>")
                    data = []
                    while True:
                        l = f.readline()
                        if l in (b".\r\n", b".\n", b""):
                            break
                        data.append(l[1:] if l.startswith(b"..") else l)
                    msg = email.message_from_bytes(b"".join(data), policy=policy.default)
                    body = msg.get_body(preferencelist=("html", "plain"))
                    self.messages.append({"from": mail_from, "to": rcpt, "subject": str(msg["subject"]),
                                          "html": body.get_content() if body else "", "tls": tls, "authed": authed})
                    send("250 OK queued")
                elif cmd in ("RSET", "NOOP"):
                    send("250 OK")
                elif cmd == "QUIT":
                    send("221 Bye"); return
                else:
                    send("502 Not implemented")
        except Exception:
            return
        finally:
            try:
                conn.close()
            except Exception:
                pass
