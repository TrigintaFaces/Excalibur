# Support Policy

This document describes the support policy for Excalibur.

---

## Getting Help

### Support Channels (Priority Order)

| Channel | Use For | Link |
|---------|---------|------|
| **GitHub Discussions** | Questions, ideas, general help | [Discussions](https://github.com/TrigintaFaces/Excalibur/discussions) |
| **GitHub Issues** | Bug reports, feature requests | [Issues](https://github.com/TrigintaFaces/Excalibur/issues) |
| **GitHub Security Advisories** | Security vulnerabilities (private) | [Security](https://github.com/TrigintaFaces/Excalibur/security) |

> **Note:** This is an open-source project maintained by volunteers. Response times are best-effort and not guaranteed.

---

## Response Time Expectations

| Issue Type | Target Response | Target Resolution |
|------------|-----------------|-------------------|
| Security (Critical/High) | 72 hours | ASAP |
| Bug (Production Impact) | 1 week | Based on severity |
| Bug (Minor) | 2 weeks | Next minor release |
| Feature Request | 1 month | Based on priority |
| Question | Best effort | Community-driven |

**Disclaimer:** These are targets, not guarantees. Support is community-driven.

---

## Supported Versions

| Framework | Status | Supported Until |
|-----------|--------|-----------------|
| .NET 10.0 | Current | .NET 11 release + 6 months |
| .NET 9.0 STS | Supported | May 2026 |
| .NET 8.0 LTS | Supported | November 2026 |
| .NET 7.0 | EOL | Not supported |
| .NET 6.0 | EOL | Not supported |

We follow the [.NET Support Policy](https://dotnet.microsoft.com/platform/support/policy).

---

## Provider Support Tiers

| Tier | Definition | Support Level |
|------|------------|---------------|
| **Tier 1** | Core providers, fully tested in CI, documented | Full support |
| **Tier 2** | Community-contributed, periodic testing | Best effort |
| **Tier 3** | Deprecated, removal planned | Bug fixes only |
| **In-Memory** | Testing only | Not for production |

### Tier 1 (Full Support)

Fully tested, documented, and maintained:

- **SQL Server**: EventSourcing, Data, LeaderElection, Saga
- **RabbitMQ**: Transport
- **Azure Service Bus**: Transport
- **Azure Cosmos DB**: EventSourcing, Data

### Tier 2 (Community)

Community-contributed, best-effort support:

- **Kafka**: Transport
- **AWS SQS**: Transport
- **Google Pub/Sub**: Transport
- **PostgreSQL**: Data
- **MongoDB**: Data
- **Redis**: Data, LeaderElection
- **DynamoDB**: EventSourcing, Data
- **Firestore**: EventSourcing, Data

### Tier 3 (Deprecated)

Scheduled for removal, bug fixes only:

- None currently

---

## End-of-Life Policy

When a framework version reaches EOL:

| Timeline | Action |
|----------|--------|
| 6 months before EOL | Announce deprecation |
| 3 months before EOL | Add `[Obsolete]` warnings |
| EOL date | Remove from CI matrix |
| Next major release | Remove support |

### Deprecation Announcements

- GitHub Discussions announcement
- CHANGELOG.md entry
- docs-site banner (if applicable)

---

## Breaking Changes

We follow [Semantic Versioning](https://semver.org/):

| Version Type | Breaking Changes | Examples |
|--------------|------------------|----------|
| Patch (x.y.Z) | No | Bug fixes only |
| Minor (x.Y.0) | No (deprecations allowed) | New features, `[Obsolete]` warnings |
| Major (X.0.0) | Yes | API changes, removals |

### Breaking Change Process

1. **Deprecate** in minor release with `[Obsolete]` attribute and migration guidance
2. **Document** migration path in CHANGELOG.md
3. **Minimum 6-month** deprecation period
4. **Remove** in next major release

---

## Security Policy

### Reporting Vulnerabilities

1. **DO NOT** create public issues for security vulnerabilities
2. Use [GitHub Security Advisories](https://github.com/TrigintaFaces/Excalibur/security/advisories/new) (private disclosure)
3. Include:
   - Affected versions
   - Severity assessment (CVSS if known)
   - Steps to reproduce
   - Potential impact

### Response Process

| Severity | Target Acknowledgment | Target Fix |
|----------|----------------------|------------|
| Critical | 24 hours | ASAP (days) |
| High | 72 hours | 1-2 weeks |
| Medium | 1 week | Next minor release |
| Low | 2 weeks | Next minor release |

### Disclosure

- CVE assigned for confirmed vulnerabilities
- Security advisory published after fix is available
- Credit given to reporters (unless anonymity requested)

---

## Commercial Support

This is an open-source project. **No commercial support is available.**

For enterprise needs, consider:
- Contributing fixes and features
- Sponsoring development
- Building internal expertise

---

## Related Documentation

- [Release Process](RELEASE.md) - How releases are made
- [Contributing Guide](CONTRIBUTING.md) - How to contribute

---

*Last Updated: Sprint 342 (W5.T5.2 Support & Compatibility Policy)*
