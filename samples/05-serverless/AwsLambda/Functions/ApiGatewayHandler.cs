// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Text.Json;

using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;

using AwsLambdaSample.Messages;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Messaging;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AwsLambdaSample.Functions;

/// <summary>
/// AWS Lambda function handling API Gateway HTTP requests.
/// Demonstrates Dispatch messaging integration with API Gateway triggers.
/// </summary>
public class ApiGatewayHandler
{
	private readonly IServiceProvider _serviceProvider;
	private readonly ILogger<ApiGatewayHandler> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="ApiGatewayHandler"/> class.
	/// </summary>
	public ApiGatewayHandler()
	{
		_serviceProvider = Startup.ServiceProvider;
		_logger = _serviceProvider.GetRequiredService<ILogger<ApiGatewayHandler>>();
	}

	/// <summary>
	/// Handles POST /orders request to create a new order.
	/// </summary>
	/// <param name="request">The API Gateway proxy request.</param>
	/// <param name="context">The Lambda execution context.</param>
	/// <returns>API Gateway proxy response.</returns>
	/// <example>
	/// POST /orders
	/// Content-Type: application/json
	///
	/// {
	///   "orderId": "ORD-001",
	///   "customerId": "CUST-100",
	///   "totalAmount": 99.99,
	///   "items": [
	///     { "productId": "PROD-1", "productName": "Widget", "quantity": 2, "unitPrice": 49.99 }
	///   ]
	/// }
	/// </example>
	[LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
	public async Task<APIGatewayProxyResponse> CreateOrderAsync(
		APIGatewayProxyRequest request,
		ILambdaContext context)
	{
		_logger.LogInformation("API Gateway: CreateOrder invoked, RequestId: {RequestId}", context.AwsRequestId);

		try
		{
			if (string.IsNullOrEmpty(request.Body))
			{
				return CreateResponse(HttpStatusCode.BadRequest, new { error = "Request body is required" });
			}

			// Deserialize the order request
			var orderRequest = JsonSerializer.Deserialize<CreateOrderRequest>(
				request.Body,
				new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

			if (orderRequest is null)
			{
				return CreateResponse(HttpStatusCode.BadRequest, new { error = "Invalid order data" });
			}

			// Create order event
			var orderEvent = new OrderCreatedEvent(
				orderRequest.OrderId,
				orderRequest.CustomerId,
				orderRequest.TotalAmount,
				DateTimeOffset.UtcNow);

			// Get dispatcher from DI
			using var scope = _serviceProvider.CreateScope();
			var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();

			// Create dispatch context
			var dispatchContext = DispatchContextInitializer.CreateDefaultContext();

			// Dispatch the event
			_ = await dispatcher.DispatchAsync(orderEvent, dispatchContext, cancellationToken: default).ConfigureAwait(false);

			_logger.LogInformation("Order {OrderId} created successfully", orderRequest.OrderId);

			return CreateResponse(HttpStatusCode.Created,
				new { orderId = orderRequest.OrderId, status = "Created", timestamp = DateTimeOffset.UtcNow, });
		}
		catch (JsonException ex)
		{
			_logger.LogError(ex, "Failed to deserialize request body");
			return CreateResponse(HttpStatusCode.BadRequest, new { error = "Invalid JSON format" });
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error creating order");
			return CreateResponse(HttpStatusCode.InternalServerError, new { error = "Internal server error" });
		}
	}

	/// <summary>
	/// Handles GET /orders/{orderId} request.
	/// </summary>
	/// <param name="request">The API Gateway proxy request.</param>
	/// <param name="context">The Lambda execution context.</param>
	/// <returns>API Gateway proxy response.</returns>
	[LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
	public Task<APIGatewayProxyResponse> GetOrderAsync(
		APIGatewayProxyRequest request,
		ILambdaContext context)
	{
		var orderId = "unknown";
		if (request.PathParameters?.TryGetValue("orderId", out var id) == true)
		{
			orderId = id;
		}

		_logger.LogInformation("API Gateway: GetOrder for {OrderId}", orderId);

		// In a real application, query DynamoDB or other data store
		var response = CreateResponse(HttpStatusCode.OK, new
		{
			orderId,
			status = "Pending",
			createdAt = DateTimeOffset.UtcNow.AddHours(-1),
			message = "This is a sample response. In production, query your database.",
		});

		return Task.FromResult(response);
	}

	private static APIGatewayProxyResponse CreateResponse(HttpStatusCode statusCode, object body)
	{
		return new APIGatewayProxyResponse
		{
			StatusCode = (int)statusCode,
			Headers = new Dictionary<string, string>
			{
				["Content-Type"] = "application/json",
				["X-Request-Id"] = Guid.NewGuid().ToString(),
			},
			Body = JsonSerializer.Serialize(body),
		};
	}
}
