// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Text.Json;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Messaging;

using Google.Cloud.Functions.Framework;

using GoogleCloudFunctionsSample.Messages;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace GoogleCloudFunctionsSample.Functions;

/// <summary>
/// HTTP-triggered Google Cloud Function for order operations.
/// Demonstrates Dispatch messaging integration with HTTP triggers.
/// </summary>
public class HttpFunction : IHttpFunction
{
	private readonly IDispatcher _dispatcher;
	private readonly ILogger<HttpFunction> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="HttpFunction"/> class.
	/// </summary>
	/// <param name="dispatcher">The Dispatch dispatcher.</param>
	/// <param name="logger">The logger instance.</param>
	public HttpFunction(IDispatcher dispatcher, ILogger<HttpFunction> logger)
	{
		_dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <summary>
	/// Handles incoming HTTP requests.
	/// </summary>
	/// <param name="context">The HTTP context.</param>
	/// <example>
	/// POST /
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
	public async Task HandleAsync(HttpContext context)
	{
		var request = context.Request;
		var response = context.Response;

		_logger.LogInformation("HTTP trigger: {Method} {Path}", request.Method, request.Path);

		// Route based on method and path
		if (request.Method == HttpMethods.Post && request.Path == "/orders")
		{
			await CreateOrderAsync(request, response).ConfigureAwait(false);
		}
		else if (request.Method == HttpMethods.Get && request.Path.StartsWithSegments("/orders", StringComparison.OrdinalIgnoreCase))
		{
			var orderId = request.Path.Value?.Split('/').LastOrDefault() ?? "unknown";
			await GetOrderAsync(orderId, response).ConfigureAwait(false);
		}
		else
		{
			response.StatusCode = (int)HttpStatusCode.NotFound;
			await response.WriteAsJsonAsync(new { error = "Not found" }).ConfigureAwait(false);
		}
	}

	private async Task CreateOrderAsync(HttpRequest request, HttpResponse response)
	{
		try
		{
			// Read and deserialize request body
			var orderRequest = await JsonSerializer.DeserializeAsync<CreateOrderRequest>(
				request.Body,
				new JsonSerializerOptions { PropertyNameCaseInsensitive = true }).ConfigureAwait(false);

			if (orderRequest is null)
			{
				response.StatusCode = (int)HttpStatusCode.BadRequest;
				await response.WriteAsJsonAsync(new { error = "Invalid order data" }).ConfigureAwait(false);
				return;
			}

			// Create order event
			var orderEvent = new OrderCreatedEvent(
				orderRequest.OrderId,
				orderRequest.CustomerId,
				orderRequest.TotalAmount,
				DateTimeOffset.UtcNow);

			// Create dispatch context
			var dispatchContext = DispatchContextInitializer.CreateDefaultContext();

			// Dispatch the event
			_ = await _dispatcher.DispatchAsync(orderEvent, dispatchContext, cancellationToken: default).ConfigureAwait(false);

			_logger.LogInformation("Order {OrderId} created successfully", orderRequest.OrderId);

			response.StatusCode = (int)HttpStatusCode.Created;
			await response.WriteAsJsonAsync(new
			{
				orderId = orderRequest.OrderId,
				status = "Created",
				timestamp = DateTimeOffset.UtcNow,
			}).ConfigureAwait(false);
		}
		catch (JsonException ex)
		{
			_logger.LogError(ex, "Failed to deserialize request body");
			response.StatusCode = (int)HttpStatusCode.BadRequest;
			await response.WriteAsJsonAsync(new { error = "Invalid JSON format" }).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error creating order");
			response.StatusCode = (int)HttpStatusCode.InternalServerError;
			await response.WriteAsJsonAsync(new { error = "Internal server error" }).ConfigureAwait(false);
		}
	}

	private static async Task GetOrderAsync(string orderId, HttpResponse response)
	{
		// In a real application, query Cloud Firestore or other data store
		response.StatusCode = (int)HttpStatusCode.OK;
		await response.WriteAsJsonAsync(new
		{
			orderId,
			status = "Pending",
			createdAt = DateTimeOffset.UtcNow.AddHours(-1),
			message = "This is a sample response. In production, query your database.",
		}).ConfigureAwait(false);
	}
}
