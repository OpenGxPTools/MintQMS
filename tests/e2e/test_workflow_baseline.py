"""Workflow baseline: every signed step of every module, and who may sign it.

For each module a record is taken through every signed step (and every send-back / re-open route) as the
Administrator. Each step records the state it lands in, which record fields it changed and the audit-trail
entries it wrote. Then, from an exact copy of the record as it was just before each step, the step is tried
once by a user holding each single role: allowed (lands in the expected state) or refused (nothing changes).
The Administrator is tried first on every copy, so a copy that cannot be signed at all is reported, not
mistaken for a role refusal.

The result is compared with tests/e2e/baseline/workflow_baseline.json, the behaviour of MintQMS 1.1.0.
Run with E2E_WRITE_BASELINE=1 to (re)write that file — only on a version whose behaviour is the reference.
"""
import json, os, re
from workflow_lib import *

HERE = os.path.dirname(os.path.abspath(__file__))
BASELINE = os.path.join(HERE, "baseline", "workflow_baseline.json")
WRITE = os.environ.get("E2E_WRITE_BASELINE") == "1"
ONLY = [m for m in os.environ.get("E2E_MODULES", "").split(",") if m]   # optional: limit to some modules


def e2e_results():
    import e2e_lib
    return e2e_lib._results


# ── run ──────────────────────────────────────────────────────────────────────────────────────────────
def walk(m, s):
    """Administrator walk. Returns the result and a snapshot record (copy source) per state."""
    rid = new_record(m["table"], m["uniq"], m["prefix"], m["init"])
    out, snaps = {"walk": [], "returns": []}, {}
    for action, extra, expected in m["steps"]:
        before = row(m["table"], rid)
        snaps.setdefault(before["Status"], copy_record(m["table"], m["uniq"], m["prefix"], rid))
        ba = max([a["Id"] for a in audit_rows(m, rid)] or [0])
        code, msg = sign(s, m, rid, action, extra, ADMIN)
        st, changed, aud = step_effect(m, rid, before, ba)
        ok = check(f"{m['name']}: {action} {before['Status']} → {expected if expected is not None else st}",
                   st == expected if expected is not None else st != before["Status"], f"got {st}; {msg or code}")
        out["walk"].append({"action": action, "from": before["Status"], "to": st, "changed": changed, "audit": aud})
        check(f"{m['name']}: page renders after {action}", get(s, f"/{m['ctrl']}/Details/{rid}").status_code == 200)
        if not ok:
            break
    snaps.setdefault(row(m["table"], rid)["Status"], copy_record(m["table"], m["uniq"], m["prefix"], rid))
    for frm, action, extra in m["returns"]:
        if frm not in snaps:
            check(f"{m['name']}: {action} from {frm} (no record in that state)", False)
            continue
        cid = copy_record(m["table"], m["uniq"], m["prefix"], snaps[frm])
        before = row(m["table"], cid)
        ba = max([a["Id"] for a in audit_rows(m, cid)] or [0])
        code, msg = sign(s, m, cid, action, extra, ADMIN)
        st, changed, aud = step_effect(m, cid, before, ba)
        if extra.get("_refused"):
            check(f"{m['name']}: {action} from {frm} refused (known behaviour)", st == frm, f"moved to {st}")
        else:
            check(f"{m['name']}: {action} from {frm} moves the record", st != frm, f"stayed {st}; {msg or code}")
        snaps.setdefault(st, copy_record(m["table"], m["uniq"], m["prefix"], cid))   # e.g. Suspended, for Reactivate
        out["returns"].append({"action": action, "from": frm, "to": st, "changed": changed, "audit": aud})
    return out, snaps


def tries_of(m, result):
    """(action, from-state, expected to-state, extra form data) for every walked step and route."""
    extras = {a: e for a, e, _ in m["steps"]}
    out = [(w["action"], w["from"], w["to"], extras.get(w["action"], {})) for w in result["walk"]]
    out += [(r["action"], r["from"], r["to"], dict(e)) for r, (_, _, e) in zip(result["returns"], m["returns"])]
    return out


