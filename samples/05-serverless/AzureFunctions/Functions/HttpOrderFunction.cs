// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using System.Net;

using AzureFunctionsSample.Messages;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Messaging;

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace AzureFunctionsSample.Functions;

/// <summary>
/// HTTP-triggered Azure Function for order operations.
/// Demonstrates Dispatch messaging integration with HTTP triggers.
/// </summary>
public sealed class HttpOrderFunction
{
	private readonly IDispatcher _dispatcher;
	private readonly ILogger<HttpOrderFunction> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="HttpOrderFunction"/> class.
	/// </summary>
	/// <param name="dispatcher">The Dispatch dispatcher.</param>
	/// <param name="logger">The logger instance.</param>
	public HttpOrderFunction(IDispatcher dispatcher, ILogger<HttpOrderFunction> logger)
	{
		_dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <summary>
	/// Creates a new order via HTTP POST request.
	/// </summary>
	/// <param name="req">The HTTP request containing order data.</param>
	/// <returns>HTTP response with order creation result.</returns>
	/// <example>
	/// POST /api/orders
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
	[Function("CreateOrder")]
	public async Task<HttpResponseData> CreateOrderAsync(
		[HttpTrigger(AuthorizationLevel.Function, "post", Route = "orders")] HttpRequestData req)
	{
		_logger.LogInformation("HTTP trigger: CreateOrder invoked");

		try
		{
			// Deserialize the order request from request body
			var request = await req.ReadFromJsonAsync<CreateOrderRequest>().ConfigureAwait(false);

			if (request is null)
			{
				_logger.LogWarning("Invalid request: Order data is null");
				var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
				await badResponse.WriteAsJsonAsync(new { error = "Invalid order data" }).ConfigureAwait(false);
				return badResponse;
			}

			// Create an order created event
			var orderEvent = new OrderCreatedEvent(
				request.OrderId,
				request.CustomerId,
				request.TotalAmount,
				DateTimeOffset.UtcNow);

			// Create dispatch context
			var context = DispatchContextInitializer.CreateDefaultContext();

			// Dispatch the event using Excalibur messaging
			_ = await _dispatcher.DispatchAsync(orderEvent, context, cancellationToken: default).ConfigureAwait(false);

			_logger.LogInformation("Order {OrderId} created successfully", request.OrderId);

			var response = req.CreateResponse(HttpStatusCode.Created);
			await response.WriteAsJsonAsync(new
			{
				orderId = request.OrderId,
				status = "Created",
				timestamp = DateTimeOffset.UtcNow,
			}).ConfigureAwait(false);

			return response;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error creating order");
			var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
			await errorResponse.WriteAsJsonAsync(new { error = "Failed to create order" }).ConfigureAwait(false);
			return errorResponse;
		}
	}

	/// <summary>
	/// Gets order status via HTTP GET request.
	/// </summary>
	/// <param name="req">The HTTP request.</param>
	/// <param name="orderId">The order identifier.</param>
	/// <returns>HTTP response with order status.</returns>
	[Function("GetOrder")]
	public async Task<HttpResponseData> GetOrderAsync(
		[HttpTrigger(AuthorizationLevel.Function, "get", Route = "orders/{orderId}")] HttpRequestData req,
		string orderId)
	{
		_logger.LogInformation("HTTP trigger: GetOrder for {OrderId}", orderId);

		// In a real application, you would query the database
		var response = req.CreateResponse(HttpStatusCode.OK);
		await response.WriteAsJsonAsync(new
		{
			orderId,
			status = "Pending",
			createdAt = DateTimeOffset.UtcNow.AddHours(-1),
			message = "This is a sample response. In production, query your database.",
		}).ConfigureAwait(false);

		return response;
	}
}
