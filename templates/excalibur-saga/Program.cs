using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Observability.Metrics;
using Excalibur.Saga.Orchestration;
using Company.ExcaliburSaga.Messages;
using Company.ExcaliburSaga.Sagas;
#if (UseSqlServer)
using Excalibur.Saga.SqlServer.DependencyInjection;
#elif (UsePostgreSql)
using Excalibur.Saga.Postgres.DependencyInjection;
#endif
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
#if (UseKafka)
    dispatch.UseKafka(kafka =>
    {
        kafka.BootstrapServers(builder.Configuration["Kafka:BootstrapServers"] ?? "localhost:9092");
    });
#elif (UseRabbitMQ)
    dispatch.UseRabbitMQ(rmq =>
    {
        rmq.ConnectionString(builder.Configuration["RabbitMQ:ConnectionString"] ?? "amqp://guest:guest@localhost:5672/");
    });
#elif (UseAzureServiceBus)
    dispatch.UseAzureServiceBus(asb =>
    {
        asb.ConnectionString(builder.Configuration["AzureServiceBus:ConnectionString"]
            ?? throw new InvalidOperationException("AzureServiceBus:ConnectionString is required."));
    });
#elif (UseAwsSqs)
    dispatch.UseAwsSqs(sqs =>
    {
        sqs.UseRegion(builder.Configuration["AWS:Region"] ?? "us-east-1");
    });
#elif (UseGooglePubSub)
    dispatch.UseGooglePubSub(pubsub =>
    {
        pubsub.ProjectId(builder.Configuration["GooglePubSub:ProjectId"] ?? "my-project");
    });
#endif
});

builder.Services.AddExcalibur(excalibur => excalibur
    .AddSagas(saga =>
    {
#if (UseSqlServer)
        saga.UseSqlServer(sql => sql.ConnectionStringName("SagaStore"));
#elif (UsePostgreSql)
        saga.UsePostgres(pg => pg.ConnectionStringName("SagaStore"));
#elif (UseInMemoryDatabase)
        saga.WithInMemoryStore();
#endif
        saga.WithCoordination()
            // In-memory timeout storage is a single-process demonstration and is lost on restart.
            // A durable timeout store registered by the selected provider takes precedence.
            .WithTimeouts()
            .WithInstrumentation();
    }));

builder.Services.AddSaga<OrderSaga, OrderSagaState>();
SagaRegistry.Register<OrderSaga, OrderSagaState>(saga => saga
    .StartsWith<StartOrderProcessing>()
    .Handles<PaymentCollected>()
    .Handles<InventoryReserved>()
    .Handles<OrderShipped>()
    .Handles<PaymentTimedOut>());

// OpenTelemetry: one call registers all Dispatch meters + activity sources
builder.Services.AddOpenTelemetry()
    .AddDispatchInstrumentation();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
