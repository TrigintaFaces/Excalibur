# Excalibur.Dispatch.Hosting.AspNetCore

ASP.NET Core hosting integration for the Dispatch messaging framework.

## Installation

```bash
dotnet add package Excalibur.Dispatch.Hosting.AspNetCore
```

## Configuration

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDispatch(options =>
{
    options.AddHandlersFromAssembly(typeof(Program).Assembly);
});

var app = builder.Build();

app.MapDispatchEndpoints();
app.Run();
```

## Features

- Automatic handler registration
- Health check endpoints
- OpenAPI/Swagger integration
- Request/response middleware
- Dependency injection integration

## License

This project is multi-licensed under:
- [Excalibur License 1.0](..\..\..\licenses\LICENSE-EXCALIBUR.txt)
- [AGPL-3.0-or-later](..\..\..\licenses\LICENSE-AGPL-3.0.txt)
- [SSPL-1.0](..\..\..\licenses\LICENSE-SSPL-1.0.txt)
- [Apache-2.0](..\..\..\licenses\LICENSE-APACHE-2.0.txt)

See [LICENSE](..\..\..\LICENSE) for details.
