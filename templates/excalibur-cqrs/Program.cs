#if (UseSqlServer)
using Excalibur.EventSourcing.SqlServer;
using Microsoft.Data.SqlClient;
#elif (UsePostgreSql)
using Excalibur.EventSourcing.Postgres;
using Npgsql;
#elif (UseInMemoryDatabase)
using Excalibur.EventSourcing.InMemory;
#endif
using Excalibur.EventSourcing.DependencyInjection;
using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch;
using Excalibur.Dispatch.Hosting.AspNetCore;
using Excalibur.EventSourcing;
using Company.ExcaliburCqrs.Application.Commands;
using Company.ExcaliburCqrs.Application.Queries;
using Excalibur.Dispatch.Observability.Metrics;
using Company.ExcaliburCqrs.Domain.Events;
using Company.ExcaliburCqrs.Domain.Aggregates;
using Company.ExcaliburCqrs.ReadModel;
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddDispatchExceptionHandler();
builder.Services.AddEndpointsApiExplorer();
// Match read-model durability to checkpoint durability so restart and multiple hosts are safe.
#if (UseSqlServer)
var connectionString = builder.Configuration.GetConnectionString("EventStore")
    ?? throw new InvalidOperationException("ConnectionStrings:EventStore is required.");
builder.Services.AddSqlServerSubscriptionCheckpointStore(() => new SqlConnection(connectionString));
builder.Services.AddSqlServerProjectionStore<OrderReadModel>(options =>
{
    options.ConnectionString = connectionString;
    options.TableName = "OrderReadModels";
});
#elif (UsePostgreSql)
var connectionString = builder.Configuration.GetConnectionString("EventStore")
    ?? throw new InvalidOperationException("ConnectionStrings:EventStore is required.");
builder.Services.AddPostgresSubscriptionCheckpointStore(() => new NpgsqlConnection(connectionString));
builder.Services.AddPostgresProjectionStore<OrderReadModel>(options =>
{
    options.ConnectionString = connectionString;
    options.TableName = "order_read_models";
});
#elif (UseInMemoryDatabase)
builder.Services.AddInMemoryProjectionStore<OrderReadModel>();
#endif

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
        // Keep the subscription name stable when adding projections. Each projection generation
        // owns its own checkpoint; adding one does not reset its siblings.
        es.EnableProjectionProcessing(opts =>
        {
            opts.ProjectionName = "orders-read-models";
            opts.SubscriptionScope = "orders/events/read-models";
        });
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

// The event-store and checkpoint scripts are provisioned as described in README.md.
// Generate the versioned read-model table from the provider contract, avoiding copied DDL.
#if (UseSqlServer)
await using (var connection = new SqlConnection(connectionString))
{
    await connection.OpenAsync();
    await using var command = connection.CreateCommand();
    command.CommandText = SqlServerProjectionSchema.CreateTableScript(
        Excalibur.EventSourcing.ProjectionVersionConvention.Qualify("OrderReadModels", typeof(OrderReadModel), '_'));
    await command.ExecuteNonQueryAsync();
}
#elif (UsePostgreSql)
await using (var connection = new NpgsqlConnection(connectionString))
{
    await connection.OpenAsync();
    await using var command = connection.CreateCommand();
    command.CommandText = PostgresProjectionSchema.CreateTableScript(
        Excalibur.EventSourcing.ProjectionVersionConvention.Qualify("order_read_models", typeof(OrderReadModel), '_'));
    await command.ExecuteNonQueryAsync();
}
#endif

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseExceptionHandler();
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.DispatchPostBodyAction<CreateOrderCommand, CreateOrderResult>("/api/orders");
app.MapGet("/api/orders/{id:guid}", async (Guid id, long? after, IDispatcher dispatcher, CancellationToken cancellationToken) =>
{
    var result = await dispatcher.DispatchAsync<GetOrderQuery, ProjectionRead<OrderReadModel>>(
        new GetOrderQuery(id, new SaveResult(0, after, ProjectionFreshness.NotRequested)), cancellationToken);
    if (!result.Succeeded) { return result.ToHttpResult(); }
    var read = result.ReturnValue;
    if (read.Freshness == ProjectionFreshness.BoundExpired) { return Results.Accepted(value: read); }
    return read.Projection is null ? Results.NotFound() : Results.Ok(read);
});
app.MapPost("/api/orders/{id:guid}/ship", async (Guid id, IDispatcher dispatcher, CancellationToken cancellationToken) =>
{
    var result = await dispatcher.DispatchAsync<ShipOrderCommand, SaveResult?>(new ShipOrderCommand(id), cancellationToken);
    if (!result.Succeeded) { return result.ToHttpResult(); }
    return result.ReturnValue is { } receipt ? Results.Ok(receipt) : Results.NotFound();
});

app.Run();
