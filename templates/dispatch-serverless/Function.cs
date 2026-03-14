#if (UseAzure)
using System.Net;

using Company.DispatchServerless.Messages;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Messaging;

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace Company.DispatchServerless;

/// <summary>
/// HTTP-triggered Azure Function demonstrating Dispatch messaging.
/// </summary>
public sealed class Function
{
    private readonly IDispatcher _dispatcher;
    private readonly ILogger<Function> _logger;

    public Function(IDispatcher dispatcher, ILogger<Function> logger)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Function("ProcessOrder")]
    public async Task<HttpResponseData> ProcessOrderAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "orders")] HttpRequestData req)
    {
        _logger.LogInformation("Processing order request");

        var request = await req.ReadFromJsonAsync<CreateOrderAction>().ConfigureAwait(false);
        if (request is null)
        {
            var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
            await badResponse.WriteAsJsonAsync(new { error = "Invalid order data" }).ConfigureAwait(false);
            return badResponse;
        }

        var context = DispatchContextInitializer.CreateDefaultContext();
        _ = await _dispatcher.DispatchAsync(request, context, cancellationToken: default).ConfigureAwait(false);

        _logger.LogInformation("Order {OrderId} dispatched", request.OrderId);

        var response = req.CreateResponse(HttpStatusCode.Accepted);
        await response.WriteAsJsonAsync(new { orderId = request.OrderId, status = "Accepted" }).ConfigureAwait(false);
        return response;
    }
}
#elif (UseAws)
using System.Text.Json;

using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;

using Company.DispatchServerless.Messages;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Messaging;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace Company.DispatchServerless;

/// <summary>
/// AWS Lambda function demonstrating Dispatch messaging.
/// </summary>
public class Function
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<Function> _logger;

    public Function()
    {
        _serviceProvider = Startup.ServiceProvider;
        _logger = _serviceProvider.GetRequiredService<ILogger<Function>>();
    }

    public async Task<APIGatewayProxyResponse> ProcessOrderAsync(
        APIGatewayProxyRequest request,
        ILambdaContext lambdaContext)
    {
        _logger.LogInformation("Processing order, RequestId: {RequestId}", lambdaContext.AwsRequestId);

        var orderAction = JsonSerializer.Deserialize<CreateOrderAction>(
            request.Body ?? "{}",
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (orderAction is null)
        {
            return new APIGatewayProxyResponse
            {
                StatusCode = 400,
                Body = JsonSerializer.Serialize(new { error = "Invalid order data" }),
            };
        }

        using var scope = _serviceProvider.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();
        var context = DispatchContextInitializer.CreateDefaultContext();

        _ = await dispatcher.DispatchAsync(orderAction, context, cancellationToken: default).ConfigureAwait(false);

        _logger.LogInformation("Order {OrderId} dispatched", orderAction.OrderId);

        return new APIGatewayProxyResponse
        {
            StatusCode = 202,
            Body = JsonSerializer.Serialize(new { orderId = orderAction.OrderId, status = "Accepted" }),
        };
    }
}
#else
using System.Net;
using System.Text.Json;

using Company.DispatchServerless.Messages;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Messaging;

using Google.Cloud.Functions.Framework;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Company.DispatchServerless;

/// <summary>
/// HTTP-triggered Google Cloud Function demonstrating Dispatch messaging.
/// </summary>
public class Function : IHttpFunction
{
    private readonly IDispatcher _dispatcher;
    private readonly ILogger<Function> _logger;

    public Function(IDispatcher dispatcher, ILogger<Function> logger)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task HandleAsync(HttpContext httpContext)
    {
        var request = httpContext.Request;
        var response = httpContext.Response;

        _logger.LogInformation("Processing order request: {Method} {Path}", request.Method, request.Path);

        if (request.Method != HttpMethods.Post)
        {
            response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
            await response.WriteAsJsonAsync(new { error = "POST method required" }).ConfigureAwait(false);
            return;
        }

        var orderAction = await JsonSerializer.DeserializeAsync<CreateOrderAction>(
            request.Body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }).ConfigureAwait(false);

        if (orderAction is null)
        {
            response.StatusCode = (int)HttpStatusCode.BadRequest;
            await response.WriteAsJsonAsync(new { error = "Invalid order data" }).ConfigureAwait(false);
            return;
        }

        var dispatchContext = DispatchContextInitializer.CreateDefaultContext();
        _ = await _dispatcher.DispatchAsync(orderAction, dispatchContext, cancellationToken: default).ConfigureAwait(false);

        _logger.LogInformation("Order {OrderId} dispatched", orderAction.OrderId);

        response.StatusCode = (int)HttpStatusCode.Accepted;
        await response.WriteAsJsonAsync(new { orderId = orderAction.OrderId, status = "Accepted" }).ConfigureAwait(false);
    }
}
#endif
