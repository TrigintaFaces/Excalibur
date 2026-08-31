# Excalibur.Compliance

Compliance implementations for the Excalibur framework. Provides AES-256-GCM encryption, key management, key rotation, and FIPS 140-2 validation for SOC2/GDPR requirements.

## Installation

```bash
dotnet add package Excalibur.Compliance
```

## Quick Start

```csharp
// Encryption at rest with an in-memory key manager (development default).
services.AddComplianceEncryption(compliance => compliance
    .WithEncryption()
    .WithInMemoryKeyManagement());
```

## Documentation

See the [main documentation](https://github.com/TrigintaFaces/Excalibur) for detailed guides and API reference.

## License

This package is part of the Excalibur framework. See [LICENSE](https://github.com/TrigintaFaces/Excalibur/blob/main/LICENSE) for license details.
