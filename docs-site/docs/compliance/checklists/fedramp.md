# FedRAMP Certification Readiness Checklist

**Framework:** Excalibur
**Standard:** NIST 800-53 Rev 5 (FedRAMP Moderate Baseline)
**Status:** 4 of 14 controls satisfied by the framework; 10 partial — the framework supplies a mechanism and you supply the rest
**Last Updated:** 2026-09-12

---

:::caution Verify every SSP statement against your own deployment before you adopt it

The **SSP Statement** blocks on this page are written to be copied into your own System Security Plan,
so treat each one as a draft about *your* system rather than as a finding about it. **A review of these
statements is in progress and is not complete.**

A statement that has not yet been reviewed may describe a capability that is **opt-in and not active
unless you register it**, that is configured differently in your deployment, or that the framework does
not provide. Several controls documented here are inactive until explicitly enabled.

Before pasting any statement into a document an assessor will read, confirm it against the
configuration you actually run.

:::

## Overview

This checklist provides a step-by-step guide for FedRAMP certification preparation using the Excalibur framework. The framework fully implements 4 NIST 800-53 Rev 5 controls and contributes a mechanism to 10 more. For those 10, a mechanism is not the control: each row below says what the framework supplies and what you must still do, and your SSP must describe both.

**FedRAMP Impact Level:** Moderate
**Authorization Boundary:** Excalibur framework (NuGet packages)
**Implementation Approach:** Secure-by-default framework capabilities

---

## Control Mapping Table

