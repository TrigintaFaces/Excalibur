# Excalibur.Dispatch.Compliance.Aws

AWS KMS integration for the Dispatch compliance framework. Provides IKeyManagementProvider implementation backed by AWS Key Management Service with support for multi-region keys, automatic key rotation, and FIPS 140-2 compliance.

## Installation

```bash
dotnet add package Excalibur.Dispatch.Compliance.Aws
```

## Quick Start

```csharp
// Add Excalibur.Dispatch.Compliance.Aws to your service configuration
services.AddComplianceAws();
```

## Documentation

See the [main documentation](https://github.com/TrigintaFaces/Excalibur) for detailed guides and API reference.

## License

This package is part of the Excalibur framework. See [LICENSE](..\..\..\LICENSE) for license details.
