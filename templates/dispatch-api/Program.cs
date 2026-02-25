var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSingleton<Company.DispatchApi.Infrastructure.InMemoryOrderStore>();

builder.Services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
#if (UseKafka)
    dispatch.AddKafkaTransport("default", kafka =>
    {
        kafka.BootstrapServers(builder.Configuration["Kafka:BootstrapServers"] ?? "localhost:9092");
    });
#elif (UseRabbitMQ)
    dispatch.AddRabbitMQTransport("default", rmq =>
    {
        rmq.ConnectionString(builder.Configuration["RabbitMQ:ConnectionString"] ?? "amqp://guest:guest@localhost:5672/");
    });
#elif (UseAzureServiceBus)
    dispatch.AddAzureServiceBusTransport("default", asb =>
    {
        asb.ConnectionString(builder.Configuration["AzureServiceBus:ConnectionString"]
            ?? throw new InvalidOperationException("AzureServiceBus:ConnectionString is required."));
    });
#elif (UseAwsSqs)
    dispatch.AddAwsSqsTransport("default", sqs =>
    {
        sqs.ConfigureAwsOptions(aws => { aws.Region = builder.Configuration["AWS:Region"] ?? "us-east-1"; });
    });
#endif
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
