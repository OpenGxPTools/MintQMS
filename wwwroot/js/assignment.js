// Adds an optional "Assign the next step to" list to each signature dialog of a workflow record page whose form
// posts to one of the actions in #assign-next-data (written by the Assignment view component). Only people who can
// do the step after it are listed; the server checks the choice again once the step is signed.
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
        people.forEach(function (p) {
            var o = document.createElement("option");
            o.value = String(p.id);
            o.textContent = p.name;
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
