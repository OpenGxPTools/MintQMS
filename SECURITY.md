# Security policy

## Reporting a vulnerability

Please **do not** report security problems in a public issue or discussion.

Use GitHub's private reporting instead:

1. Open the repository's **Security** tab.
2. Choose **Report a vulnerability**.

Please include:

- what the problem is and where it is (page, module or file)
- the steps to reproduce it
- which version (or commit) you tested
- what an attacker could achieve

You should receive an acknowledgement within 14 days. Fixes are made on a best-effort basis (see
[DISCLAIMER.md](DISCLAIMER.md)). Once a fix is available, the problem is described in the
[CHANGELOG](CHANGELOG.md), with credit to the reporter if they wish.

## Supported versions

Only the latest release receives security fixes. Keep your installation up to date (see
[DEPLOYMENT.md, section 6](DEPLOYMENT.md#6-upgrading-to-a-new-version)) and keep the server's operating
system and .NET runtime patched.

## Scope

In scope:

- the MintQMS application code in this repository
- its default configuration
- the deployment instructions in this repository

Out of scope:

- vulnerabilities in third-party components, which should go to their maintainers (please tell us if
  MintQMS is affected)
- problems that need an already-compromised server or administrator account
- findings from automated scanners without a demonstrated impact
