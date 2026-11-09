# Contributing to MintQMS

Thank you for your interest. MintQMS is a community project maintained on a best-effort basis.

## Feedback

- **Questions, ideas and experiences:** use GitHub **Discussions**.
- **Bugs:** open an **Issue** using the bug-report form. Include the steps to reproduce it, what you expected
  and what happened.
- **Feature requests:** open an **Issue** using the feature-request form. Describe the quality process it
  supports, and the guideline it relates to if there is one.
- **Security problems:** follow [SECURITY.md](SECURITY.md) and never post them publicly.

Never include real company data, personal data, patient data or adverse-event details in issues, discussions
or screenshots.

## Code contributions

1. Open an issue first to discuss larger changes.
2. Keep each pull request focused on one change, and match the style of the surrounding code.
3. Workflow, signature, audit-trail and access-control behaviour are the core of a QMS. Explain any change to
   them clearly in the pull request.
4. Run the end-to-end tests in [`tests/e2e`](tests/e2e) (see its README) and add tests for what you change.
5. Update the [User & Operations Guide](docs/MintQMS_User_and_Operations_Guide.md) or
   [DEPLOYMENT.md](DEPLOYMENT.md) when behaviour or installation changes.

## Interface text and translations

MintQMS can be shown in English, Spanish and Portuguese (Brazil). English text in the code is the
translation key, and anything without a translation is shown in English.

- **Text on a page:** write `@L["Save changes"]`, not plain `Save changes`. A sentence with a link or
  bold part inside is translated whole: `@H["Click <strong>{0}</strong> to continue.", L["Stay Signed In"].Value]`.
- **States, types and roles on a page:** `@L.Enum(item.Status)` and `@L.Role(name)`. Never use these
  when writing records — `EnumHelper.Display` stays English for audit entries, PDFs and stored text.
- **Messages from page models and controllers:** `this.T("Invalid login attempt.")`.
- **Dates on a page:** `x.ToString("dd MMM yyyy", DisplayDates.Culture)` or `DisplayDates.Long(x)` — in views
  only. Dates written into records, audit entries, exports and PDFs keep the server's format.
- **Never translate anything that is stored** (audit-trail actions, record fields, file names) or
  compared in code or scripts (e.g. `data-status` values must match what the filtered column shows).
- Add the Spanish and Portuguese to `Resources/Localization/translations.json`, then run
  `tools/i18n.py build` and `tools/i18n.py check` (the check fails on missing translations,
  mismatched `{0}` placeholders, HTML in translations and out-of-date resource files).
  `tools/i18n.py review out.csv` exports a sheet for a native reader.
- `tools/i18n_wrap.py` can wrap plain text in views for you; it skips anything that looks like code.
  Always build and run the e2e tests afterwards.

By submitting a contribution you agree that it is licensed under the GNU AGPL version 3, the licence of this
project, and that you have the right to submit it.
