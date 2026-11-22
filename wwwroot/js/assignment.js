// Adds an optional "Assign the next step to" list to each signature dialog of a workflow record page whose form
// posts to one of the actions in #assign-next-data (written by the Assignment view component). Only people who can
// do the step after it are listed, and the person planned for that step is chosen already; the server checks the
// choice again once the step is signed. Also opens the responsibilities dialog when the page was opened with ?plan.
(function () {
    "use strict";
    // The panel (and this script) comes before the page's signature dialogs: wait until the page is complete.
    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", run);
    } else {
        run();
    }

    function run() {
    var el = document.getElementById("assign-next-data");
    if (!el) return;
    var data;
    try { data = JSON.parse(el.textContent); } catch (e) { return; }

    if (data.modal) {
        var dlg = document.getElementById(data.modal);
        if (dlg && dlg.getAttribute("data-open") === "1" && window.bootstrap) {
            window.bootstrap.Modal.getOrCreateInstance(dlg).show();
        }
    }

    function path(form) {
        try { return new URL(form.getAttribute("action") || "", window.location.href).pathname; } catch (e) { return ""; }
    }

    document.querySelectorAll("form").forEach(function (form) {
        var people = data.actions[path(form)];
        if (!people || people.length === 0 || form.querySelector("[name='" + data.field + "']")) return;

        var group = document.createElement("div");
        group.className = "mb-3 assign-next";
        var id = "assign-next-" + Math.random().toString(36).slice(2);
        var label = document.createElement("label");
        label.className = "form-label";
        label.htmlFor = id;
        label.textContent = data.label;
        var select = document.createElement("select");
        select.className = "form-select";
        select.name = data.field;
        select.id = id;
        var none = document.createElement("option");
        none.value = "";
        none.textContent = data.none;
        select.appendChild(none);
        var planned = (data.planned || {})[path(form)];
        people.forEach(function (p) {
            var o = document.createElement("option");
            o.value = String(p.id);
            o.textContent = p.name;
            if (planned === p.id) o.selected = true;
            select.appendChild(o);
        });
        group.appendChild(label);
        group.appendChild(select);

        // inside the dialog body when there is one, otherwise just before the submit button
        var body = form.querySelector(".modal-body");
        var submit = form.querySelector("[type='submit']");
        if (body) body.appendChild(group);
        else if (submit) submit.parentNode.insertBefore(group, submit);
        else form.appendChild(group);
    });
    }
})();

// Responsibilities dialog: as people are chosen, mark a step whose person the four-eyes rule keeps out because they
// are also named for an earlier step it checks (data-not-same), or because they raised the record and also do the
// step that must be someone else's (data-raiser-other). Save is disabled while a step is marked; the server checks
// the same rules again.
(function () {
    "use strict";
    function run() {
        var form = document.getElementById("plan-form");
        var textsEl = document.getElementById("plan-texts");
        if (!form || !textsEl) return;
        var texts;
        try { texts = JSON.parse(textsEl.textContent); } catch (e) { return; }
        var steps = Array.prototype.slice.call(form.querySelectorAll(".plan-step"));
        var byStage = {};
        steps.forEach(function (st) { byStage[st.getAttribute("data-stage")] = st; });
        function sel(st) { return st.querySelector("select.plan-user"); }
        function who(st) { var s = sel(st); return s && s.value !== "0" ? s.value : null; }
        function name(st) { var s = sel(st); return s ? s.options[s.selectedIndex].textContent.replace(/\s*\(.*\)\s*$/, "") : ""; }
        function fmt(t, a, b) { return t.replace("{0}", a).replace("{1}", b); }
        function check() {
            var bad = false;
            steps.forEach(function (st) {
                var s = sel(st), msg = "", me = who(st);
                if (me) {
                    (st.getAttribute("data-not-same") || "").split(",").filter(Boolean).forEach(function (stage) {
                        var other = byStage[stage];
                        if (!msg && other && who(other) === me) msg = fmt(texts.clash, name(st), "“" + other.getAttribute("data-task") + "”");
                    });
                    var ro = st.getAttribute("data-raiser-other");
                    if (!msg && ro && byStage[ro] && texts.raiserId != null && String(texts.raiserId) === me && who(byStage[ro]) === me)
                        msg = fmt(texts.raiser, name(st), "“" + byStage[ro].getAttribute("data-task") + "”");
                }
                s.classList.toggle("is-invalid", !!msg);
                st.querySelector(".plan-clash").textContent = msg;
                if (msg) bad = true;
            });
            var save = document.getElementById("assign-save");
            if (save) save.disabled = bad;
        }
        steps.forEach(function (st) { var s = sel(st); if (s) s.addEventListener("change", check); });
        check();
    }
    if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", run); else run();
})();
