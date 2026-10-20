# Excalibur.Dispatch.Hosting.AspNetCore

ASP.NET Core hosting integration for the Excalibur framework.

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

builder.Services.AddDispatchExceptionHandler();

var app = builder.Build();
app.UseExceptionHandler();

// Map one endpoint per action. The request is bound, dispatched, and its result returned.
app.DispatchPostAction<GreetRequest, GreetAction, string>("/greet");
app.Run();
```

## Features

- Automatic handler registration
- Health check endpoints
- OpenAPI/Swagger integration
- Request/response middleware
- Dependency injection integration
- Framework exception status mapping and problem details with production-safe responses

`AddDispatchExceptionHandler` uses the existing Dispatch ASP.NET Core integration package. Production
responses retain the mapped HTTP status and public per-property validation errors, while internal
messages and stack traces remain in server logs. Development responses include diagnostic details.
Customize status reference URLs through its optional configuration callback. Call `UseExceptionHandler`
to activate the registered handler; multiple handler types run in registration order.

Existing `Excalibur.Hosting.Web.AddGlobalExceptionHandler` consumers retain their original entry point,
options type and logger category, including demystified development stack traces. The lean Dispatch
handler uses the exception's standard `ToString()` diagnostic text and does not add a Demystifier
dependency. Both handlers omit stack traces and error codes outside Development.

## License

This project is multi-licensed under:
- [Excalibur License 1.1](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-EXCALIBUR.txt)
- [AGPL-3.0-or-later](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-AGPL-3.0.txt)
- [SSPL-1.0](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-SSPL-1.0.txt)

See [LICENSE](https://github.com/TrigintaFaces/Excalibur/blob/main/LICENSE) for details.
