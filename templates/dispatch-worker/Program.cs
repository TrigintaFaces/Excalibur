using Company.DispatchWorker.Workers;

var builder = Host.CreateDefaultBuilder(args);

builder.ConfigureServices((hostContext, services) =>
{
    services.AddDispatch(dispatch =>
    {
        dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
#if (UseKafka)
        dispatch.UseKafka(kafka =>
        {
            kafka.BootstrapServers(hostContext.Configuration["Kafka:BootstrapServers"] ?? "localhost:9092");
        });
#elif (UseRabbitMQ)
        dispatch.UseRabbitMQ(rmq =>
        {
            rmq.ConnectionString(hostContext.Configuration["RabbitMQ:ConnectionString"] ?? "amqp://guest:guest@localhost:5672/");
        });
#elif (UseAzureServiceBus)
        dispatch.UseAzureServiceBus(asb =>
        {
            asb.ConnectionString(hostContext.Configuration["AzureServiceBus:ConnectionString"]
                ?? throw new InvalidOperationException("AzureServiceBus:ConnectionString is required."));
        });
#elif (UseAwsSqs)
        dispatch.UseAwsSqs(sqs =>
        {
            sqs.UseRegion(hostContext.Configuration["AWS:Region"] ?? "us-east-1");
        });
#elif (UseGooglePubSub)
        dispatch.UseGooglePubSub(pubsub =>
        {
            pubsub.ProjectId(hostContext.Configuration["GooglePubSub:ProjectId"] ?? "my-project");
        });
#endif
    });

    // OpenTelemetry: one call registers all Dispatch meters + activity sources
    services.AddOpenTelemetry()
        .AddDispatchInstrumentation();

    services.AddHostedService<OrderProcessingWorker>();
});

var host = builder.Build();
await host.RunAsync().ConfigureAwait(false);
