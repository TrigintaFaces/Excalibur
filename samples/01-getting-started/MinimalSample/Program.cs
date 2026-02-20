// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

// ============================================================================
// MinimalSample - Simplest Dispatch Integration Example
// ============================================================================
// Demonstrates the minimum viable Dispatch configuration with:
// - Command dispatching with response
// - Event dispatching (fire-and-forget)
// - Validation middleware
// ============================================================================

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Transport;
using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Validation;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using MinimalSample;

Console.WriteLine("================================================");
Console.WriteLine("  MinimalSample - Dispatch Basics");
Console.WriteLine("================================================");
Console.WriteLine();

// Build services with minimal configuration
var services = new ServiceCollection();

// Add logging (minimal setup - warning level to reduce noise)
services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));

// Configure Dispatch with handlers and validation
services.AddDispatch(dispatch =>
{
	_ = dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
	_ = dispatch.AddDispatchValidation();
});

var provider = services.BuildServiceProvider();

// Initialize the local message bus
_ = provider.GetRequiredKeyedService<IMessageBus>("Local");

// Get the dispatcher and create a context
var dispatcher = provider.GetRequiredService<IDispatcher>();
var context = DispatchContextInitializer.CreateDefaultContext(provider);

// Dispatch a command that returns a response
Console.WriteLine("Dispatching PingCommand...");
var response = await dispatcher.DispatchAsync<PingCommand, string>(
	new PingCommand { Text = "Hello" },
	context, cancellationToken: default).ConfigureAwait(false);
Console.WriteLine($"Command response: {response.ReturnValue}");
Console.WriteLine();

// Dispatch an event (can have multiple handlers)
Console.WriteLine("Dispatching PingEvent...");
var eventResult = await dispatcher.DispatchAsync(
	new PingEvent("Greetings"),
	context, cancellationToken: default).ConfigureAwait(false);
Console.WriteLine($"Event dispatched (Success: {eventResult.Succeeded})");
Console.WriteLine();

Console.WriteLine("================================================");
Console.WriteLine("  Sample Complete!");
Console.WriteLine("================================================");
