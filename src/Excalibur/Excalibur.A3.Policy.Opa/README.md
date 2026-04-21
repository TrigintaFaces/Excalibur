# Excalibur.A3.Policy.Opa

Open Policy Agent (OPA) HTTP adapter for the Excalibur A3 authorization framework.

## Overview

This package implements `IAuthorizationEvaluator` by delegating policy decisions to an external OPA server via HTTP/REST. Authorization requests are mapped to OPA input format and evaluated against your Rego policies.

## Quick Start

```csharp
services.AddExcaliburA3(a3 =>
{
    a3.UseOpaPolicy(opa =>
    {
        opa.Endpoint = "http://localhost:8181";
        opa.PolicyPath = "v1/data/authz/allow";
        opa.TimeoutMs = 5000;
        opa.FailClosed = true; // deny on OPA errors
    });
});
```

## Features

- **Fail-closed by default**: HTTP errors, timeouts, and malformed responses result in denial
- **Configurable fail-open**: Set `FailClosed = false` for permissive error handling
- **IHttpClientFactory integration**: Managed HTTP client lifetime with proper pooling
- **STJ source-gen ready**: Zero-reflection JSON serialization for AOT compatibility
- **Minimal dependencies**: Only `Microsoft.Extensions.Http` and `System.Text.Json`
