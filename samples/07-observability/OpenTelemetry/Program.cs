// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA1506 // Avoid excessive class coupling - expected for sample demonstrating multiple integrations

// OpenTelemetry Observability Sample
// ===================================
// This sample demonstrates how to use OpenTelemetry with Dispatch messaging
// for distributed tracing, metrics collection, and observability.
//
// Prerequisites:
// 1. Docker for running Jaeger: docker-compose up -d
// 2. View traces at: http://localhost:16686
//
// The sample creates spans for:
// - HTTP requests (ASP.NET Core instrumentation)
// - Dispatch message processing
// - Custom business operations

using System.Diagnostics;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Observability.Metrics;
using Excalibur.Dispatch.Serialization;

using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

using OpenTelemetrySample.Messages;
using OpenTelemetrySample.Models;

var builder = WebApplication.CreateBuilder(args);

// Configure service name for tracing
const string serviceName = "dispatch-otel-sample";
const string serviceVersion = "1.0.0";

// Configure OpenTelemetry
builder.Services.AddOpenTelemetry()
	.ConfigureResource(resource => resource
		.AddService(serviceName: serviceName, serviceVersion: serviceVersion)
		.AddAttributes(new Dictionary<string, object>
		{
			["deployment.environment"] = builder.Environment.EnvironmentName,
			["host.name"] = Environment.MachineName,
		}))
	.WithTracing(tracing => tracing
		// ASP.NET Core instrumentation
		.AddAspNetCoreInstrumentation(options =>
		{
			options.RecordException = true;
			options.Filter = ctx => !ctx.Request.Path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase);
		})
		// HTTP client instrumentation
		.AddHttpClientInstrumentation(options =>
		{
			options.RecordException = true;
		})
		// Add Dispatch activity source for message tracing
		.AddSource(DispatchActivitySource.Name)
		// Export to console for demo (use OTLP for production)
		.AddConsoleExporter()
		// Export to Jaeger via OTLP
		.AddOtlpExporter(options =>
		{
			options.Endpoint = new Uri(builder.Configuration["Otel:Endpoint"] ?? "http://localhost:4317");
		}))
	.WithMetrics(metrics => metrics
		// ASP.NET Core metrics
		.AddAspNetCoreInstrumentation()
		// HTTP client metrics
		.AddHttpClientInstrumentation()
		// Runtime metrics
		.AddRuntimeInstrumentation()
		// Add Dispatch metrics
		.AddDispatchMetrics()
		.AddTransportMetrics()
		// Export to console for demo
		.AddConsoleExporter()
		// Export to OTLP
		.AddOtlpExporter(options =>
		{
			options.Endpoint = new Uri(builder.Configuration["Otel:Endpoint"] ?? "http://localhost:4317");
		}));

// Configure Dispatch messaging
// Handlers are auto-registered with DI by AddHandlersFromAssembly
builder.Services.AddDispatch(dispatch =>
{
	_ = dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
	_ = dispatch.AddDispatchSerializer<DispatchJsonSerializer>(version: 0);
});

var app = builder.Build();

// Sample endpoints
app.MapGet("/", () => "OpenTelemetry Sample - POST /orders to create traces");

app.MapPost("/orders", async (OrderRequest request, IDispatcher dispatcher, ILogger<Program> logger) =>
{
	// Create a custom span for order processing
	using var activity = DispatchActivitySource.Instance.StartActivity("ProcessOrder");
	_ = (activity?.SetTag("order.id", request.OrderId));
	_ = (activity?.SetTag("order.customer_id", request.CustomerId));
	_ = (activity?.SetTag("order.amount", request.Amount));

	logger.LogInformation("Processing order {OrderId} for customer {CustomerId}",
		request.OrderId, request.CustomerId);

	// Create and dispatch event
	var orderEvent = new OrderProcessedEvent(
		request.OrderId,
		request.CustomerId,
		request.Amount,
		DateTimeOffset.UtcNow);

	var context = DispatchContextInitializer.CreateDefaultContext();

	// Add trace context to dispatch context for correlation
	if (Activity.Current != null)
	{
		context.Properties["TraceId"] = Activity.Current.TraceId.ToString();
		context.Properties["SpanId"] = Activity.Current.SpanId.ToString();
	}

	_ = await dispatcher.DispatchAsync(orderEvent, context, cancellationToken: default).ConfigureAwait(false);

	_ = (activity?.SetStatus(ActivityStatusCode.Ok));

	return Results.Accepted(value: new
	{
		orderId = request.OrderId,
		status = "Processed",
		traceId = Activity.Current?.TraceId.ToString(),
	});
});

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

Console.WriteLine("OpenTelemetry Sample");
Console.WriteLine("====================");
Console.WriteLine();
Console.WriteLine("Endpoints:");
Console.WriteLine("  POST /orders - Create order (generates traces)");
Console.WriteLine("  GET  /health - Health check");
Console.WriteLine();
Console.WriteLine("View traces at: http://localhost:16686 (Jaeger)");
Console.WriteLine();
Console.WriteLine("Start Jaeger with: docker-compose up -d");
Console.WriteLine();

app.Run();
