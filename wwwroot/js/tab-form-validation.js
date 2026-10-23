// Tab-aware form validation helper.
//
// Multi-tab Create/Edit forms hide inactive tabs with display:none. jQuery
// Validate's default `ignore: ":hidden"` therefore skips fields on other tabs,
// so a validation error on a tab you aren't looking at lets the form submit,
// the server rejects it, and the page silently re-renders — "nothing happens".
//
// Include this script (after _ValidationScriptsPartial, if used) on any tabbed
// form. It is safe on forms without tabs or without jQuery Validate.
//
//   1. If a jQuery Validate validator exists, validate hidden-tab fields too and,
//      on an invalid submit, switch to the tab holding the first error.
//   2. On a server-rejected re-render, jump to the first errored field's tab and
//      scroll the error banner into view.
(function () {
    'use strict';
    if (!window.jQuery) { return; }

    function showTabFor(el) {
        var $pane = jQuery(el).closest('.tab-pane');
        if (!$pane.length) { return; }
        var $btn = jQuery('[data-bs-target="#' + $pane.attr('id') + '"]');
        if ($btn.length && window.bootstrap && window.bootstrap.Tab) {
            new window.bootstrap.Tab($btn[0]).show();
        }
    }

    jQuery(function ($) {
        var $form = $('form').filter(function () { return $(this).find('.tab-pane').length > 0; }).first();
        if (!$form.length) { $form = $('form').first(); }

        // 1) Client-side: make validation tab-aware.
        var validator = $form.data('validator');
        if (validator) {
            validator.settings.ignore = [];
            validator.settings.invalidHandler = function (event, v) {
                if (v && v.errorList && v.errorList.length) {
                    var el = v.errorList[0].element;
                    showTabFor(el);
                    setTimeout(function () {
                        try { el.scrollIntoView({ block: 'center' }); $(el).trigger('focus'); } catch (e) { }
                    }, 150);
                }
            };
        }

        // 2) Server-side: a rejected save re-renders with error markers.
        var $firstErr = $('.input-validation-error, .field-validation-error').first();
        if ($firstErr.length) {
            showTabFor($firstErr[0]);
            var $banner = $('#saveErrorBanner, .alert-danger').first();
            var $scroll = $banner.length ? $banner : $firstErr;
            try { $scroll[0].scrollIntoView({ block: 'start' }); } catch (e) { }
        }
    });
})();
