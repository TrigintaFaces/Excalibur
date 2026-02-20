---
sidebar_position: 30
title: Security
description: Security implementation guides for Dispatch and Excalibur
---

# Security

Dispatch and Excalibur provide comprehensive security features for enterprise applications, including encryption, audit logging, and compliance support.

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
- Install the required packages:
  ```bash
  dotnet add package Excalibur.Dispatch
  dotnet add package Excalibur.Dispatch.Security  # encryption, signing, input validation
  ```
- Familiarity with [middleware](../middleware/index.md) and [pipeline concepts](../pipeline/index.md)

## Security Topics

| Topic | Description |
|-------|-------------|
| [Encryption Architecture](encryption-architecture.md) | AES-256-GCM encryption, key management, cloud KMS integration |
| [Audit Logging](audit-logging.md) | Tamper-evident audit trails with hash chain integrity |
| [Threat Model Baseline](threat-model.md) | Release-blocking threat categories and governance expectations |

## Quick Links

### Encryption
- [AES-256-GCM Provider](encryption-architecture.md#aes-256-gcm-encryption) - Technical specifications and usage
- [Key Management](encryption-architecture.md#key-management) - Key lifecycle and rotation
- [Cloud KMS Providers](encryption-architecture.md#key-management-providers) - Azure Key Vault, AWS KMS, HashiCorp Vault
- [Message Encryption](encryption-architecture.md#message-level-encryption) - Pipeline middleware
- [Store Decorators](encryption-architecture.md#encrypting-store-decorators) - Transparent persistence encryption
- [FIPS 140-2](encryption-architecture.md#fips-140-2-compliance) - Federal compliance

### Compliance
- [Compliance Overview](../compliance/index.md) - FedRAMP, GDPR, SOC 2, HIPAA
- [Quick Start Guide](../compliance/quick-start.md) - 30-minute implementation guide

## Related Documentation

- [Advanced Security](../advanced/security.md) - Detailed security guide
- [Compliance](../compliance/index.md) - Regulatory compliance checklists

## See Also

- [Encryption Providers](./encryption-providers.md) — Available encryption providers including AES-GCM, Azure Key Vault, AWS KMS, and HashiCorp Vault
- [Authorization & Audit (A3)](./authorization.md) — Activity-based authorization, token validation, grants, and audit events
- [Audit Logging](./audit-logging.md) — Hash-chained audit trails, SIEM integration, and compliance mapping
- [Threat Model Baseline](./threat-model.md) — Supply-chain, integrity, privilege, and availability threat baseline
- [Compliance Overview](../compliance/index.md) — FedRAMP, GDPR, SOC 2, and HIPAA compliance checklists and guides
