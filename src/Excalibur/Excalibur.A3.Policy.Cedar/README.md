# Excalibur.A3.Policy.Cedar

Cedar policy engine HTTP adapter for the Excalibur A3 authorization framework.

## Overview

This package implements `IAuthorizationEvaluator` by delegating policy decisions to a Cedar policy engine over HTTP. Supports both local Cedar agents and Amazon Verified Permissions (AVP).

## Quick Start

### Local Cedar Agent

```csharp
services.AddExcaliburA3(a3 =>
{
    a3.UseCedarPolicy(cedar =>
    {
        cedar.Endpoint = "http://localhost:8180";
        cedar.Mode = CedarMode.Local;
        cedar.TimeoutMs = 5000;
        cedar.FailClosed = true;
    });
});
```

### Amazon Verified Permissions

```csharp
services.AddExcaliburA3(a3 =>
{
    a3.UseCedarPolicy(cedar =>
    {
        cedar.Endpoint = "https://verifiedpermissions.us-east-1.amazonaws.com";
        cedar.Mode = CedarMode.AwsVerifiedPermissions;
        cedar.PolicyStoreId = "ps-abc123";
        cedar.FailClosed = true;
    });
});
```

## Features

- **Dual mode**: Local Cedar agent or Amazon Verified Permissions
- **Fail-closed by default**: HTTP errors, timeouts, and malformed responses result in denial
- **No AWS SDK dependency**: AVP endpoint uses raw HTTP for lightweight integration
- **IHttpClientFactory integration**: Managed HTTP client lifetime with proper pooling

## License

This project is multi-licensed under:
- [Excalibur License 1.1](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-EXCALIBUR.txt)
- [AGPL-3.0-or-later](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-AGPL-3.0.txt)
- [SSPL-1.0](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-SSPL-1.0.txt)

See [LICENSE](https://github.com/TrigintaFaces/Excalibur/blob/main/LICENSE) for details.
