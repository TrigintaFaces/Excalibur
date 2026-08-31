# Excalibur.Compliance.Abstractions

Compliance abstractions for the Excalibur framework. Contains interfaces and contracts for encryption, key management, data classification, and audit logging per SOC2/GDPR requirements.

## Installation

```bash
dotnet add package Excalibur.Compliance.Abstractions
```

## Usage

This package contains only the abstractions -- interfaces, options, and contract types. It registers
no services of its own.

Reference it from a library that must compile against the contract without taking a dependency on a
concrete provider. Applications reference [`Excalibur.Compliance`](https://github.com/TrigintaFaces/Excalibur) (or another
implementation package), which supplies the registration entry points.

```bash
dotnet add package Excalibur.Compliance
```

## Documentation

See the [main documentation](https://github.com/TrigintaFaces/Excalibur) for detailed guides and API reference.

## License

This package is part of the Excalibur framework. See [LICENSE](https://github.com/TrigintaFaces/Excalibur/blob/main/LICENSE) for license details.
