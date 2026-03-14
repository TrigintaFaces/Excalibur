---
sidebar_position: 4
title: PII-Safe Telemetry
description: Prevent personal data leakage into observability systems with ITelemetrySanitizer.
---

# PII-Safe Telemetry

Observability systems (tracing, metrics, logging) can inadvertently capture personal data — user IDs, email addresses, tenant identifiers — in span tags, log properties, and metric dimensions. Excalibur.Dispatch provides a sanitization layer that hashes, suppresses, or passes through telemetry values based on tag classification.

**Package:** `Excalibur.Dispatch.Observability`

## Architecture

```
ITelemetrySanitizer (interface, 2 methods)
├── HashingTelemetrySanitizer      (default, SHA-256 hashing)
└── ComplianceTelemetrySanitizer   (regex pattern detection + hashing)
```

- **`HashingTelemetrySanitizer`** — Classifies tags into three categories: **hashed** (SHA-256), **suppressed** (tag omitted entirely), or **passthrough** (unchanged). Uses a bounded cache (1024 entries) for hash performance.
- **`ComplianceTelemetrySanitizer`** — Layers regex-based PII pattern detection (emails, phone numbers, SSNs) on top of the baseline hashing sanitizer.

## Setup

### Basic (Registered Automatically)

`AddDispatchObservability()` registers `HashingTelemetrySanitizer` as the default `ITelemetrySanitizer`:

```csharp
builder.Services.AddDispatchObservability();
```

No additional configuration is needed — the default configuration hashes common PII tags and suppresses highly sensitive tags.

### Custom Tag Classification

Configure which tags are hashed vs suppressed:

```csharp
builder.Services.AddDispatchObservability();

builder.Services.Configure<TelemetrySanitizerOptions>(opt =>
{
    // Tags whose values are hashed (SHA-256) before emission
    opt.SensitiveTagNames =
    [
        "user.id",
        "user.name",
        "auth.user_id",
        "auth.subject_id",
        "auth.identity_name",
        "auth.tenant_id",
        "audit.user_id",
        "tenant.id",
        "tenant.name",
        "dispatch.messaging.tenant_id",
    ];

    // Tags suppressed entirely (not emitted)
    opt.SuppressedTagNames =
    [
        "auth.email",
        "auth.token",
    ];
});
```

### Development Override

Bypass all sanitization in development environments for debugging:

```csharp
builder.Services.Configure<TelemetrySanitizerOptions>(opt =>
{
    opt.IncludeRawPii = true; // Development only!
});
```

A startup warning is emitted if `IncludeRawPii` is `true` in non-Development environments.

### Compliance-Level Sanitization

For regulated environments, add regex-based PII detection that catches patterns even in unclassified tags:

```csharp
builder.Services.AddDispatchObservability();
builder.Services.AddComplianceTelemetrySanitizer(opt =>
{
    // Add custom patterns beyond the built-in email/phone/SSN detection
    opt.CustomPatterns.Add(new PiiPattern("medical-record", @"MRN-\d{8}"));
});
```

## ITelemetrySanitizer Interface

```csharp
namespace Excalibur.Dispatch.Abstractions.Telemetry;

public interface ITelemetrySanitizer
{
    /// <summary>
    /// Sanitizes a telemetry tag value. Returns null to suppress the tag entirely.
    /// </summary>
    string? SanitizeTag(string tagName, string? rawValue);

    /// <summary>
    /// Sanitizes a payload string (e.g., message body or log message).
    /// </summary>
    string SanitizePayload(string payload);
}
```

## How Values Are Sanitized

| Classification | Behavior | Example |
|---|---|---|
| **Sensitive** (in `SensitiveTagNames`) | Hashed to `sha256:<hex>` | `user.id: "john"` → `user.id: "sha256:a8cfcd..."` |
| **Suppressed** (in `SuppressedTagNames`) | Tag omitted entirely | `auth.email` → not emitted |
| **Passthrough** (not in either list) | Returned unchanged | `http.method: "GET"` → `http.method: "GET"` |
| **IncludeRawPii = true** | All values passed through | No sanitization applied |

## Middleware Integration

The following middleware inject `ITelemetrySanitizer` and sanitize telemetry data before emission:

| Middleware | What It Sanitizes |
|---|---|
| `TracingMiddleware` | Span tags and error descriptions |
| `LoggingMiddleware` | Log properties |
| `AuditLoggingMiddleware` | Audit trail entries |
| `MetricsLoggingMiddleware` | Metric dimension values |
| `AuthenticationMiddleware` | Identity-related span tags |
| `AuthorizationMiddleware` | Authorization context tags |
| `TenantIdentityMiddleware` | Tenant identification tags |
| `JwtAuthenticationMiddleware` | JWT claim values in spans |
| `RetryMiddleware` | Error descriptions in retry spans |
| `CircuitBreakerMiddleware` | Error descriptions in circuit breaker spans |

## SetSanitizedErrorStatus Extension

When recording exceptions on OpenTelemetry spans, use the sanitized extension to prevent PII in error messages from leaking:

```csharp
using Excalibur.Dispatch.Extensions;

// Instead of:
activity.SetStatus(ActivityStatusCode.Error, exception.Message); // PII risk!

// Use:
activity.SetSanitizedErrorStatus(exception, sanitizer);
```

This method:
1. Returns only the exception type name for well-known system exceptions (no PII risk)
2. Sanitizes the message using `ITelemetrySanitizer.SanitizePayload()` for all other exceptions
3. Records a sanitized exception event on the span
4. Sets the span status to `Error`

## SensitiveDataPostConfigureOptions

The `SensitiveDataPostConfigureOptions` class automatically flows the `IncludeRawPii` toggle into all `IncludeSensitiveData` flags across the framework:

| Options Class | Property |
|---|---|
| `TracingOptions` | `IncludeSensitiveData` |
| `AuditLoggingOptions` | `IncludeSensitiveData` |
| `ObservabilityOptions` | `IncludeSensitiveData` |

When `TelemetrySanitizerOptions.IncludeRawPii = true`, all three are set to `true` automatically. This ensures a single toggle controls PII inclusion across the entire pipeline.

## TelemetrySanitizerOptions Reference

| Property | Type | Default | Description |
|---|---|---|---|
| `IncludeRawPii` | `bool` | `false` | Bypass all sanitization (development only) |
| `SensitiveTagNames` | `IList<string>` | 10 common PII tags | Tags hashed with SHA-256 |
| `SuppressedTagNames` | `IList<string>` | `auth.email`, `auth.token` | Tags suppressed entirely |

## See Also

- [Telemetry Configuration](./telemetry-configuration.md) — Configure OpenTelemetry metrics and tracing
- [Metrics Reference](./metrics-reference.md) — Complete metrics catalog
- [Production Observability](./production-observability.md) — Monitoring guide
