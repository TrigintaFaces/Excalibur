# Architecture Boundaries

**Status**: Enforced via NetArchTest + CI
**Last Updated**: 2025-11-13
**Requirements**: R1.9, R17.8, R23.1, R0.14

---

## Overview

This document defines the **architectural boundaries** for the Dispatch / Excalibur framework. These boundaries preserve the **framework's modular design**, prevent **circular dependencies**, and enable the **pay-for-play provider model**.

All boundaries are **automatically enforced** through:

- **NetArchTest** architecture tests (`tests/ArchitectureTests/Phase8_3_BoundaryTests.cs`)
- **PowerShell validation script** (`eng/validate-architecture-boundaries.ps1`)
- **CI gates** (fail build on violations)

---

## Core Architectural Principles

### 1. **Separation of Concerns**

- **Dispatch**: Pipeline, middleware, transports, routing (framework core)
- **Excalibur**: CQRS, sagas, projections, event store (domain patterns)

### 2. **Dependency Flow**

- **Dispatch** owns abstractions -> Excalibur consumes them
- **Dispatch** must **never** reference Excalibur (prevents coupling)
- **Excalibur** references **only** Dispatch abstractions (loose coupling)

### 3. **Pay-for-Play Providers**

- **Core libraries** are **cloud-agnostic** (no Azure/AWS/Google SDKs)
- **Provider packages** are **isolated** (Azure packages don't reference AWS)
- **Consumers** choose only the providers they need

---

## Boundary Rules

### R1.9: Dispatch MUST NOT Reference Excalibur

**Rule**: `Dispatch.*` projects must **never** reference `Excalibur.*` projects.

**Rationale**:

- Dispatch is the foundational messaging framework
- Excalibur builds on Dispatch (not vice versa)
- Violating this boundary creates circular dependencies

**Enforcement**:

```csharp
// NetArchTest (tests/ArchitectureTests/Phase8_3_BoundaryTests.cs)
Types.InCurrentDomain()
    .That().ResideInNamespace("Dispatch")
    .ShouldNot().HaveDependencyOn("Excalibur")
    .GetResult();
```

**Examples**:

WRONG:

```xml
<!-- Excalibur.Dispatch.csproj -->
<ItemGroup>
  <ProjectReference Include="..\..\..\Excalibur\Excalibur.Application\Excalibur.Application.csproj" />
</ItemGroup>
```

CORRECT:

```xml
<!-- Excalibur.Dispatch.csproj - No Excalibur references -->
<ItemGroup>
  <ProjectReference Include="..\Excalibur.Dispatch.Abstractions\Excalibur.Dispatch.Abstractions.csproj" />
</ItemGroup>
```

**Remediation**:
If Dispatch needs functionality from Excalibur:

1. **Extract abstraction** -> Define interface in `Excalibur.Dispatch.Abstractions`
2. **Implement in Excalibur** -> Concrete implementation in `Excalibur.*`
3. **Wire in hosting** -> DI registration in `Excalibur.Hosting`

---

### R17.8: Excalibur MAY Reference Excalibur.Dispatch.Abstractions Only

**Rule**: `Excalibur.*` projects may reference `Dispatch.*.Abstractions`, but **must not** reference concrete implementations (`Excalibur.Dispatch`, `Excalibur.Dispatch.Patterns`, etc.).

**Rationale**:

- Enables **testability** (mock abstractions, not implementations)
- Supports **provider substitution** (swap implementations without Excalibur changes)
- Prevents **tight coupling** to internal Dispatch details

**Allowed References**:

- `Excalibur.Dispatch.Abstractions`
- `Excalibur.Dispatch.Transport.Abstractions`
- `Excalibur.Dispatch.Patterns.Abstractions`
- `Excalibur.Dispatch.Hosting.Serverless.Abstractions`
- `Excalibur.Dispatch.Caching` (for `Excalibur.Caching` only - caching infrastructure)

**Forbidden References**:

- `Excalibur.Dispatch`
- `Excalibur.Dispatch.Patterns`
- `Excalibur.Dispatch.Hosting.Web`

**Note on Caching**: `Excalibur.Caching` may reference `Excalibur.Dispatch.Caching` to use generic caching infrastructure (`ICacheInvalidationService`). Projection-specific caching (`IProjectionCacheInvalidator`, `IProjectionTagResolver`) lives in `Excalibur.Caching.Projections`.

**Note on Compliance/Audit**: All 10 `Excalibur.Compliance.*` and `Excalibur.AuditLogging.*` packages remain in Dispatch as they handle **cross-cutting middleware** (HOW compliance flows through the pipeline). Only `Excalibur.Compliance.SqlServer` lives in Excalibur as it handles **stateful persistence** (WHAT gets stored). The key distinction is stateless providers (Dispatch) vs stateful domain storage (Excalibur).

| Dispatch (Stateless Middleware) | Excalibur (Stateful Persistence) |
|--------------------------------|----------------------------------|
| `Excalibur.Compliance` | `Excalibur.Compliance.SqlServer` |
| `Excalibur.Compliance.Abstractions` | |
| `Excalibur.Compliance.{Aws,Azure,Vault}` | |
| `Excalibur.AuditLogging` | |
| `Excalibur.AuditLogging.{Datadog,Sentinel,Splunk,SqlServer}` | |

**Note on Hosting**: All 12 hosting packages stay where they are - the architecture is correct. Dispatch hosting packages handle **message routing from triggers** (serverless adapters, HTTP endpoints). Excalibur hosting packages handle **full infrastructure setup** (CQRS/ES stack, jobs, web infrastructure).

| Dispatch Hosting (6 packages) | Excalibur Hosting (6 packages) |
|-------------------------------|--------------------------------|
| `Excalibur.Dispatch.Hosting.AspNetCore` | `Excalibur.Hosting` |
| `Excalibur.Dispatch.Hosting.AzureFunctions` | `Excalibur.Hosting.Web` |
| `Excalibur.Dispatch.Hosting.AwsLambda` | `Excalibur.Hosting.AzureFunctions` |
| `Excalibur.Dispatch.Hosting.GoogleCloudFunctions` | `Excalibur.Hosting.AwsLambda` |
| `Excalibur.Dispatch.Hosting.Serverless.Abstractions` | `Excalibur.Hosting.GoogleCloudFunctions` |
| `Excalibur.Dispatch.Patterns.Hosting.Json` | `Excalibur.Hosting.Jobs` |

**Important Lesson**: The rule "Dispatch does NOT depend on Excalibur" has **NO exceptions**. An attempt to consolidate `Excalibur.Dispatch.Hosting.Serverless.Abstractions` into Excalibur was reverted because it would have required Dispatch cloud adapters to reference Excalibur - a boundary violation. The `SkipDependencyValidation` flag is for controlled migrations only, not permanent bypasses.

**Enforcement**:

```csharp
// NetArchTest enforcement
Types.InCurrentDomain()
    .That().ResideInNamespace("Excalibur")
    .ShouldNot().HaveDependencyOnAny(new[] {
        "Excalibur.Dispatch",
        "Excalibur.Dispatch.Patterns"
    })
    .GetResult();
```

**Examples**:

WRONG:

```csharp
// Excalibur.Application/CommandHandler.cs
using Excalibur.Dispatch.Messaging; // References concrete implementation
using Excalibur.Dispatch.Patterns.CQRS.Commands; // Not an abstraction

public class CreateOrderHandler : ICommandHandler<CreateOrderCommand>
{
    private readonly Dispatcher _dispatcher; // Concrete type
    ...
}
```

CORRECT:

```csharp
// Excalibur.Application/CommandHandler.cs
using Excalibur.Dispatch; // Abstractions only
using Excalibur.Dispatch.Patterns.Abstractions.CQRS; // Pattern abstractions

public class CreateOrderHandler : ICommandHandler<CreateOrderCommand>
{
    private readonly IDispatcher _dispatcher; // Interface
    ...
}
```

**Remediation**:
If Excalibur references `Excalibur.Dispatch` or `Excalibur.Dispatch.Patterns`:

1. **Identify the dependency** -> Find the concrete type being used
2. **Find the abstraction** -> Locate the interface in `Dispatch.*.Abstractions`
3. **Update reference** -> Change `<ProjectReference>` to abstractions package
4. **Update usings** -> Replace `using Excalibur.Dispatch.Messaging` with `using Excalibur.Dispatch`
5. **Update DI** -> Register concrete type in hosting project (not application layer)

---

### R23.1: Core Libraries MUST NOT Reference Cloud SDKs

**Rule**: Core Dispatch/Excalibur projects must be **cloud-agnostic**. Cloud provider SDKs belong **only** in provider packages.

**Rationale**:

- **Pay-for-play model** -> Consumers only pay for providers they use
- **No transitive bloat** -> Installing `Excalibur.Dispatch` doesn't pull Azure/AWS/Google SDKs
- **Provider isolation** -> Azure packages don't reference AWS (no cross-contamination)

**Forbidden in Core**:

- `Azure.*` packages
- `Microsoft.Azure.*` packages
- `AWSSDK.*` packages
- `Google.Cloud.*` packages
- `Google.Apis.*` packages

**Allowed Locations**:

- `Excalibur.Dispatch.Transport.Azure.*` -> Azure SDKs only
- `Excalibur.Dispatch.Transport.Aws.*` -> AWS SDKs only
- `Excalibur.Dispatch.Transport.Google.*` -> Google Cloud SDKs only
- `Excalibur.Dispatch.Hosting.Serverless.AwsLambda` -> AWS Lambda SDKs only
- etc.

**Enforcement**:

```powershell
# PowerShell validation (eng/validate-architecture-boundaries.ps1)
if ($projectName -match '^Dispatch\.(Core|Patterns)$') {
    $cloudSDKs = $packageRefs | Where-Object {
        $_.Name -match '^(Azure\.|AWSSDK\.|Google\.)'
    }
    if ($cloudSDKs) {
        # VIOLATION: Core project references cloud SDK
    }
}
```

**Examples**:

WRONG:

```xml
<!-- Excalibur.Dispatch.csproj -->
<ItemGroup>
  <PackageReference Include="Azure.Messaging.ServiceBus" Version="7.18.3" />
  <PackageReference Include="AWSSDK.SQS" Version="3.7.400" />
</ItemGroup>
```

CORRECT:

```xml
<!-- Excalibur.Dispatch.Transport.Azure.csproj -->
<ItemGroup>
  <PackageReference Include="Azure.Messaging.ServiceBus" Version="7.18.3" />
  <ProjectReference Include="..\Excalibur.Dispatch.Transport.Abstractions\Excalibur.Dispatch.Transport.Abstractions.csproj" />
</ItemGroup>
```

**Provider Isolation**:

```xml
<!-- Excalibur.Dispatch.Transport.Azure MUST NOT reference AWS/Google SDKs -->
<!-- Excalibur.Dispatch.Transport.Aws MUST NOT reference Azure/Google SDKs -->
<!-- Excalibur.Dispatch.Transport.Google MUST NOT reference Azure/AWS SDKs -->
```

**Remediation**:

1. **Identify cloud SDK reference** in core project
2. **Create provider package** (e.g., `Excalibur.Dispatch.Transport.Azure`)
3. **Move SDK reference** to provider package
4. **Implement `ITransport`** interface from `Excalibur.Dispatch.Transport.Abstractions`
5. **Remove reference** from core project

---

### R0.14: Serialization Boundaries

**Rule**: Core Dispatch libraries use **MemoryPack only** for internal binary serialization. Public boundaries use **System.Text.Json** with source generation.

**Rationale**:

- **Performance** -> MemoryPack is fastest for internal wire format
- **Interoperability** -> STJ for public APIs (HTTP, CloudEvents, external consumers)
- **Consistency** -> One serializer per boundary, no mixing

**Excalibur.Dispatch Serialization Policy**:

- MemoryPack -> Internal message envelopes, transport wire format
- System.Text.Json -> Belongs in `Excalibur.Dispatch.Hosting.Web`, `Excalibur.Dispatch.Patterns.Hosting.Json`
- MessagePack -> Opt-in package only (`Excalibur.Dispatch.Serialization.MessagePack`)
- Protobuf -> Opt-in package only (`Excalibur.Dispatch.Serialization.Protobuf`)

**Public Boundary Serialization**:

- `Excalibur.Dispatch.Hosting.Web` -> System.Text.Json (HTTP/REST APIs)
- `Excalibur.Dispatch.Hosting.Serverless.*` -> System.Text.Json (CloudEvents, function payloads)
- CloudEvents adapters -> System.Text.Json (structured mode)

**Enforcement**:

```csharp
// NetArchTest enforcement
Types.InCurrentDomain()
    .That().ResideInNamespace("Excalibur.Dispatch")
    .ShouldNot().HaveDependencyOnAny(new[] {
        "System.Text.Json",
        "MessagePack",
        "Google.Protobuf"
    })
    .GetResult();
```

**Remediation**:
If `Excalibur.Dispatch` references `System.Text.Json`:

1. **Identify usage** -> Find JSON serialization code
2. **Move to hosting layer** -> Create adapter in `Excalibur.Dispatch.Patterns.Hosting.Json`
3. **Use MemoryPack** for internal wire format
4. **Use STJ** only at HTTP/REST boundary

---

## Migration Guide

### Fixing R17.8 Violations (Excalibur -> Excalibur.Dispatch)

**Scenario**: `Excalibur.Patterns.Hosting` references `Excalibur.Dispatch.Patterns` (concrete implementation).

**Step-by-Step Fix**:

1. **Identify the violation**:

   ```bash
   $ pwsh eng/validate-architecture-boundaries.ps1 -GenerateReport
   Excalibur.Patterns.Hosting references Excalibur.Dispatch.Patterns (R17.8 violation)
   ```

2. **Find the abstraction**:
   - Look for interface in `Excalibur.Dispatch.Patterns.Abstractions`
   - Example: `ICommandDispatcher`, `IQueryHandler<TQuery, TResult>`

3. **Update project reference**:

   ```xml
   <!-- Before -->
   <ProjectReference Include="..\..\Dispatch\Excalibur.Dispatch.Patterns\Excalibur.Dispatch.Patterns.csproj" />

   <!-- After -->
   <ProjectReference Include="..\..\Dispatch\Excalibur.Dispatch.Patterns.Abstractions\Excalibur.Dispatch.Patterns.Abstractions.csproj" />
   ```

4. **Update code usings**:

   ```csharp
   // Before
   using Excalibur.Dispatch.Patterns.CQRS.Commands;

   // After
   using Excalibur.Dispatch.Patterns.Abstractions.CQRS;
   ```

5. **Update DI registration** (if hosting package):

   ```csharp
   // Excalibur.Patterns.Hosting/ServiceCollectionExtensions.cs
   public static IServiceCollection AddExcaliburPatterns(this IServiceCollection services)
   {
       // Register concrete implementation from Excalibur.Dispatch.Patterns
       services.AddDispatchPatterns(); // This brings in concrete types

       // Excalibur code only references ICommandDispatcher, IQueryHandler, etc.
       return services;
   }
   ```

6. **Verify**:

   ```bash
   $ dotnet build
   $ pwsh eng/validate-architecture-boundaries.ps1
   ARCHITECTURE BOUNDARY GATE: PASSED
   ```

---

## Automated Enforcement

### NetArchTest (Compile-Time)

**Location**: `tests/ArchitectureTests/Phase8_3_BoundaryTests.cs`

**Tests**:

- `R1_9_Dispatch_MustNotReference_Excalibur`
- `R17_8_Excalibur_MustOnlyReference_DispatchAbstractions`
- `R23_1_DispatchCore_MustNotReference_CloudSDKs`
- `R0_14_DispatchCore_MustOnlyUse_MemoryPack`

**Run Tests**:

```bash
cd tests/ArchitectureTests
dotnet test --filter "FullyQualifiedName~Phase8_3_BoundaryTests"
```

**CI Integration**: Tests run on every PR and fail build if violations detected.

---

### PowerShell Validation Script (Project-Level)

**Location**: `eng/validate-architecture-boundaries.ps1`

**Features**:

- Parses all `.csproj` files for references
- Validates project-to-project references (R1.9, R17.8)
- Validates package references (R23.1, R0.14)
- Generates CSV violation report
- Provides remediation guidance

**Usage**:

```bash
# Validate all boundaries
pwsh eng/validate-architecture-boundaries.ps1

# Generate detailed report
pwsh eng/validate-architecture-boundaries.ps1 -GenerateReport

# Fail on warnings (strict mode)
pwsh eng/validate-architecture-boundaries.ps1 -FailOnWarnings
```

**CI Integration**: Runs in GitHub Actions on every commit.

---

## Consequences of Violations

### R1.9 Violation (Dispatch -> Excalibur)

- **Impact**: Circular dependency, breaks framework modularity
- **Severity**: **Critical**
- **Build**: Fails CI gate
- **Remediation Time**: ~2-4 hours (extract abstraction)

### R17.8 Violation (Excalibur -> Excalibur.Dispatch)

- **Impact**: Tight coupling, breaks testability, prevents provider substitution
- **Severity**: **High**
- **Build**: Fails CI gate
- **Remediation Time**: ~1-2 hours (switch to abstractions)

### R23.1 Violation (Core -> Cloud SDK)

- **Impact**: Transitive bloat, violates pay-for-play model
- **Severity**: **High**
- **Build**: Fails CI gate
- **Remediation Time**: ~3-6 hours (extract provider package)

### R0.14 Violation (Excalibur.Dispatch -> STJ/MessagePack)

- **Impact**: Performance degradation, serialization boundary confusion
- **Severity**: **Critical**
- **Build**: Fails CI gate
- **Remediation Time**: ~2-3 hours (move to hosting layer)

---

## FAQ

### Q: Can Excalibur.Hosting reference Excalibur.Dispatch?

**A**: **Yes**, hosting packages are **composition roots** and wire abstractions to implementations. This is the correct pattern.

Example:

```csharp
// Excalibur.Hosting/ServiceCollectionExtensions.cs
public static IServiceCollection AddDispatchCore(this IServiceCollection services)
{
    // Hosting package may reference both abstractions and core
    services.AddSingleton<IDispatcher, Dispatcher>(); // Excalibur.Dispatch type
    return services;
}
```

### Q: What if I need Excalibur.Dispatch functionality in Excalibur.Application?

**A**: Use **dependency injection** with abstractions:

1. Define interface in `Excalibur.Dispatch.Abstractions`
2. Implement in `Excalibur.Dispatch`
3. Reference interface in `Excalibur.Application` (not concrete type)
4. Register concrete implementation in `Excalibur.Hosting`

### Q: Can I add a new cloud provider SDK to Excalibur.Dispatch?

**A**: **No**. Create a provider package:

1. Create `Excalibur.Dispatch.Transport.{Provider}` project
2. Add cloud SDK package reference there
3. Implement `ITransport` from `Excalibur.Dispatch.Transport.Abstractions`
4. Register in consumer's DI (pay-for-play)

### Q: Why use MemoryPack instead of System.Text.Json in Excalibur.Dispatch?

**A**: Performance and alignment:

- **MemoryPack**: Binary, zero-allocation, fastest for internal wire format
- **STJ**: Text-based, best for HTTP/REST/CloudEvents (external boundaries)
- **Separation**: Internal vs external serialization boundaries

---

## See Also

- [Architecture Boundary Enforcement](../../management/architecture/ADR-041-Architecture-Boundary-Enforcement.md)
- [Phase 8.3 Remediation Report](../../management/reports/2025-11-13_phase8-3-architecture-boundaries_v1.0.0.md)
- [Architecture Tests README](../../tests/ArchitectureTests/README.md)
- [Dispatch Requirements Volume 01](../../management/specs/Dispatch.Requirements.01-Architecture-And-Messaging.md)
- [Pay-for-Play Provider Model](./provider-model.md)

---

**Last Validated**: 2026-01-09
**Violations**: 0 (100% compliant per W1 Capability Ownership)
