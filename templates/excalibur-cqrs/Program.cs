var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSingleton(typeof(Excalibur.EventSourcing.Abstractions.IProjectionStore<>),
    typeof(Company.ExcaliburCqrs.Infrastructure.InMemoryProjectionStore<>));

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

builder.Services.AddExcalibur(excalibur =>
{
    excalibur.AddEventSourcing(es =>
    {
#if (UseSqlServer)
        es.UseSqlServer(builder.Configuration.GetConnectionString("EventStore")
            ?? throw new InvalidOperationException("ConnectionStrings:EventStore is required."));
#elif (UsePostgreSql)
        es.UsePostgreSql(builder.Configuration.GetConnectionString("EventStore")
            ?? throw new InvalidOperationException("ConnectionStrings:EventStore is required."));
#elif (UseInMemoryDatabase)
        es.UseInMemory();
#endif
    });
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
