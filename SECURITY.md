# Security Policy

## Supported Versions

**Pre-release packages are published on NuGet.org and are in scope.** If you are holding one, a defect in
it is a defect we want reported -- do not assume an alpha is out of scope because no stable release has
been cut.

| What you are running | In scope for a security report | Where a fix is delivered |
|---|---|---|
| **The latest pre-release** | Yes | A later pre-release |
| **An earlier pre-release** | Yes -- report it | A later pre-release. A superseded pre-release is not serviced in place |
| **A stable release, once one exists** | Yes | A patch on its major line, per the support lifecycle |
| **A build from `main`** | Yes, but say so -- `main` is not a release and the fix may already be there | Already in `main`, or the next pre-release |

The support window for a released line, and what is backported to an older one, is stated once in
[Versioning and release stages](https://docs.excalibur-dispatch.dev/docs/migration/version-upgrades) and
is not restated here.

## Reporting a Vulnerability

We take security vulnerabilities seriously. If you discover a security issue in Excalibur.Dispatch, please report it responsibly.

### How to Report

**Do NOT open a public GitHub issue for security vulnerabilities.**

Instead, please use one of the following methods:

1. **GitHub Security Advisories (Preferred):** Use the [GitHub Security Advisory](../../security/advisories/new) feature to report vulnerabilities privately. This allows us to collaborate on a fix before public disclosure.

2. **Email:** Send a detailed report to the repository maintainers via the email address listed in the repository contact information.

### What to Include

Please provide as much of the following information as possible:

- **Description** of the vulnerability and its potential impact
- **Steps to reproduce** the issue, including any proof-of-concept code
- **Affected packages** (e.g., `Excalibur.Dispatch`, `Excalibur.EventSourcing.SqlServer`)
- **Affected versions** or commits
- **Suggested fix** (if you have one)

### What to Expect

- **Acknowledgment:** We will acknowledge receipt of your report within **3 business days**.
- **Assessment:** We will assess the severity and impact within **7 business days**.
- **Resolution:** We aim to provide a fix or mitigation within **30 days** for confirmed vulnerabilities, depending on severity and complexity.
- **Disclosure:** We follow coordinated disclosure. We will work with you to agree on a disclosure timeline after the fix is available.

### Severity Classification

We use the following severity levels aligned with [CVSS v3.1](https://www.first.org/cvss/):

| Severity | CVSS Score | Response Target |
|----------|------------|-----------------|
| Critical | 9.0 - 10.0 | Fix within 7 days |
| High | 7.0 - 8.9 | Fix within 14 days |
| Medium | 4.0 - 6.9 | Fix within 30 days |
| Low | 0.1 - 3.9 | Fix in next release |

## Who Responds, and How a Fix Reaches You

### Owners

| Role | Held by |
|---|---|
| **Triage and severity assessment** | The repository maintainers, via the private advisory thread |
| **Fix and release** | The maintainer who cuts the release, through the `nuget-production` GitHub environment -- the only path to NuGet.org ([RELEASE.md](RELEASE.md)) |
| **Disclosure timing** | Agreed with you, the reporter, before publication |

**There is no separate security team and no commercial support tier.** Saying so is more useful to you
than naming a rota that does not exist: it tells you the response you get is best-effort by volunteers,
on the targets above.

### How a patched release is produced

1. The fix is developed **in the private advisory fork**, not in a public branch, so the defect is not
   disclosed by its own commit before a fix exists
2. It lands on `main` with a test that fails without it
3. A release is cut through the normal automated pipeline -- **a security fix does not bypass the gates
   it would otherwise pass**, because a rushed fix that breaks something else is not a fix
4. The version is published, and **the advisory is published at the same time, not before**

### How you learn about it

| Channel | What it carries |
|---|---|
| **[GitHub Security Advisories](https://github.com/TrigintaFaces/Excalibur/security/advisories)** | The advisory itself: affected versions, the fixed version, severity, and any workaround |
| **GitHub Releases** | The release carrying the fix, linked to the advisory |
| **[What's New](https://docs.excalibur-dispatch.dev/docs/whats-new)** | The change, in the same place as every other change you need to act on |

**An advisory always names a fixed version, never a commit.** A commit is not something you can install.
If no fixed version exists yet, the advisory says so and gives you the workaround instead.

### Publishing credentials, and what a compromise would actually look like

**There is no long-lived publishing key to steal.** Packages are published through NuGet.org **Trusted
Publishing**: the credential is minted by an OIDC exchange at the start of each release run, lives about
an hour, and nuget.org validates the token against a policy naming **this repository, this workflow
file, and the `nuget-production` environment**. One token buys exactly one key. If the exchange fails,
the run fails and **nothing is published** -- the failure is safe by construction, because the GitHub
release was created as a draft first.

The one secret involved, the nuget.org profile name, is held as a secret only to keep the account name
out of a mirrored file. **It is not a credential.**

**What this means for you:** the usual supply-chain worry -- a leaked API key used to push a malicious
version from somewhere else -- is not available against this project. An attacker would have to
compromise the repository or the workflow itself, which is a different and noisier thing.

**Packages are repository-signed by NuGet.org and are not author-signed**
([RELEASE.md](RELEASE.md#package-signing)). An author signature is therefore **not** available to you as
a provenance check, and we will not imply otherwise.

If we ever believe the publishing path has been compromised we will: revoke the Trusted Publishing
policy; compare every published version against the pipeline runs that should have produced it;
**unlist anything we cannot match to a run, and say so in an advisory naming the exact versions**; and
tell you what to check on your side. We will publish that advisory **even if the audit finds nothing**,
because *"we checked and found nothing"* is the part you cannot determine for yourself.

### Exercises

**We do not run scheduled incident exercises, and we are not going to claim otherwise.** What we do
instead is cheaper and verifiable: the credential-compromise steps above are written down before they
are needed, the release pipeline that would produce the fix runs on every release, and the advisory
route is the same GitHub mechanism used for ordinary reports. **An untested runbook is a liability; a
runbook whose every step is already exercised by normal operation is not.**

---

## Security Practices

### Dependencies

- NuGet dependencies are audited for known vulnerabilities via `dotnet list package --vulnerable` in CI.
- Dependency updates are tracked and applied regularly.
- **Accepted risks are recorded where you can read them, and the default is to accept none.** An
  advisory can be allowlisted only by naming it in `eng/governance/cve-allowlist.yaml`, a file the
  security workflow reads and which is part of the public source tree. **If that file is absent --
  which it is today -- every detected advisory blocks the build**, and if it exists but cannot be read
  the workflow fails closed rather than proceeding with an empty allowlist.

### Static Analysis

- Security SAST scanning runs on every CI build.
- Results are published as SARIF artifacts for review.

### Code Quality

- Banned API scanning prevents use of known-insecure patterns (e.g., `Newtonsoft.Json`, blocking async).
- SQL injection prevention uses `[GeneratedRegex]` whitelist validation and bracket-escape defense-in-depth.
- PII protection uses `ITelemetrySanitizer` with SHA-256 hashing for data subject identifiers in telemetry.
- Serialization policy enforcement prevents unauthorized serializer usage in core packages.

## Scope

This security policy covers the Excalibur.Dispatch framework packages published from this repository:

- `Excalibur.Dispatch` and `Excalibur.Dispatch.Abstractions`
- `Excalibur.Domain`, `Excalibur.Data.*`, `Excalibur.EventSourcing.*`
- All transport, middleware, and infrastructure packages
- CI/CD scripts and workflow definitions

Third-party dependencies are covered by their own security policies.
