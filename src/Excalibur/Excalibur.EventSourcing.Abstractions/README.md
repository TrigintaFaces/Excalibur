# Excalibur.EventSourcing.Abstractions

Event sourcing abstractions for the Excalibur framework

## Installation

```bash
dotnet add package Excalibur.EventSourcing.Abstractions
```

## Usage

This package contains only the abstractions -- interfaces, options, and contract types. It registers
no services of its own.

Reference it from a library that must compile against the contract without taking a dependency on a
concrete provider. Applications reference [`Excalibur.EventSourcing`](https://github.com/TrigintaFaces/Excalibur) (or another
implementation package), which supplies the registration entry points.

```bash
dotnet add package Excalibur.EventSourcing
```

## Documentation

See the [main documentation](https://github.com/TrigintaFaces/Excalibur) for detailed guides and API reference.

## License

This package is part of the Excalibur framework. See [LICENSE](https://github.com/TrigintaFaces/Excalibur/blob/main/LICENSE) for license details.
