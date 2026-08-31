# Excalibur.Dispatch.Validation.FluentValidation

FluentValidation integration for the Excalibur framework. Provides IValidatorResolver implementation using FluentValidation validators.

## Installation

```bash
dotnet add package Excalibur.Dispatch.Validation.FluentValidation
```

## Quick Start

```csharp
// FluentValidation plugs into the Dispatch pipeline, not the service collection.
services.AddDispatch(dispatch => dispatch.WithFluentValidation());
```

## Documentation

See the [main documentation](https://github.com/TrigintaFaces/Excalibur) for detailed guides and API reference.

## License

This package is part of the Excalibur framework. See [LICENSE](https://github.com/TrigintaFaces/Excalibur/blob/main/LICENSE) for license details.
