using Company.DispatchWorker.Workers;

var builder = Host.CreateDefaultBuilder(args);

builder.ConfigureServices((hostContext, services) =>
{
    services.AddDispatch(dispatch =>
    {
        dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
#if (UseKafka)
        dispatch.AddKafkaTransport("default", kafka =>
        {
            kafka.BootstrapServers(hostContext.Configuration["Kafka:BootstrapServers"] ?? "localhost:9092");
        });
#elif (UseRabbitMQ)
        dispatch.AddRabbitMQTransport("default", rmq =>
        {
            rmq.ConnectionString(hostContext.Configuration["RabbitMQ:ConnectionString"] ?? "amqp://guest:guest@localhost:5672/");
        });
#elif (UseAzureServiceBus)
        dispatch.AddAzureServiceBusTransport("default", asb =>
        {
            asb.ConnectionString(hostContext.Configuration["AzureServiceBus:ConnectionString"]
                ?? throw new InvalidOperationException("AzureServiceBus:ConnectionString is required."));
        });
#elif (UseAwsSqs)
        dispatch.AddAwsSqsTransport("default", sqs =>
        {
            sqs.ConfigureAwsOptions(aws => { aws.Region = hostContext.Configuration["AWS:Region"] ?? "us-east-1"; });
        });
#endif
    });

    services.AddHostedService<OrderProcessingWorker>();
});

var host = builder.Build();
await host.RunAsync().ConfigureAwait(false);
