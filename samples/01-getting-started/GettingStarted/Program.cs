// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

// ============================================================================
// GettingStarted - Minimal Dispatch Template Sample
// ============================================================================
// This sample demonstrates the fundamentals of Dispatch messaging:
// - Commands (actions that change state)
// - Queries (read data without side effects)
// - Events (notify multiple handlers of something that happened)
// - [AutoRegister] attribute for opt-in service registration
//
// Run with: dotnet run
// Test endpoints with curl or your preferred HTTP client.
// ============================================================================

using Excalibur.Dispatch.Abstractions;

using GettingStarted.Messages;

var builder = WebApplication.CreateBuilder(args);

// Register Dispatch with handler discovery from this assembly
builder.Services.AddDispatch(typeof(Program).Assembly);

// Register the in-memory order store
// In a real app with source generators, you could use [AutoRegister] and AddGeneratedServices()
// See: docs-site/docs/source-generators/getting-started.md
builder.Services.AddSingleton<GettingStarted.Handlers.IOrderStore, GettingStarted.Handlers.OrderStore>();

var app = builder.Build();

// ============================================================================
// COMMAND endpoint: Create a new order
// Commands represent intent to change state and can return a value
// ============================================================================
app.MapPost("/orders", async (CreateOrderCommand cmd, IDispatcher dispatcher, CancellationToken ct) =>
{
	var context = app.Services.CreateScope().ServiceProvider
		.GetRequiredService<IMessageContext>();

	var result = await dispatcher.DispatchAsync<CreateOrderCommand, Guid>(cmd, context, ct);

	return result.Succeeded
		? Results.Ok(new { OrderId = result.ReturnValue, Message = "Order created successfully" })
		: Results.BadRequest(new { Error = result.ErrorMessage });
});

// ============================================================================
// QUERY endpoint: Get order details
// Queries read data without side effects
// ============================================================================
app.MapGet("/orders/{orderId:guid}", async (Guid orderId, IDispatcher dispatcher, CancellationToken ct) =>
{
	var context = app.Services.CreateScope().ServiceProvider
		.GetRequiredService<IMessageContext>();

	var query = new GetOrderQuery(orderId);
	var result = await dispatcher.DispatchAsync<GetOrderQuery, OrderDetails?>(query, context, ct);

	return result.Succeeded && result.ReturnValue is not null
		? Results.Ok(result.ReturnValue)
		: Results.NotFound(new { Error = "Order not found" });
});

// ============================================================================
// EVENT endpoint: Dispatch an event to multiple handlers
// Events notify interested parties that something happened
// ============================================================================
app.MapPost("/orders/{orderId:guid}/ship", async (Guid orderId, IDispatcher dispatcher, CancellationToken ct) =>
{
	var context = app.Services.CreateScope().ServiceProvider
		.GetRequiredService<IMessageContext>();

	var evt = new OrderShippedEvent(orderId, DateTimeOffset.UtcNow);
	var result = await dispatcher.DispatchAsync(evt, context, ct);

	return result.Succeeded
		? Results.Ok(new { Message = "Order shipped event published" })
		: Results.BadRequest(new { Error = result.ErrorMessage });
});

// ============================================================================
// Health check and info endpoint
// ============================================================================
app.MapGet("/", () => Results.Ok(new
{
	Name = "GettingStarted - Dispatch Template Sample",
	Endpoints = (IReadOnlyList<string>)
	[
		"POST /orders - Create a new order (Command)",
		"GET /orders/{orderId} - Get order details (Query)",
		"POST /orders/{orderId}/ship - Ship an order (Event)"
	],
	Documentation = "See README.md for full usage instructions"
}));

app.Run();