| Control | Title | Framework Status | Consumer Action | Evidence Location |
|---------|-------|------------------|-----------------|-------------------|
| **AC-3** | Access Enforcement | ✅ SATISFIED | Inherit `[RequirePermission]` | [Attribute-based authorization](../../advanced/security.md#requirepermission-attribute) |
| **AC-6** | Least Privilege | ✅ SATISFIED | Inherit RBAC | [Role-based authorization](../../advanced/security.md#role-based-authorization) |
| **AU-2** | Audit Events | ⚠️ PARTIAL | Inherit the `IAuditLogger` API and event taxonomy. Nothing is audited automatically: you choose the events, call `IAuditLogger` at each point, and review the list periodically | [Audit event types](../../security/audit-logging.md#event-types) |
| **AU-3** | Content of Audit Records | ✅ SATISFIED | Inherit audit schema | [Audit event properties](../../security/audit-logging.md#event-properties) |
| **AU-9** | Protection of Audit Information | ⚠️ PARTIAL | Inherit tamper-EVIDENCE: a hash chain that detects modification after the fact. Immutability is NOT provided — store audit records where the application cannot update or delete them, and restrict access yourself | [Hash chain integrity](../../security/audit-logging.md#hash-chain-integrity) |
| **IA-5** | Authenticator Management | ⚠️ PARTIAL | Inherit Argon2id hashing of stored passwords. Issuance, distribution, revocation on compromise and MFA are yours | [Password hashing](../../advanced/security.md#password-hashing) |
| **SC-8** | Transmission Confidentiality | ⚠️ PARTIAL | Inherit TLS-required message transports (`TransportSecurityOptions.RequireTls` defaults to `true`). Your HTTP host's TLS is yours to configure | [Transport encryption](../../advanced/security.md#transport-encryption) |
| **SC-13** | Cryptographic Protection | ✅ SATISFIED | Inherit `IEncryptionProvider` | [AES-256-GCM encryption](../../security/encryption-architecture.md#aes-256-gcm-encryption) |
| **SC-28** | Protection of Information at Rest | ⚠️ PARTIAL | Inherit field encryption for `[PersonalData]` — ONLY in the event, inbox, outbox and projection stores once encryption is registered. Elsewhere the attribute encrypts nothing | [Field-level encryption](../../security/encryption-architecture.md#personaldata-attribute) |
| **SI-4** | System Monitoring | ⚠️ PARTIAL | Inherit OpenTelemetry traces and metrics. The framework detects no attacks; detection and alerting are yours | [OpenTelemetry](../../observability/index.md#opentelemetry) |
| **SI-7** | Software Integrity | ⚠️ PARTIAL | Inherit SBOM + dependency scanning; packages are NOT author-signed — they carry only NuGet.org's repository signature, which does not attest publisher provenance | [SBOM generation](../fedramp/CM-8-SBOM.md#sbom-generation) |
| **PM-11** | Mission/Business Process Definition | ⚠️ PARTIAL | Define your own mission/business processes and their security risk | N/A (business process) |
| **SA-15** | Development Process | ⚠️ PARTIAL | The framework's own CI/CD is supplier evidence only. SA-15 is about YOUR development process | [Development process (SA-15)](../fedramp/README.md#development-process-sa-15) |
| **CM-8** | Component Inventory | ⚠️ PARTIAL | The framework's SBOM covers its own packages only — one entry in your inventory, not the inventory | [Component inventory (CM-8)](../fedramp/CM-8-SBOM.md) |

**Status:** 4 satisfied (AC-3, AC-6, AU-3, SC-13), 10 partial (AU-2, AU-9, IA-5, SC-8, SC-28, SI-4, SI-7, PM-11, SA-15, CM-8)

---

## Implementation Checklist

### Phase 1: Prerequisites (Week 1)

#### 1.1 Understand Authorization Boundary

- [ ] Review framework scope: NuGet packages distributed to consumers
- [ ] Identify what is IN scope: Framework code, CI/CD, documentation
- [ ] Identify what is OUT of scope: Consumer applications, consumer infrastructure
- [ ] Document authorization boundary in System Security Plan (SSP)

**Reference:** [FedRAMP overview — authorization boundary](../fedramp/README.md#overview)

#### 1.2 Review Control Inheritance Model

- [ ] Read control mapping table (see above)
- [ ] Understand which controls are INHERITED vs IMPLEMENTED by consumers
- [ ] Document inheritance statements in SSP

**Example Inheritance Statement:**
> "The application inherits SC-13 (Cryptographic Protection) from the Excalibur framework, which implements AES-256-GCM encryption via the `IEncryptionProvider` abstraction. Framework compliance evidence includes NIST FIPS 140-2 validated algorithms and continuous vulnerability scanning."

**Reference:** [FedRAMP overview — references](../fedramp/README.md#references)

#### 1.3 Gather Evidence Package

- [ ] Clone repository: `git clone https://github.com/YourOrg/Excalibur.Dispatch.git`
- [ ] Navigate to compliance docs: `cd docs/compliance/fedramp/`
- [ ] Review control documentation: `cat CM-8-SBOM.md`
- [ ] Download SBOM artifacts from GitHub Actions (90-day retention)

**Command:**
```bash
# Download SBOM artifacts

:::warning Not legal advice

This page describes technical features that can **support** your compliance work. It is not legal
advice, and it does not establish that any system is compliant with any law, regulation or standard.
You remain responsible for your own compliance assessment, independent testing and validation, and
review by qualified legal and compliance professionals. See the [Compliance Disclaimer](../../legal/compliance-disclaimer.md).
:::
gh run download <run-id> -n cyclonedx-sbom

# Download security scan reports
# No DAST or container-scan artifacts are produced by this pipeline.
```

**Reference:** [FedRAMP authorization inheritance](../fedramp/README.md#fedramp-authorization-inheritance)

---

### Phase 2: Access Control (AC) - Week 2

#### 2.1 AC-3: Access Enforcement

**Control Requirement:**
The system enforces approved authorizations for logical access to information and system resources.

**Framework Implementation:**
- Declarative authorization via `[RequirePermission]` attribute
- Permission-based access control (PBAC)
- Centralized authorization policy enforcement

**Consumer Checklist:**

- [ ] Install `Excalibur.Domain` NuGet package
- [ ] Apply `[RequirePermission]` to protected operations

**Code Example:**
```csharp
using Excalibur.A3.Authorization;

[RequirePermission("users.delete")]
public class DeleteUserCommand : IDispatchAction
{
    public Guid UserId { get; set; }
}

// Authorization middleware enforces permission check
// Unauthorized requests are rejected before handler execution
```

- [ ] Define permission catalog in `appsettings.json`
- [ ] Configure role-to-permission mappings
- [ ] Test authorization enforcement (unit + integration tests)

**Evidence:**
- [Attribute-based authorization](../../advanced/security.md#attribute-based-authorization) - Authorization guide
- Test coverage reports from **your own** CI (the framework enforces a 44% regression floor in its own)
- Workflow runs from **your own** CI

**SSP Statement:**
> "AC-3 is satisfied by the framework's `[RequirePermission]` attribute, which enforces permission-based access control at the API layer. All protected operations are annotated with required permissions, and unauthorized requests are rejected before execution."

#### 2.2 AC-6: Least Privilege

**Control Requirement:**
The organization employs the principle of least privilege, allowing only authorized accesses for users (or processes acting on behalf of users) which are necessary to accomplish assigned tasks.

**Framework Implementation:**
- Role-based access control (RBAC)
- Granular permission definitions
- Default-deny policy

**Consumer Checklist:**

- [ ] Define roles with minimal necessary permissions
- [ ] Assign users to roles (not direct permissions)
- [ ] Review permission catalog for over-privileged roles

**Code Example:**
```csharp
// Define minimal role permissions
{
  "Roles": {
    "Viewer": ["users.read", "orders.read"],
    "Editor": ["users.read", "users.update", "orders.read", "orders.update"],
    "Admin": ["users.*", "orders.*"]  // Full control (use sparingly)
  }
}
```

- [ ] Implement periodic access reviews
- [ ] Document role justifications in SSP

**Evidence:**
- [Role-based authorization](../../advanced/security.md#role-based-authorization) - RBAC guide
- Permission catalog (configuration files)
- Access review procedures

**SSP Statement:**
> "AC-6 is satisfied through role-based access control. Users are assigned roles with the minimum permissions required for their job function. The framework enforces a default-deny policy, requiring explicit permission grants."

---

### Phase 3: Audit and Accountability (AU) - Week 3

#### 3.1 AU-2: Audit Events

**Control Requirement:**
The organization determines that the information system is capable of auditing specific security-relevant events.

**Framework Implementation:**
- `IAuditLogger` interface for structured audit logging
- Configurable audit event types
- Comprehensive event catalog

**Consumer Checklist:**

- [ ] Install `Excalibur.Domain` NuGet package
- [ ] Inject `IAuditLogger` into services

**Code Example:**
```csharp
using Excalibur.Compliance;

public class UserService
{
    private readonly IAuditLogger _auditLogger;

    public UserService(IAuditLogger auditLogger)
    {
        _auditLogger = auditLogger;
    }

    public async Task DeleteUserAsync(Guid userId, CancellationToken ct)
    {
        // Perform deletion
        await _repository.DeleteAsync(userId, ct);

        // Audit the action
        await _auditLogger.LogAsync(new AuditEvent
        {
            EventId = Guid.NewGuid().ToString(),
            EventType = AuditEventType.DataModification,
            Action = "User.Delete",
            ActorId = _currentUser.Id,
            Outcome = AuditOutcome.Success,
            Timestamp = DateTimeOffset.UtcNow,
            ResourceId = userId.ToString(),
            ResourceType = "User"
        }, ct);
    }
}
```

- [ ] Configure audit event types in `appsettings.json`
- [ ] Implement `IAuditStore` persistence layer (SQL Server, Elasticsearch, etc.)
- [ ] Verify audit logs are generated for all security-relevant events

**Evidence:**
- [Audit logging](../../security/audit-logging.md) - Audit logging guide
- Audit log samples (anonymized)
- Test coverage for audit logging

**SSP Statement:**
> "AU-2 is implemented by [system name]. We audit the following event types: [list], selected because [justification] and reviewed every [period]. Each is recorded by calling the Excalibur `IAuditLogger` API at the point the event occurs; the framework records no event on its own."

#### 3.2 AU-3: Content of Audit Records

**Control Requirement:**
The information system generates audit records containing information that establishes what type of event occurred, when the event occurred, where the event occurred, the source of the event, the outcome of the event, and the identity of any individuals or subjects associated with the event.

**Framework Implementation:**
- Comprehensive audit log schema
- Structured fields: EventType, Action, Timestamp, ActorId, Outcome, CorrelationId, Metadata
- Correlation ID traceability

**Consumer Checklist:**

- [ ] Review audit schema documentation
- [ ] Ensure all required fields are populated

**Audit Schema.** Audit records are `Excalibur.Compliance.Abstractions.Audit.AuditEvent`. The fields
below are the shipped surface; this page describes them rather than restating the declaration, so it
cannot drift away from the type you actually receive.

| AU-3 element | fields on `AuditEvent` |
|---|---|
| what type of event occurred | `EventType`, `Action` |
| when the event occurred | `Timestamp` |
| where the event occurred | `ApplicationName`, `SessionId` |
| the source of the event | `IpAddress`, `UserAgent` |
| the outcome of the event | `Outcome` |
| identity of individuals or subjects | `ActorId`, `ActorType` |
| affected resource | `ResourceId`, `ResourceType`, `ResourceClassification` |
| traceability and context | `CorrelationId`, `TenantId`, `Reason`, `Metadata`, `EventId` |
| tamper evidence | `EventHash`, `PreviousEventHash` |

`EventHash` and `PreviousEventHash` chain each record to its predecessor, which is what lets an
assessor detect that a record was altered or removed after the fact. Verify the chain over the period
under assessment rather than treating the presence of the fields as evidence on its own.

- [ ] Verify audit records include all required fields
- [ ] Test audit record completeness (integration tests)

**Evidence:**
- [Audit event properties](../../security/audit-logging.md#event-properties) - Audit schema documentation
- Sample audit records (anonymized)
- Schema validation tests

**SSP Statement:**
> "AU-3 is satisfied by the framework's audit schema, which includes all required fields: EventType (what), Action (what action), Timestamp (when), ActorId (who), Outcome (result), CorrelationId (traceability), and Metadata (additional context)."

#### 3.3 AU-9: Protection of Audit Information

**Control Requirement:**
The information system protects audit information and audit tools from unauthorized access, modification, and deletion.

**Framework Implementation:**
- **Tamper-evidence, not immutability.** The SQL Server and Postgres audit stores chain each record's hash to the previous one (`EnableHashChain`, on by default), and `AuditChainVerifier.VerifyAsync` checks the chain. That detects a modification after it has happened. It does not prevent one.
- The shipped audit stores only ever `INSERT` audit records. They `DELETE` them only through the retention purges (`PurgeExpiredAsync`, `PurgeTenantAsync`).
- Protecting audit information from unauthorized modification and deletion — the core of AU-9 — is **your storage and access configuration**, not something a library can supply.

:::warning A hash chain is not immutability
AU-9 asks that audit information be protected from unauthorized access, modification **and** deletion. A hash chain protects against none of the three: it tells you afterwards that one of them happened. Do not describe this control as inherited.
:::

**Consumer Checklist:**

- [ ] Grant the application's database identity `INSERT` and `SELECT` only on the audit table — no `UPDATE`, no `DELETE`
- [ ] Run retention purges under a **separate** identity that alone holds `DELETE`, so the application itself cannot remove audit records
- [ ] Or write audit records to a WORM (write-once) sink with its own retention lock
- [ ] Configure audit log encryption at rest
- [ ] Restrict audit log access to security team only

**Code Example** (SQL Server, default `audit.AuditEvents` table):
```sql
-- The application identity can write and read audit records, and nothing else.
GRANT INSERT, SELECT ON audit.AuditEvents TO [app_identity];
DENY  UPDATE, DELETE ON audit.AuditEvents TO [app_identity];

-- Retention purges (PurgeExpiredAsync / PurgeTenantAsync) delete rows, so run them
-- under a separate identity that alone holds DELETE.
GRANT SELECT, DELETE ON audit.AuditEvents TO [audit_retention_identity];
```

- [ ] Test tamper detection (verify hash chain integrity)
- [ ] Document audit log retention policy (e.g., 90 days, 1 year, 7 years)

**Evidence:**
- [Hash chain integrity](../../security/audit-logging.md#hash-chain-integrity) - Audit protection guide
- Audit storage configuration (IAM policies, SQL permissions)
- Retention policy documentation

**SSP Statement:**
> "The framework contributes tamper-evidence to AU-9: each audit record carries a hash chained to the previous record, and the chain can be verified to detect unauthorized modification after the fact. Protection from modification and deletion is provided by [describe YOUR control — e.g. INSERT-only database permissions for the application identity, a separate identity for retention purges, or a WORM store]."

---

### Phase 4: Identification and Authentication (IA) - Week 4

#### 4.1 IA-5: Authenticator Management

**Control Requirement:**
The organization manages information system authenticators (passwords, tokens, etc.) by screening candidate passwords against a list of commonly-used, expected or compromised values, transmitting passwords only over cryptographically-protected channels, storing them with an approved salted key-derivation function, requiring a new password on account recovery, and applying organization-defined composition and complexity rules.

> **Revision note.** This summarises IA-5(1) as it stands in **NIST SP 800-53 Rev. 5**. Rev. 4's
> IA-5(1)(d) additionally required *"password minimum and maximum lifetime restrictions"*; **Rev. 5
> removed that item.** If your authorization is assessed against a Rev. 4 baseline, that requirement
> still applies to you and this framework does not implement it — see the note under the SSP statement
> below.
>
> **Confirm which revision your own authorization is assessed against.** FedRAMP is mid-transition, and
> which baseline binds you is a fact about your authorization rather than about this framework, so we
> do not assert it here. Check the current FedRAMP baselines directly rather than relying on a revision
> named in this document.

**Framework Implementation:**
- Argon2id password hashing (OWASP recommended)
- Key rotation support
- Configurable password policies

**Consumer Checklist:**

- [ ] Configure password complexity requirements

**Code Example:**
```csharp
using Excalibur.Security;

// Argon2id password hashing (OWASP defaults)
services.AddPasswordHasher(options =>
{
    options.MemorySize = 65536;  // 64 MB
    options.Iterations = 4;
    options.Parallelism = 4;
});
```

> **Note:** Password complexity policies (minimum length, character requirements, expiration) are application-level concerns. The framework provides the hashing primitive; consumers must implement policy enforcement.

- [ ] Implement password expiration (90-day maximum per FedRAMP Moderate)
- [ ] Store hashed passwords only (NEVER plaintext)
- [ ] Enforce password history (prevent reuse of last 5 passwords)

**Evidence:**
- [Password hashing](../../advanced/security.md#password-hashing) - Password management guide
- Password policy configuration
- Unit tests for password hashing

**SSP Statement:**
> "IA-5 is partly addressed by the framework's Argon2id password hashing implementation, which is registered in this deployment; authenticator issuance, distribution, revocation on compromise and multi-factor authentication are provided by [your identity provider / process]. Passwords are stored as cryptographic hashes with per-user salts. The framework enforces configurable complexity requirements and screens candidate values against a prohibited-value list. Routine password expiration is deliberately not enforced, in line with NIST SP 800-63B Rev. 4, which directs that passwords be rotated only on suspected compromise. NIST SP 800-53 Rev. 5 carries no password-lifetime requirement in IA-5(1); confirm which revision your own authorization is assessed against."

:::caution Password hashing is opt-in — register it before adopting this statement

The Argon2id hasher is **not** composed by default: call `AddPasswordHasher()` to register
`IPasswordHasher`. A deployment that never calls it has no framework password hashing at all, and the
statement above does not describe it.

**On the absence of password expiry — this is a position, not a gap.** NIST SP 800-63B Rev. 4 rejects
routine expiration and requires rotation only on suspected compromise, and Rev. 5 of SP 800-53 removed
the minimum/maximum lifetime item that Rev. 4's IA-5(1)(d) carried. Building an expiry policy here
would implement a control the current standards direct against. If your authorizing official still
requires a rotation period, it belongs in your identity provider, which owns the account lifecycle —
this framework hashes and verifies credentials and does not manage accounts.

:::

---

### Phase 5: System and Communications Protection (SC) - Week 5

#### 5.1 SC-8: Transmission Confidentiality

**Control Requirement:**
The information system protects the confidentiality of transmitted information.

**Framework Implementation:**
- TLS 1.2+ enforcement
- Encryption pipeline for message transport
- Certificate validation

**Consumer Checklist:**

- [ ] Configure TLS 1.2+ for all HTTP endpoints
- [ ] Disable insecure protocols (SSL 3.0, TLS 1.0, TLS 1.1)

**Code Example:**
```csharp
// ASP.NET Core Startup.cs
services.Configure<HttpsConnectionAdapterOptions>(options =>
{
    options.SslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13;
});

// Enforce HTTPS redirection
app.UseHttpsRedirection();
app.UseHsts();
```

- [ ] Verify TLS configuration with `nmap` or `testssl.sh`
- [ ] Document certificate management procedures

**Evidence:**
- [Transport encryption](../../advanced/security.md#transport-encryption) - TLS configuration guide
- TLS scan results (testssl.sh output)
- Certificate management procedures

**SSP Statement:**
> "SC-8 is implemented by [system name]. Our HTTP endpoints require TLS [1.2/1.3], configured in [host/load balancer]. Message-transport connections made through the Excalibur transports require TLS by default (`TransportSecurityOptions.RequireTls = true`) and fail to connect without it."

#### 5.2 SC-13: Cryptographic Protection

**Control Requirement:**
The information system implements cryptographic mechanisms to prevent unauthorized disclosure of information and/or detect changes to information during transmission unless otherwise protected by alternative physical measures.

**Framework Implementation:**
- `IEncryptionProvider` abstraction for pluggable encryption
- AES-256-GCM for data at rest
- NIST FIPS 140-2 validated algorithms

**Consumer Checklist:**

- [ ] Install `Excalibur.Domain` NuGet package
- [ ] Implement `IEncryptionProvider` (or use default AES-256-GCM)

**Code Example:**
```csharp
using Excalibur.Compliance;

services.AddEncryption(encryption => encryption
    .UseKeyManagement<AesGcmEncryptionProvider>("aes-gcm-primary")
    .ConfigureOptions(options => options.DefaultPurpose = "field-encryption"));

// Encrypt sensitive fields
public class User
{
    public Guid Id { get; set; }

    [PersonalData]  // [PersonalData] is encrypted ONLY on a record that also carries [DataSubjectId],
    // and only on a path you wire (crypto-shredding / the encrypting event-store decorator).
    // A record with no [DataSubjectId] member is left in cleartext.
    public string Email { get; set; }

    [PersonalData]
    public string PhoneNumber { get; set; }
}
```

- [ ] Configure key management (Azure Key Vault, AWS KMS, etc.)
- [ ] Verify encryption with integration tests
- [ ] Document key rotation procedures

**Evidence:**
- [Encryption architecture](../../security/encryption-architecture.md) - Encryption guide
- Key management procedures
- FIPS 140-2 compliance statement

**SSP Statement:**
> "SC-13 is satisfied by the framework's `IEncryptionProvider` abstraction, which implements AES-256-GCM encryption using NIST-approved algorithms (whether the underlying cryptographic module is FIPS 140 validated depends on your platform and its configuration). Consumers configure key management via Azure Key Vault or AWS KMS."

#### 5.3 SC-28: Protection of Information at Rest

**Control Requirement:**
The information system protects the confidentiality and integrity of information at rest.

**Framework Implementation:**
- Field-level encryption via `[PersonalData]` attribute
- Transparent encryption/decryption in data access layer
- Integration with `IEncryptionProvider`

**Consumer Checklist:**

- [ ] Annotate sensitive fields with `[PersonalData]`
- [ ] Register encryption for every store that holds them (`AddEncryption()` plus the store's encryption registration). **`[PersonalData]` on a type persisted any other way — your own SQL tables, a document store you write to directly — encrypts nothing and reports nothing.** Protect that data with storage-level encryption
- [ ] Verify encryption at rest with database inspection

**Code Example:**
```csharp
// Domain model
public class CreditCard
{
    public Guid Id { get; set; }

    [PersonalData]
    public string CardNumber { get; set; }  // Encrypted in database

    [PersonalData]
    public string CVV { get; set; }  // Encrypted in database

    public DateTime ExpirationDate { get; set; }  // NOT encrypted
}

// Encryption happens on the paths you wire (crypto-shredding / the encrypting event-store
// decorator), and only for records carrying [DataSubjectId]. There is no generic encrypting repository.
var card = await _repository.GetAsync<CreditCard>(cardId, ct);
Console.WriteLine(card.CardNumber);  // Decrypted: "4111111111111111"

// Database inspection shows encrypted value:
// SELECT CardNumber FROM CreditCards WHERE Id = '...'
// Result: "AQIDBAUGBwgJCgsMDQ4PEA=="  (Base64-encoded ciphertext)
```

- [ ] Test decryption failures (tampered ciphertext)
- [ ] Document data classification policy (what gets encrypted)

**Evidence:**
- [Field-level encryption](../../security/encryption-architecture.md#field-level-encryption) - Data at rest encryption guide
- Data classification policy
- Encryption verification tests

**SSP Statement:**
> "SC-28 is implemented by [system name]. Data held in the Excalibur event, inbox, outbox and projection stores is field-encrypted with AES-256-GCM where properties carry `[PersonalData]` on records with a `[DataSubjectId]`, with encryption registered at startup. All other data at rest is protected by [storage encryption / your mechanism]."

:::caution Both attributes are required — `[PersonalData]` alone does not encrypt

Encryption is keyed on the data subject, so the framework encrypts a field only when its record declares
**both** `[PersonalData]` on the field **and** `[DataSubjectId]` on the identifying property. A record
annotated with `[PersonalData]` alone names no subject key and is not encrypted by this path.

**`[Sensitive]` — check the version you hold.** In every published version it classifies and masks only;
it does not encrypt. On the main branch it also selects a property for encryption at rest. Until a
release names that change, do not assert encryption on the strength of `[Sensitive]` alone.

**And the annotations alone are not sufficient: at-rest field encryption is opt-in and must be
registered.** Call `AddEventSourcingCryptoShredding()` to activate it for the event store, inbox and
outbox; projection stores are registered per type, so each one carrying personal data also needs
`AddProjectionEncryption<TProjection>()`. Annotating records without registering leaves them stored in
plaintext.

**A startup check covers only part of this, and the boundary matters.** If crypto-shredding is
configured but the store is not wired for at-rest encryption, the host refuses to start — that
half-configured state cannot boot. **It does not detect annotations alone:** a host that annotates
records and registers no encryption at all starts normally and stores them in plaintext. So a clean
startup is not evidence that this control is active; verify the registration.

:::

---

### Phase 6: System and Information Integrity (SI) - Week 6

#### 6.1 SI-4: System Monitoring

**Control Requirement:**
The organization monitors the information system to detect attacks and indicators of potential attacks, unauthorized local, network, and remote connections, and unauthorized system events.

**Framework Implementation:**
- OpenTelemetry integration for distributed tracing
- Health check endpoints
- Structured logging with correlation IDs

**Consumer Checklist:**

- [ ] Configure OpenTelemetry exporter (Application Insights, Jaeger, etc.)

**Code Example:**
```csharp
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;

services.AddOpenTelemetry()
    .WithTracing(builder => builder
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddApplicationInsightsExporter(options =>
        {
            options.ConnectionString = Configuration["ApplicationInsights:ConnectionString"];
        }))
    .WithMetrics(builder => builder
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddApplicationInsightsExporter());

// Health checks
services.AddHealthChecks()
    .AddSqlServer(connectionString)
    .AddRedis(redisConnectionString);

app.MapHealthChecks("/health");
```

- [ ] Set up alerts for suspicious activity (failed authentication, unauthorized access)
- [ ] Implement log aggregation (ELK stack, Splunk, etc.)
- [ ] Test monitoring with simulated attacks

**Evidence:**
- [Observability](../../observability/index.md#opentelemetry) - Monitoring guide
- OpenTelemetry configuration
- Alert rules and runbooks

**SSP Statement:**
> "SI-4 is implemented by [system name] using [monitoring/SIEM tool], which detects [attack indicators, unauthorized access] and alerts [who]. Excalibur contributes OpenTelemetry traces, metrics and correlation IDs as input to that monitoring; it performs no detection itself."

#### 6.2 SI-7: Software Integrity

**Control Requirement:**
The organization employs integrity verification mechanisms to detect unauthorized changes to software and information.

**Framework Implementation:**
- SHA-256 hash verification for NuGet packages
- SBOM generation (CycloneDX) for supply chain transparency
- Dependency vulnerability scanning

**Consumer Checklist:**

- [ ] Download SBOM artifacts from the release workflow
- [ ] Review SBOM for known vulnerabilities
- [ ] Verify the hash on the package you actually downloaded
- [ ] Record that the packages carry **no author signature** (see below) in your own supply-chain risk assessment

**Evidence:**
- [SBOM generation](../fedramp/CM-8-SBOM.md#sbom-generation) - SBOM generation guide
- NuGet package hashes (`.nupkg` files)
- Vulnerability scan reports

**SSP Statement:**
> "SI-7 is partly addressed through SBOM generation, package hash verification and dependency vulnerability scanning. SBOM artifacts (CycloneDX format) enable supply chain transparency and automated vulnerability scanning. The framework's packages are published without an author signature; authenticity at the registry level is provided by nuget.org's own repository signature."

:::warning Packages carry no AUTHOR signature — do not claim author signing
**Published packages carry no author signature.** That is measured from the shipped artifact:
`dotnet nuget verify` reports a repository signature issued by NuGet.org and no publisher signature.

**We are not stating why, because we cannot verify it from the artifact.** The release pipeline
contains an author-signing step, conditional on a signing certificate being configured; whether it
was configured for any particular published version is a property of the publishing environment and
not of the package. Treat the absence as the fact and the cause as unestablished.

What this means for your SSP: **do not inherit an author-signing control from this framework.**
`dotnet nuget verify` on a package you download will not show a publisher signature. nuget.org
applies its own repository signature to everything it serves, so you retain a registry-level
authenticity guarantee — but that is nuget.org's control, not ours, and it should be attributed to
them if you cite it.

Author signing is not yet established for published packages. The pipeline's signing step activates
when a certificate is configured, so this can change without the documentation changing — re-check
it against a package you have actually downloaded before an assessment.
:::

---

### Phase 7: Program Management (PM) - Week 7

#### 7.1 PM-11: Mission/Business Process Definition

**Control Requirement:**
The organization defines mission/business processes with consideration for information security and the resulting risk to organizational operations.

**Framework Implementation:**
- Requirements traceability matrix (RTM)
- User stories linked to acceptance criteria
- Risk assessment in ADRs

**Consumer Checklist:**

- [ ] Review RTM for framework requirements coverage
- [ ] Ensure consumer requirements trace to framework capabilities

**SSP Statement:**
> "PM-11 is addressed by [organization] through a requirements traceability matrix that links user stories to implementation and test coverage. Architecture Decision Records document risk assessments and trade-offs for security-relevant decisions."

---

### Phase 8: System and Services Acquisition (SA) - Week 8

#### 8.1 SA-15: Development Process

**Control Requirement:**
The organization requires the developer of the information system to follow a documented development process that explicitly addresses security requirements, identifies the standards and tools used in the development process, and documents the specific tool options and configurations used.

**Framework Implementation:**
- Comprehensive CI/CD pipeline with quality gates
- Automated testing (unit, integration, functional)
- Security scanning (SAST via CodeQL, secrets via Gitleaks, dependency vulnerabilities)
- SBOM generation on every build

**Consumer Checklist:**

- [ ] Review CI/CD pipeline configuration

**Pipeline Stages:**
```yaml
# .github/workflows/ci.yml
jobs:
  build:
    - Checkout code
    - Setup .NET 10.0
    - Restore dependencies
    - Build solution
    - Run unit tests
    - Upload coverage (enforced regression floor of 44%)

  security-sast:
    - CodeQL analysis (SAST)
    - Dependency vulnerability scan
    - Secrets scanning (Gitleaks)

  # NOTE: no DAST job exists in this pipeline.
    - API security testing

  # (no container-scan job exists)
    - Critical vulnerability blocking

  sbom-generation:
    - CycloneDX SBOM generation
    - Upload artifacts (90-day retention)

  # (no requirements-traceability job exists)
    - Requirements traceability validation
    - Coverage enforcement
```

- [ ] Verify all quality gates pass in **your own** pipeline, and keep those run records — the
      framework's repository runs are not evidence for your system
- [ ] Document development standards in SSP

**Evidence:**
- [Development process (SA-15)](../fedramp/README.md#development-process-sa-15) - CI/CD pipeline and quality gates
- Workflow runs from **your own** CI, retained as the audit trail — not the framework repository's
- Security scan reports (SARIF, JSON)

**SSP Statement:**

Write this about **your own** pipeline. SA-15 is a control over *your* development process; the
framework is a dependency of your system rather than a part of that process, so its repository's
workflow runs are not evidence for your system and must not be cited as your audit trail. Replace each
bracketed item with what your pipeline actually does, and keep only the gates you actually run:

> "SA-15 is satisfied through our CI/CD pipeline and its automated quality gates. Every build of
> [system name] runs [unit and integration tests], [static analysis tool], [secret scanning tool],
> [dependency vulnerability scanning] and [SBOM generation]; a build that fails any of these gates is
> not promoted. Workflow run records are retained for [retention period] as the audit trail, and
> coverage is enforced at [your threshold]."

**What the framework supplies, and what it does not.** The artifacts below are evidence about the
*framework's* development. They belong to your supply-chain and inventory controls — not to SA-15,
which an assessor will read as a statement about your own engineering process.

| Artifact | Produced for the framework | Where it applies to you |
|----------|---------------------------|-------------------------|
| CycloneDX SBOM of the framework's packages | Yes — by the release pipeline | Component inventory (CM-8) and supply chain (SR-3) |
| SAST (CodeQL) and secret scanning over framework source | Yes | Supply chain — evidence about a dependency |
| DAST; container image scanning | **No** — neither runs | Nothing. Do not claim either. |
| Test, scan or coverage records for **your** system | **No** | You produce these; nothing here substitutes |

:::warning Do not paste the framework's coverage figure into your SSP
The framework enforces a regression floor on **its own** test suite. That number is a threshold on
framework code and says nothing about the coverage of your system. An assessor who reads it inside your
SSP is reading a measurement nobody took against your code, and the claim is yours to defend, not ours.
:::

---

### Phase 9: Configuration Management (CM) - Week 9

#### 9.1 CM-8: Component Inventory

**Control Requirement:**
The organization develops and documents an inventory of information system components that accurately reflects the current information system.

**Framework Implementation:**
- Automated SBOM generation (CycloneDX)
- 90-day artifact retention
- Package-level granularity with dependency graph

**Consumer Checklist:**

- [ ] Download SBOM artifacts from GitHub Actions

**Command:**
```bash
# List available workflow runs
gh run list --workflow=ci.yml --limit=10

# Download SBOM artifacts from specific run
gh run download <run-id> -n cyclonedx-sbom

# Verify SBOM completeness
ls -lh bom.json bom.xml
```

- [ ] Review SBOM contents (dependencies, licenses, versions)
- [ ] Import SBOM into dependency tracking tool (e.g., OWASP Dependency-Track)

**SBOM Contents:**
```json
{
  "bomFormat": "CycloneDX",
  "specVersion": "1.7",
  "version": 1,
  "components": [
    {
      "type": "library",
      "name": "Excalibur.Domain",
      "version": "1.0.0",
      "description": "Domain building blocks for Excalibur.Dispatch",
      "licenses": [{"license": {"id": "MIT"}}],
      "hashes": [{"alg": "SHA-256", "content": "abc123..."}],
      "externalReferences": [
        {"type": "vcs", "url": "https://github.com/YourOrg/Excalibur.Dispatch"}
      ]
    }
  ],
  "dependencies": [
    {"ref": "Excalibur.Domain", "dependsOn": ["Excalibur.Dispatch.Abstractions"]}
  ]
}
```

- [ ] Reference SBOM in SSP (control CM-8 evidence)

**Evidence:**
- [Component inventory (CM-8)](../fedramp/CM-8-SBOM.md) - Comprehensive CM-8 documentation
- SBOM artifacts (CycloneDX JSON/XML)
- GitHub Security tab (dependency graph)

**SSP Statement:**
> "CM-8 is implemented by [system name] through [inventory tool], covering our code, third-party libraries, infrastructure and containers, reviewed every [period]. For the Excalibur packages we use, the supplier's CycloneDX SBOM supplies package metadata, dependency graphs and hashes."

---

## Consumer Responsibilities

**Framework Provides:**
- Compliant security capabilities (encryption, audit, access control)
- SBOM for supply chain transparency
- Secure development process evidence
- Continuous compliance monitoring

**Consumer Must Implement:**
- System Security Plan (SSP) development
- Control implementation statements (inheriting framework capabilities)
- Continuous monitoring plan
- Incident response procedures
- Infrastructure configuration (TLS, key management, audit storage)
- User training and awareness
- Periodic access reviews

---

## Compliance Verification

### Pre-Certification Testing

**Week 10: Internal Audit**

- [ ] Review all 14 control implementation statements
- [ ] Verify evidence package completeness
- [ ] Test framework capabilities in staging environment
- [ ] Document any gaps or findings

**Week 11: Penetration Testing**

- [ ] Conduct DAST testing (ZAP, Burp Suite)
- [ ] Test authentication and authorization controls
- [ ] Verify encryption in transit and at rest
- [ ] Test audit logging completeness

**Week 12: Final Review**

- [ ] Address penetration testing findings
- [ ] Update SSP with remediation evidence
- [ ] Prepare for 3PAO assessment

### Third-Party Assessment Organization (3PAO) Engagement

- [ ] Select FedRAMP-approved 3PAO
- [ ] Provide evidence package (this checklist + documentation)
- [ ] Schedule kickoff meeting
- [ ] Respond to 3PAO questions and findings
- [ ] Remediate any identified gaps

### Authorization Package Submission

- [ ] Finalize SSP with 3PAO findings
- [ ] Compile Security Assessment Report (SAR)
- [ ] Create Plan of Action & Milestones (POA&M) for any residual risks
- [ ] Submit to FedRAMP PMO or Agency AO
- [ ] Respond to PMO/AO questions
- [ ] Obtain Authority to Operate (ATO)

---

## Evidence References

### Primary Evidence

**Process Evidence:**
- [Development process (SA-15)](../fedramp/README.md#development-process-sa-15) - CI/CD pipeline and quality gates
- Workflow runs from **your own** CI, retained as the audit trail (90 days is the platform default)
- Test coverage reports (enforced regression floor of 44%)
- Security scan reports (CodeQL SAST, Gitleaks secrets, dependency scanning)

**Artifact Evidence:**
- SBOM artifacts (CycloneDX JSON/XML)
- NuGet packages (hash-verifiable; **no author signature** — see SI-7)
- Docker images (**not scanned by this pipeline** — no container scanning runs here)
- RTM reports (requirements traceability)

### Supporting Documentation

**Standards:**
- [FedRAMP Program](https://www.fedramp.gov/)
- [NIST SP 800-53 Rev 5](https://csrc.nist.gov/publications/detail/sp/800-53/rev-5/final)
- [FedRAMP Moderate Baseline](https://www.fedramp.gov/assets/resources/documents/FedRAMP_Security_Controls_Baseline.xlsx)

**Framework Documentation:**
- [FedRAMP overview](../fedramp/README.md) - FedRAMP overview
- [Component inventory (CM-8)](../fedramp/CM-8-SBOM.md) - CM-8 detailed implementation

---

## Continuous Compliance

### Automated Monitoring (Every CI Build)

- SBOM generation (component inventory)
- Dependency vulnerability scanning
- Security policy enforcement (CRITICAL vulnerabilities block)
- Requirements traceability validation
- Code coverage measurement (enforced regression floor of 44%)

### Pull Request Gates

- Coverage diff enforcement (≤1% drop on touched files)
- API compatibility checks
- Architecture boundary validation
- Transitive dependency bloat detection

### Quarterly Reviews

- Control effectiveness assessment
- Evidence package updates
- Compliance documentation refresh
- Security posture evaluation

### On-Demand

- Pre-release compliance verification
- Audit preparation support
- Incident response documentation

---

## Contact

**Questions:**
- Product Manager: Requirements clarification, control scope
- Software Architect: Technical implementation, architecture decisions
- Project Manager: Evidence packages, audit coordination

**Escalation:**
- Security incidents: follow your organisation's incident response plan. The framework records
  security-relevant events through the audit trail; it does not detect, triage, or escalate incidents.
- Compliance gaps: Create GitHub issue with `compliance` label
- Audit requests: Contact Project Manager for evidence package

---

## See Also

- [Compliance Checklists](index.md) - All compliance checklists overview
- [Security Overview](../../security/index.md) - Security architecture and threat model

---

**Last Updated:** 2026-09-12
**Status:** 4 of 14 controls satisfied by the framework; 10 partial
