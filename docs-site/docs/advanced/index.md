---
sidebar_position: 40
title: Advanced Topics
description: Advanced topics for Excalibur applications
---

# Advanced Topics

This section covers advanced scenarios, deployment patterns, security hardening, and testing strategies for production applications built with Excalibur.Dispatch.

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
- A working Dispatch application
- Completion of [Getting Started](../getting-started/index.md) and [Core Concepts](../core-concepts/index.md)

## Overview

Once you've mastered the [core concepts](../core-concepts/index.md), these advanced topics help you build production-ready, secure, and scalable systems:

| Topic | Description | Key Features |
|-------|-------------|--------------|
| [Security](security.md) | Security hardening guide | Encryption, authentication, authorization |
| [Deployment](deployment.md) | Cloud-native deployment | Kubernetes, Azure, AWS patterns |
| [Testing](testing.md) | Testing strategies | Unit, integration, conformance testing |
| [Source Generators](source-generators.md) | AOT-compatible code generation | Handler registry, JSON serialization, result factory |
| [Native AOT](native-aot.md) | Native AOT compilation | Zero-reflection dispatch, trimming, AOT publish |

---

## Security Hardening

The [Security Guide](security.md) covers:

- **Encryption**: AES-256-GCM field-level encryption, key management
- **Authentication**: JWT, OAuth 2.0, mTLS integration
- **Authorization**: Interface-based access control (`IRequireAuthorization`)
- **Audit Logging**: Tamper-evident hash chains, compliance logging
- **Secret Management**: Azure Key Vault, AWS KMS, HashiCorp Vault

---

## Deployment Patterns

The [Deployment Guide](deployment.md) covers:

### Kubernetes
- Health check endpoints (`/health`, `/ready`, `/live`)
- ConfigMap and Secret management
- Horizontal Pod Autoscaling (HPA)
- Service mesh integration (Istio, Linkerd)

### Azure
- Azure App Service deployment
- Azure Functions integration
- Azure Service Bus transport
- Application Insights observability

### AWS
- AWS Lambda deployment
- Amazon SQS/SNS transport
- AWS Secrets Manager integration
- CloudWatch metrics and logging

---

## Testing Strategies

The [Testing Guide](testing.md) covers:

- **Unit Testing**: xUnit, Shouldly, FakeItEasy patterns
- **Integration Testing**: TestContainers, real infrastructure
- **Conformance Testing**: 130+ compliance test kits
- **Shared Test Doubles**: `TestMessageContext`, `TestDispatcher`

### Test Double Reference

| Test Double | Location | Purpose |
|-------------|----------|---------|
| `TestMessageContext` | `Tests.Shared` | IMessageContext for unit tests |
| `TestRoutingResult` | `Tests.Shared` | IRoutingResult stub |
| `TestServiceProvider` | `Tests.Shared` | Minimal IServiceProvider |

---

## Quick Links

### By Scenario

| I want to... | Read... |
|--------------|---------|
| Encrypt sensitive fields | [Security Guide - Encryption](security.md#encryption) |
| Deploy to Kubernetes | [Deployment Guide - Kubernetes](deployment.md#kubernetes) |
| Write unit tests | [Testing Guide](testing.md) |
| Integrate with Azure | [Deployment Guide - Azure](deployment.md#azure) |
| Add audit logging | [Security Guide - Audit Logging](security.md#audit-logging) |
| Use source generators for AOT | [Source Generators](source-generators.md) |
| Publish with Native AOT | [Native AOT Guide](native-aot.md) |

### By Compliance Requirement

| Requirement | Documentation |
|-------------|---------------|
| SOC 2 | [Compliance Checklist](../compliance/checklists/soc2.md) |
| GDPR | [Compliance Checklist](../compliance/checklists/gdpr.md) |
| HIPAA | [Compliance Checklist](../compliance/checklists/hipaa.md) |
| FedRAMP | [Compliance Checklist](../compliance/checklists/fedramp.md) |

---

## Related Documentation

- [Core Concepts](../core-concepts/index.md) - Fundamental concepts
- [Patterns](../patterns/index.md) - Common messaging patterns
- [Migration Guides](../migration/version-upgrades.md) - Version upgrade guidance
- [Compliance](../compliance/index.md) - Regulatory compliance

## See Also

- [Getting Started](../getting-started/index.md) — Quick start guide for new users
- [Core Concepts](../core-concepts/index.md) — Fundamental concepts including actions, handlers, and message context
- [Source Generators Getting Started](../source-generators/getting-started.md) — Step-by-step guide to enabling source generators
- [Performance Overview](../performance/index.md) — Performance optimization strategies and benchmarks