def role_outcomes(m, snaps, result, s, email):
    """Every step tried once by one user, each from a fresh copy of the record just before the step."""
    out = {}
    for action, frm, to, extra in tries_of(m, result):
        cid = copy_record(m["table"], m["uniq"], m["prefix"], snaps[frm])
        sign(s, m, cid, action, extra, email)
        st = row(m["table"], cid)["Status"]
        out[f"{action}@{frm}"] = ("allowed" if st == to and to != frm else "refused" if st == frm else f"other:{st}")
    return out


def compare(name, got, want):
    if want is None:
        check(f"{name}: in baseline", WRITE, "module missing from the baseline file")
        return
    for part in ("walk", "returns"):
        g = [(x["action"], x["from"], x["to"], x["changed"], x["audit"]) for x in got[part]]
        w = [(x["action"], x["from"], x["to"], x["changed"], x["audit"]) for x in want.get(part, [])]
        for i in range(max(len(g), len(w))):
            a = g[i] if i < len(g) else None
            b = w[i] if i < len(w) else None
            check(f"{name}: {part} {(b or a)[0]} from {(b or a)[1]} same as baseline", a == b, f"now {a} / baseline {b}")
    for key, roles in want.get("roles", {}).items():
        for role, outcome in roles.items():
            now = got["roles"].get(key, {}).get(role)
            if now != outcome:
                check(f"{name}: {key} by {role} same as baseline", False, f"now {now} / baseline {outcome}")
    extra = set(got["roles"]) - set(want.get("roles", {}))
    check(f"{name}: no steps beyond the baseline", not extra, sorted(extra))
    check(f"{name}: role matrix same as baseline ({len(want.get('roles', {}))} steps × {len(ROLES)} roles)",
          all(got["roles"].get(k) == v for k, v in want.get("roles", {}).items()))


users = {r: (ADMIN if r == "Administrator" else ensure_user(f"bl-{r.lower()}@e2e.local", [r], "BL", r)) for r in ROLES}
emails = {r: (ADMIN if r == "Administrator" else f"bl-{r.lower()}@e2e.local") for r in ROLES}
# The baseline is 1.1.0 behaviour, which had no four-eyes rule: with the rule off, every step must behave exactly
# as then (the Administrator walks every step alone). test_four_eyes.py covers the rule itself.
four_eyes(False)
baseline = json.load(open(BASELINE, encoding="utf-8")) if os.path.exists(BASELINE) else {}
results = {}
for m in MODULES:
    if ONLY and m["name"] not in ONLY:
        continue
    admin = login()
    res, snaps = walk(m, admin)
    matrix = {}
    # one role at a time: a new login ends the previous session (one active session per user is enforced)
    for role in ROLES:
        for k, v in role_outcomes(m, snaps, res, login(emails[role]), emails[role]).items():
            matrix.setdefault(k, {})[role] = v
    for k, v in matrix.items():
        refused_route = any(r[1] == k.split("@")[0] and str(r[0]) == k.split("@")[1] and r[2].get("_refused")
                            for r in m["returns"])
        if v["Administrator"] != "allowed" and not refused_route:
            check(f"{m['name']}: {k} can be signed from a copy (Administrator)", False, v["Administrator"])
    res["roles"] = matrix
    results[m["name"]] = res
    if not WRITE:
        compare(m["name"], res, baseline.get(m["name"]))

if WRITE and all(ok for _, ok in e2e_results()):
    os.makedirs(os.path.dirname(BASELINE), exist_ok=True)
    merged = {**baseline, **results}
    json.dump(merged, open(BASELINE, "w", encoding="utf-8"), indent=1, ensure_ascii=False, sort_keys=True)
    print(f"baseline written: {BASELINE} ({', '.join(results)})")
elif WRITE:
    print("baseline NOT written: fix the failures first")
four_eyes(True)
sys.exit(summary())
