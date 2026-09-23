# Excalibur.Dispatch Templates

`dotnet new` project templates for Excalibur.Dispatch — message dispatching, event sourcing, the
outbox pattern and domain-driven design building blocks for .NET.

## Install

```bash
dotnet new install Excalibur.Dispatch.Templates
```

## Templates

| Short name | Creates |
| --- | --- |
| `dispatch-api` | Web API wired for dispatching commands and queries |
| `dispatch-minimal-api` | The same, on minimal APIs |
| `dispatch-worker` | Worker service that consumes messages from a transport |
| `dispatch-serverless` | Serverless host (functions-style entry points) |
| `excalibur-cqrs` | CQRS with separate read and write paths |
| `excalibur-ddd` | Domain-driven design with event sourcing |
| `excalibur-outbox` | Reliable messaging via the outbox pattern |
| `excalibur-saga` | Saga / process manager coordinating a long-running workflow |

## Use

```bash
dotnet new dispatch-api -n MyCompany.Orders
cd MyCompany.Orders
dotnet run
```

Most templates take options to select a transport and a database, and `--include-tests` to generate a
test project alongside the source. List what a template accepts with:

```bash
dotnet new dispatch-api --help
```

## Uninstall

```bash
dotnet new uninstall Excalibur.Dispatch.Templates
```

## License

See the license accompanying the Excalibur.Dispatch distribution.
