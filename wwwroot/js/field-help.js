// Field-level help popovers.
//
// Mirrors the "?" guidance mechanism used on the Deviations form, extracted so
// any form can reuse it. Markup contract:
//
//   <label>Field Name
//       <button type="button" class="btn btn-link btn-sm p-0 ms-1 help-btn"
//               data-help="mykey" aria-label="Field Name guidance">?</button>
//   </label>
//   ...
//   <div class="d-none">
//       <div id="help-mykey"> ...rich guidance HTML... </div>
//   </div>
//
// Clicking the "?" shows the matching #help-<key> content as a popover; clicking
// elsewhere dismisses open popovers. Requires Bootstrap's bundle (loaded globally).
(function () {
    'use strict';
    function init() {
        if (!window.bootstrap || !window.bootstrap.Popover) { return; }
        document.querySelectorAll('.help-btn').forEach(function (btn) {
            if (btn.dataset.helpInit) { return; }
            btn.dataset.helpInit = '1';
            var contentEl = document.getElementById('help-' + btn.dataset.help);
            new bootstrap.Popover(btn, {
                html: true,
                trigger: 'click',
                placement: 'right',
                content: contentEl ? contentEl.innerHTML : '',
                title: '',
                container: 'body'
            });
        });
        document.addEventListener('click', function (e) {
            if (!e.target.classList.contains('help-btn')) {
                document.querySelectorAll('.help-btn').forEach(function (btn) {
                    var pop = bootstrap.Popover.getInstance(btn);
                    if (pop) { pop.hide(); }
                });
            }
        });
    }
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
