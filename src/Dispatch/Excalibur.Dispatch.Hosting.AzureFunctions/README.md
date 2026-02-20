# Excalibur.Dispatch.Hosting.AzureFunctions

Azure Functions hosting integration for the Dispatch messaging framework.

## Installation

```bash
dotnet add package Excalibur.Dispatch.Hosting.AzureFunctions
```

## Configuration

```csharp
var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services =>
    {
        services.AddDispatch(options =>
        {
            options.AddHandlersFromAssembly(typeof(Program).Assembly);
        });
    })
    .Build();

host.Run();
```

## Features

- Azure Functions isolated worker support
- Queue and Service Bus triggers
- HTTP trigger integration
- Durable Functions support

## License

This project is multi-licensed under:
- [Excalibur License 1.0](..\..\..\licenses\LICENSE-EXCALIBUR.txt)
- [AGPL-3.0-or-later](..\..\..\licenses\LICENSE-AGPL-3.0.txt)
- [SSPL-1.0](..\..\..\licenses\LICENSE-SSPL-1.0.txt)
- [Apache-2.0](..\..\..\licenses\LICENSE-APACHE-2.0.txt)

See [LICENSE](..\..\..\LICENSE) for details.
