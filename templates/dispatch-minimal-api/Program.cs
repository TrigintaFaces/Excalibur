using Company.DispatchMinimalApi.Actions;
using Company.DispatchMinimalApi.Infrastructure;
using Excalibur.Dispatch.Abstractions;

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

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();

app.MapPost("/api/orders", async (CreateOrderAction action, IDispatcher dispatcher, CancellationToken cancellationToken) =>
{
    var result = await dispatcher.DispatchAsync<CreateOrderAction, Guid>(action, cancellationToken).ConfigureAwait(false);
    return Results.Created($"/api/orders/{result.ReturnValue}", result.ReturnValue);
});

app.MapGet("/api/orders/{id:guid}", async (Guid id, IDispatcher dispatcher, CancellationToken cancellationToken) =>
{
    var result = await dispatcher.DispatchAsync<GetOrderAction, OrderResult?>(new GetOrderAction(id), cancellationToken).ConfigureAwait(false);
    return result.ReturnValue is not null ? Results.Ok(result.ReturnValue) : Results.NotFound();
});

app.Run();
