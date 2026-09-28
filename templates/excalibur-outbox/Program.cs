using Excalibur.Dispatch.Configuration;
// Required, and it stays required until the release that moves this extension is PUBLISHED.
// Template validation compiles this scaffold against the published feed, not against source, so
// it sees whichever namespace the released package declares UseOutbox in. In the source tree the
// extension now lives in Microsoft.Extensions.DependencyInjection and needs no import at all;
// until that ships, the released package still declares it here. Keeping the directive is correct
// in BOTH worlds -- the namespace still exists either way (the middleware types live in it), so
// after the move publishes this line is merely redundant rather than wrong.
using Excalibur.Dispatch.Middleware.Outbox;
using Excalibur.Dispatch.Observability.Metrics;
#if (UseSqlServer)
using Excalibur.Outbox.SqlServer;
#endif

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);

    // REQUIRED. AddDispatch() seats no behavioural middleware, so registering an outbox STORE is
    // not enough on its own -- the staging stage has to be named. Without this line a handler that
    // writes to the outbox throws at dispatch, because there is no stage to stage it.
    dispatch.UseOutbox();
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
    .AddOutbox(outbox =>
    {
#if (UseSqlServer)
        outbox.UseSqlServer(sql => sql.ConnectionStringName("OutboxStore"));
#endif
        outbox.WithProcessing(processing =>
        {
            processing.BatchSize(100)
                      .PollingInterval(TimeSpan.FromSeconds(5));
        })
        .EnableBackgroundProcessing();
    }));

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
