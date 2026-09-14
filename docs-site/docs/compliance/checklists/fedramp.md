# FedRAMP Certification Readiness Checklist

**Framework:** Excalibur
**Standard:** NIST 800-53 Rev 5 (FedRAMP Moderate Baseline)
**Status:** 12 of 14 controls satisfied by the framework; 2 partial (SI-7, PM-11)
**Last Updated:** 2026-09-12

---

## Overview

This checklist provides a step-by-step guide for FedRAMP certification preparation using the Excalibur framework. The framework implements 12 NIST 800-53 Rev 5 controls as secure-by-default capabilities, and contributes partially to 2 more, enabling framework consumers to inherit compliance rather than implement from scratch.

**FedRAMP Impact Level:** Moderate
**Authorization Boundary:** Excalibur framework (NuGet packages)
**Implementation Approach:** Secure-by-default framework capabilities

---

## Control Mapping Table

| Control | Title | Framework Status | Consumer Action | Evidence Location |
|---------|-------|------------------|-----------------|-------------------|
| **AC-3** | Access Enforcement | ✅ SATISFIED | Inherit `[RequirePermission]` | [Attribute-based authorization](../../advanced/security.md#requirepermission-attribute) |
| **AC-6** | Least Privilege | ✅ SATISFIED | Inherit RBAC | [Role-based authorization](../../advanced/security.md#role-based-authorization) |
| **AU-2** | Audit Events | ✅ SATISFIED | Inherit `IAuditLogger` | [Audit event types](../../security/audit-logging.md#event-types) |
| **AU-3** | Content of Audit Records | ✅ SATISFIED | Inherit audit schema | [Audit event properties](../../security/audit-logging.md#event-properties) |
| **AU-9** | Protection of Audit Information | ✅ SATISFIED | Inherit immutable logs | [Hash chain integrity](../../security/audit-logging.md#hash-chain-integrity) |
| **IA-5** | Authenticator Management | ✅ SATISFIED | Inherit Argon2id hashing | [Password hashing](../../advanced/security.md#password-hashing) |
| **SC-8** | Transmission Confidentiality | ✅ SATISFIED | Inherit TLS 1.2+ | [Transport encryption](../../advanced/security.md#transport-encryption) |
| **SC-13** | Cryptographic Protection | ✅ SATISFIED | Inherit `IEncryptionProvider` | [AES-256-GCM encryption](../../security/encryption-architecture.md#aes-256-gcm-encryption) |
| **SC-28** | Protection of Information at Rest | ✅ SATISFIED | Inherit `[PersonalData]` | [Field-level encryption](../../security/encryption-architecture.md#personaldata-attribute) |
| **SI-4** | System Monitoring | ✅ SATISFIED | Inherit OpenTelemetry | [OpenTelemetry](../../observability/index.md#opentelemetry) |
| **SI-7** | Software Integrity | ⚠️ PARTIAL | Inherit SBOM + dependency scanning; packages are UNSIGNED | [SBOM generation](../fedramp/CM-8-SBOM.md#sbom-generation) |
| **PM-11** | Mission/Business Process Definition | ⚠️ PARTIAL | Define your own mission/business processes and their security risk | N/A (business process) |
| **SA-15** | Development Process | ✅ SATISFIED | Reference CI/CD | [Development process (SA-15)](../fedramp/README.md#development-process-sa-15) |
| **CM-8** | Component Inventory | ✅ SATISFIED | Reference SBOM | [Component inventory (CM-8)](../fedramp/CM-8-SBOM.md) |

**Status:** 12 satisfied, 2 partial (SI-7 software integrity, PM-11 business process)

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
- Test coverage reports (enforced regression floor of 44% in CI)
- GitHub Actions workflow runs

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
> "AU-2 is satisfied by the framework's `IAuditLogger` interface, which provides structured audit logging for all security-relevant events. Consumers implement the `IAuditStore` interface to persist audit logs to their chosen backend."

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

**Audit Schema:**
```csharp
public sealed record AuditEvent
{
    public required string EventId { get; init; }           // Unique event identifier
    public required AuditEventType EventType { get; init; } // What occurred (enum)
    public required string Action { get; init; }            // Specific action
    public required AuditOutcome Outcome { get; init; }     // Success/Failure (enum)
    public required DateTimeOffset Timestamp { get; init; } // When it occurred
    public required string ActorId { get; init; }           // Who initiated
    public string? ResourceId { get; init; }                // What was affected
    public string? ResourceType { get; init; }              // Resource category
    public string? CorrelationId { get; init; }             // Request traceability
    public string? TenantId { get; init; }                  // Tenant discriminator (scoping is caller-supplied per query)
    public IReadOnlyDictionary<string, string>? Metadata { get; init; } // Additional context
}
```

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
- Immutable append-only audit logs
- Tamper detection via cryptographic hashing
- Separate audit storage (read-only access for application)

**Consumer Checklist:**

- [ ] Implement append-only `IAuditStore` (e.g., SQL Server with INSERT-only permissions)
- [ ] Configure audit log encryption at rest
- [ ] Restrict audit log access to security team only

**Code Example:**
```csharp
public class AppendOnlyAuditStore : IAuditStore
{
    public async Task<AuditEventId> StoreAsync(AuditEvent auditEvent, CancellationToken ct)
    {
        // INSERT-only, no UPDATE or DELETE
        await _db.ExecuteAsync(
            "INSERT INTO AuditLog (EventId, EventType, Timestamp, UserId, Outcome, CorrelationId, Metadata) " +
            "VALUES (@EventId, @EventType, @Timestamp, @UserId, @Outcome, @CorrelationId, @Metadata)",
            auditEvent
        );

        // Tamper detection: store hash of previous record
        await _db.ExecuteAsync(
            "UPDATE AuditLog SET PreviousHash = @Hash WHERE EventId = @EventId",
            new { Hash = ComputeHash(auditEvent), auditEvent.EventId }
        );
    }
}
```

- [ ] Test tamper detection (verify hash chain integrity)
- [ ] Document audit log retention policy (e.g., 90 days, 1 year, 7 years)

**Evidence:**
- [Hash chain integrity](../../security/audit-logging.md#hash-chain-integrity) - Audit protection guide
- Audit storage configuration (IAM policies, SQL permissions)
- Retention policy documentation

**SSP Statement:**
> "AU-9 is satisfied through append-only audit logs with cryptographic tamper detection. The framework stores a hash of each audit record, enabling detection of unauthorized modifications. Audit storage is configured with INSERT-only permissions."

---

### Phase 4: Identification and Authentication (IA) - Week 4

#### 4.1 IA-5: Authenticator Management

**Control Requirement:**
The organization manages information system authenticators (passwords, tokens, etc.) by enforcing minimum password complexity, storing and transmitting only cryptographically-protected passwords, and enforcing password minimum and maximum lifetime restrictions.

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
> "IA-5 is satisfied by the framework's Argon2id password hashing implementation. Passwords are stored as cryptographic hashes with per-user salts. The framework enforces configurable password complexity requirements and maximum lifetime restrictions."

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
> "SC-8 is satisfied by enforcing TLS 1.2+ for all data in transit. The framework disables insecure protocols and validates certificates. Consumers configure TLS in their hosting environment."

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
> "SC-13 is satisfied by the framework's `IEncryptionProvider` abstraction, which implements AES-256-GCM encryption using NIST FIPS 140-2 validated algorithms. Consumers configure key management via Azure Key Vault or AWS KMS."

#### 5.3 SC-28: Protection of Information at Rest

**Control Requirement:**
The information system protects the confidentiality and integrity of information at rest.

**Framework Implementation:**
- Field-level encryption via `[PersonalData]` attribute
- Transparent encryption/decryption in data access layer
- Integration with `IEncryptionProvider`

**Consumer Checklist:**

- [ ] Annotate sensitive fields with `[PersonalData]`
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
> "SC-28 is satisfied by field-level encryption using the `[PersonalData]` attribute. The framework transparently encrypts sensitive fields at rest using AES-256-GCM. Consumers annotate sensitive properties to enable automatic encryption."

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
> "SI-4 is satisfied through OpenTelemetry integration for distributed tracing and metrics. The framework provides health check endpoints and structured logging with correlation IDs. Consumers configure telemetry exporters and alerting rules."

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
> "SI-7 is satisfied through SBOM generation, package hash verification and dependency vulnerability scanning. SBOM artifacts (CycloneDX format) enable supply chain transparency and automated vulnerability scanning. The framework's packages are published without an author signature; authenticity at the registry level is provided by nuget.org's own repository signature."

:::warning Packages are published UNSIGNED — do not claim author signing
There is currently no signing certificate, so every release produces packages carrying **no author
signature**. This is a declared, committed default in the release pipeline rather than an accident
of a missing secret, and the pipeline emits a warning on every release saying so.

What this means for your SSP: **do not inherit an author-signing control from this framework.**
`dotnet nuget verify` on a package you download will not show a publisher signature. nuget.org
applies its own repository signature to everything it serves, so you retain a registry-level
authenticity guarantee — but that is nuget.org's control, not ours, and it should be attributed to
them if you cite it.

The signing path exists in the pipeline and activates when a certificate is configured. Re-check
this section before an assessment rather than assuming it still reads the same.
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
> "PM-11 is satisfied through a requirements traceability matrix that links user stories to implementation and test coverage. Architecture Decision Records document risk assessments and trade-offs for security-relevant decisions."

---

### Phase 8: System and Services Acquisition (SA) - Week 8

#### 8.1 SA-15: Development Process

**Control Requirement:**
The organization requires the developer of the information system to follow a documented development process that explicitly addresses security requirements, identifies the standards and tools used in the development process, and documents the specific tool options and configurations used.

**Framework Implementation:**
- Comprehensive CI/CD pipeline with quality gates
- Automated testing (unit, integration, functional)
- Security scanning (SAST, DAST, container, secrets)
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

- [ ] Verify all quality gates pass (GitHub Actions workflow runs)
- [ ] Document development standards in SSP

**Evidence:**
- [Development process (SA-15)](../fedramp/README.md#development-process-sa-15) - CI/CD pipeline and quality gates
- GitHub Actions workflow runs (audit trail)
- Security scan reports (SARIF, JSON)

**SSP Statement:**
> "SA-15 is satisfied through a comprehensive CI/CD pipeline with automated quality gates. Every build runs unit tests, security scans (SAST, DAST, container), dependency vulnerability checks, and SBOM generation. Coverage enforcement (≥60%) ensures test quality."

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
  "specVersion": "1.4",
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
> "CM-8 is satisfied through automated SBOM generation using the CycloneDX standard. SBOMs are generated on every CI build and retained for 90 days. The SBOM includes all framework components with package metadata, dependency graphs, and cryptographic hashes."

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
- GitHub Actions workflow runs (90-day audit trail)
- Test coverage reports (enforced regression floor of 44%)
- Security scan reports (SAST, DAST, container, secrets)

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
**Status:** 12 of 14 controls satisfied by the framework; SI-7 and PM-11 partial
