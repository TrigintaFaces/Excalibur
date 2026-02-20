// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

Console.WriteLine("Transport Bindings API Demo");
Console.WriteLine("===========================");

// Create host builder
var builder = Host.CreateApplicationBuilder();

// Configure transport using the ADR-098 single entry point pattern
// InMemory transport - no external dependencies needed for testing/demonstration
builder.Services.AddInMemoryTransport("test");

// Other transports are available when you have the required infrastructure:
// builder.Services.AddKafkaTransport("kafka", k => k.BootstrapServers("localhost:9092"));
// builder.Services.AddRabbitMQTransport("rabbitmq", r => r.ConnectionString("amqp://localhost"));
// builder.Services.AddAzureServiceBusTransport("servicebus", sb => sb.ConnectionString("Endpoint=sb://..."));
// builder.Services.AddAzureStorageQueueTransport("orders", sq => sq.ConnectionString("..."));
// builder.Services.AddAzureEventHubsTransport("eventhubs", eh => eh.ConnectionString("Endpoint=sb://..."));

builder.Services.AddEventBindings(static b =>
{
	// TODO: Uncomment when transport implementations are available
	// _ = b.FromQueue("orders")
	// 	.RouteName("order-received")
	// 	.ToDispatcher("internal-event");

	// _ = b.FromTimer("cron")
	// 	.RouteType<TimerInfo>()
	// 	.ToDispatcher("internal-event");

	// _ = b.FromTransport("rabbitmq")
	// 	.RouteType<GenericDispatchMessage>()
	// 	.ToDispatcher("strict");

	// _ = b.FromTransport("kafka")
	// 	.RouteName("customer-event")
	// 	.ToDispatcher("strict");
});

// Build and run the host
var host = builder.Build();

// Demonstrate that the configuration worked
var transportRegistry = host.Services.GetRequiredService<TransportRegistry>();
var bindingRegistry = host.Services.GetRequiredService<TransportBindingRegistry>();

Console.WriteLine("\nRegistered Transports:");
foreach (var transportName in transportRegistry.GetTransportNames())
{
	var registration = transportRegistry.GetTransportRegistration(transportName);
	Console.WriteLine($"  - {transportName} ({registration.TransportType})");
}

Console.WriteLine("\nRegistered Bindings:");
foreach (var binding in bindingRegistry.GetBindings())
{
	Console.WriteLine($"  - {binding.Name}: {binding.EndpointPattern} -> Profile: {binding.PipelineProfile?.Name ?? "default"}");
}

Console.WriteLine("\nTransport Bindings API implementation complete!");
Console.WriteLine("Press any key to exit...");
Console.ReadKey();
