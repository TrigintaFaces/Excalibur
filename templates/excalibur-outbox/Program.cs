using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Middleware.Outbox;
using Excalibur.Dispatch.Observability.Metrics;
using Excalibur.Dispatch.Hosting.AspNetCore;
using Excalibur.Dispatch.Serialization;
using Company.ExcaliburOutbox.Messages;
#if (UseRabbitMQ)
using Excalibur.Dispatch.Transport.RabbitMQ;
#endif
#if (UseSqlServer)
using Excalibur.Outbox.SqlServer;
#elif (UsePostgreSql)
using Excalibur.Outbox.Postgres;
#endif

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddDispatchExceptionHandler();

builder.Services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
    dispatch.WithSerialization(serialization => serialization.RegisterSystemTextJson().UseSystemTextJson());

    // REQUIRED. AddDispatch() seats no behavioural middleware, so registering an outbox STORE is
    // not enough on its own -- the staging stage has to be named. Without this line a handler that
    // writes to the outbox throws at dispatch, because there is no stage to stage it.
    dispatch.UseOutbox();
#if (UseKafka)
    dispatch.UseKafka("outbox-events", kafka =>
    {
        kafka.BootstrapServers(builder.Configuration["Kafka:BootstrapServers"] ?? "localhost:9092");
        kafka.MapTopic<OrderPlacedEvent>(builder.Configuration["Kafka:Topic"] ?? "orders");
        if (builder.Environment.IsDevelopment())
        {
            kafka.RequireTls(false);
        }
        else
        {
            kafka.UseSecurityProtocol(Confluent.Kafka.SecurityProtocol.Ssl);
        }
    });
#elif (UseRabbitMQ)
    dispatch.UseRabbitMQ("outbox-events", rmq =>
    {
        rmq.ConnectionString(builder.Configuration["RabbitMQ:ConnectionString"]
            ?? throw new InvalidOperationException("RabbitMQ:ConnectionString is required."))
           .RequireTls(!builder.Environment.IsDevelopment())
           .ConfigureExchange(exchange => exchange.Name("orders").Type(RabbitMQExchangeType.Topic))
           .ConfigureQueue(queue => queue.Name("orders"))
           .ConfigureBinding(binding => binding.Exchange("orders").Queue("orders").RoutingKey("orders.placed"))
           .MapExchange<OrderPlacedEvent>("orders")
           .MapQueue<OrderPlacedEvent>("orders");
    });
#elif (UseAzureServiceBus)
    dispatch.UseAzureServiceBus("outbox-events", asb =>
    {
        asb.ConnectionString(builder.Configuration["AzureServiceBus:ConnectionString"]
            ?? throw new InvalidOperationException("AzureServiceBus:ConnectionString is required."));
        asb.ConfigureOptions(options => options.Sender.DefaultEntityName = builder.Configuration["AzureServiceBus:EntityName"] ?? "orders");
    });
#elif (UseAwsSqs)
    dispatch.UseAwsSqs("outbox-events", sqs =>
    {
        sqs.UseRegion(builder.Configuration["AWS:Region"] ?? "us-east-1");
        sqs.MapQueue<OrderPlacedEvent>(builder.Configuration["AWS:QueueUrl"]
            ?? throw new InvalidOperationException("AWS:QueueUrl is required."));
    });
#elif (UseGooglePubSub)
    dispatch.UseGooglePubSub("outbox-events", pubsub =>
    {
        pubsub.ProjectId(builder.Configuration["GooglePubSub:ProjectId"]
            ?? throw new InvalidOperationException("GooglePubSub:ProjectId is required."));
        pubsub.TopicId(builder.Configuration["GooglePubSub:TopicId"] ?? "orders");
    });
#endif
#if (UseKafka || UseRabbitMQ || UseAzureServiceBus || UseAwsSqs || UseGooglePubSub)
    dispatch.UseRouting(routing =>
    {
        routing.Transport.Route<OrderPlacedEvent>().To("outbox-events");
        routing.Endpoints.Route<OrderPlacedEvent>().To("outbox-events");
    });
#endif
});

builder.Services.AddExcalibur(excalibur => excalibur
    .AddOutbox(outbox =>
    {
#if (UseSqlServer)
        outbox.UseSqlServer(sql => sql.ConnectionStringName("OutboxStore"));
#elif (UsePostgreSql)
        outbox.UsePostgres(pg => pg.ConnectionStringName("OutboxStore"));
#elif (UseInMemoryDatabase)
        outbox.UseInMemory();
#endif
        outbox.WithProcessing(processing =>
        {
            processing.BatchSize(100)
                      .PollingInterval(TimeSpan.FromSeconds(5));
        })
        .EnableBackgroundProcessing();
    }));

#if (UseSqlServer)
// The processor requires an explicit recovery queue independently of its outbox store.
// Apply the package's outbox/dead-letter schema before starting this host.
builder.Services.AddSqlServerDeadLetterQueue(options =>
{
    options.ConnectionString = builder.Configuration.GetConnectionString("OutboxStore")
        ?? throw new InvalidOperationException("ConnectionStrings:OutboxStore is required.");
});
#endif

// OpenTelemetry: one call registers all Dispatch meters + activity sources
builder.Services.AddOpenTelemetry()
    .AddDispatchInstrumentation();

var app = builder.Build();

app.UseExceptionHandler();

app.UseHttpsRedirection();
// Bind a JSON command, return its typed result, and stage the event through framework middleware.
app.DispatchPostBodyAction<PlaceOrderCommand, Guid>("/orders");

app.Run();
