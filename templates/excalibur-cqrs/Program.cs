#if (UseSqlServer)
using Excalibur.EventSourcing.SqlServer;
#elif (UsePostgreSql)
using Excalibur.EventSourcing.Postgres;
#elif (UseInMemoryDatabase)
using Excalibur.EventSourcing.InMemory;
#endif
using Excalibur.EventSourcing.DependencyInjection;
using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Observability.Metrics;
using Company.ExcaliburCqrs.Domain.Events;
using Company.ExcaliburCqrs.Domain.Aggregates;
using Company.ExcaliburCqrs.ReadModel;
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSingleton(typeof(Excalibur.EventSourcing.IProjectionStore<>),
    typeof(Company.ExcaliburCqrs.Infrastructure.InMemoryProjectionStore<>));

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

builder.Services.AddExcalibur(excalibur =>
{
    excalibur.AddEventSourcing(es =>
    {
        es.RegisterEventTypesFromAssembly(typeof(OrderCreated).Assembly);
        es.AddRepository<Order, Guid>(id => new Order(id));
        // One writer owns the read model: the projection host applies committed events in position
        // order. A save returns a session token; pass it to a read that must observe its own write.
        es.AddProjection<OrderReadModel>(projection => projection
            .Async()
            .WhenHandledBy<OrderCreated, OrderProjection>()
            .WhenHandledBy<OrderShipped, OrderProjection>());
        // Runs the projection host. Without it nothing applies the events to the read model.
        //
        // SubscriptionScope is required and has no default. It names this event source and this
        // projection destination together, and every process that cooperates on the same read model
        // must use the same value — that is how they agree to share one checkpoint. Two processes
        // using different values would each advance the same checkpoint past work the other never
        // did, so there is no value the framework can safely invent for you.
        es.EnableProjectionProcessing(opts => opts.SubscriptionScope = "orders-read-model");
#if (UseSqlServer)
        es.UseSqlServer(sql => sql.ConnectionStringName("EventStore"));
#elif (UsePostgreSql)
        es.UsePostgres(pg => pg.ConnectionStringName("EventStore"));
#elif (UseInMemoryDatabase)
        es.UseInMemory();
#endif
    });
});

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
