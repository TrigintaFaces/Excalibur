using Company.DispatchMinimalApi.Actions;
using Company.DispatchMinimalApi.Infrastructure;
using Excalibur.Dispatch;
using Excalibur.Dispatch.Hosting.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSingleton<InMemoryOrderStore>();

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

// OpenTelemetry: one call registers all Dispatch meters + activity sources
builder.Services.AddOpenTelemetry()
    .AddDispatchInstrumentation();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();

// The action is bound from the request body, dispatched, and its result is turned into the
// correct HTTP response automatically — no controller, no manual result-unwrapping:
//   validation failure -> 400 ProblemDetails, authorization failure -> 403, success -> 200.
// (Pass a responseHandler argument if you want 201 Created or a custom mapping.)
app.DispatchPostAction<CreateOrderAction, CreateOrderResult>("/api/orders");

// A route-bound query reads naturally as a plain MapGet (404-on-not-found is a query concern the
// generic result bridge does not model); the command path above shows the DispatchPostAction bridge.
app.MapGet("/api/orders/{id:guid}", async (Guid id, IDispatcher dispatcher, CancellationToken cancellationToken) =>
{
    var result = await dispatcher.DispatchAsync<GetOrderAction, OrderResult?>(new GetOrderAction(id), cancellationToken).ConfigureAwait(false);
    return result.ReturnValue is not null ? Results.Ok(result.ReturnValue) : Results.NotFound();
});

app.Run();
