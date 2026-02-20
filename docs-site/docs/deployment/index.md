---
sidebar_position: 20
title: Deployment
description: Deploy Dispatch and Excalibur applications to various environments
---

# Deployment

Deploy Dispatch and Excalibur applications to cloud, container, and on-premises environments.

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
- A working Dispatch application ready for deployment
- Familiarity with [configuration](../configuration/index.md) and [observability](../observability/index.md)

## Deployment Options

| Platform | Guide | Description |
|----------|-------|-------------|
| [Docker](docker.md) | Container deployment | Docker and Docker Compose |
| [Kubernetes](kubernetes.md) | Container orchestration | K8s deployment patterns |
| [AWS Lambda](aws-lambda.md) | Serverless | AWS serverless deployment |
| [Azure Functions](azure-functions.md) | Serverless | Azure serverless deployment |
| [Google Cloud Functions](google-cloud-functions.md) | Serverless | GCP serverless deployment |
| [On-Premises](on-premises.md) | Self-hosted | Traditional server deployment |

## Security

- [Security Best Practices](security-best-practices.md) - Security considerations for all deployments

## Related Documentation

- [Getting Started](/docs/getting-started) - Quick start guide
- [Advanced Topics](../advanced/index.md) - Advanced deployment patterns

## See Also

- [ASP.NET Core](../deployment/aspnet-core.md) — Host Excalibur applications in ASP.NET Core with web API and background processing
- [Worker Services](../deployment/worker-services.md) — Deploy dedicated background workers for event processing
- [Kubernetes](../deployment/kubernetes.md) — Container orchestration patterns and deployment manifests
- [Docker](../deployment/docker.md) — Container deployment with Docker and Docker Compose

