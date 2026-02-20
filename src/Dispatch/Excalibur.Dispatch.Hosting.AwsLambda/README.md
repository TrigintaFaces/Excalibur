# Excalibur.Dispatch.Hosting.AwsLambda

AWS Lambda hosting integration for the Dispatch messaging framework.

## Installation

```bash
dotnet add package Excalibur.Dispatch.Hosting.AwsLambda
```

## Configuration

```csharp
public class Function
{
    private readonly IDispatcher _dispatcher;

    public Function()
    {
        var services = new ServiceCollection();
        services.AddDispatch(options =>
        {
            options.AddHandlersFromAssembly(typeof(Function).Assembly);
        });

        var provider = services.BuildServiceProvider();
        _dispatcher = provider.GetRequiredService<IDispatcher>();
    }

    public async Task Handler(SQSEvent sqsEvent, ILambdaContext context)
    {
        // Process messages
    }
}
```

## Features

- SQS event processing
- SNS event processing
- API Gateway integration
- Cold start optimization

## License

This project is multi-licensed under:
- [Excalibur License 1.0](..\..\..\licenses\LICENSE-EXCALIBUR.txt)
- [AGPL-3.0-or-later](..\..\..\licenses\LICENSE-AGPL-3.0.txt)
- [SSPL-1.0](..\..\..\licenses\LICENSE-SSPL-1.0.txt)
- [Apache-2.0](..\..\..\licenses\LICENSE-APACHE-2.0.txt)

See [LICENSE](..\..\..\LICENSE) for details.

