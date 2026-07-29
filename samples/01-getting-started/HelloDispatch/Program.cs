// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

// ============================================================================
// HelloDispatch - the simplest end-to-end Dispatch app
// ============================================================================
// One package (Excalibur.Dispatch) and one file. Everything a newcomer needs
// in the first five minutes:
//   - Command dispatching with a response
//   - Event dispatching (fire-and-forget, may have multiple handlers)
//   - Validation middleware
// The messages and handlers are inlined below the program on purpose — in a real
// app you would split them into their own files, but a first sample stays in one.
// ============================================================================

using Excalibur.Dispatch;
using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Validation;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

Console.WriteLine("================================================");
Console.WriteLine("  HelloDispatch - Dispatch Basics");
Console.WriteLine("================================================");
Console.WriteLine();

// Build services with minimal configuration.
var services = new ServiceCollection();

// Add logging (minimal setup - warning level to reduce noise).
services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));

// Configure Dispatch with handlers and validation.
services.AddDispatch(dispatch =>
{
	_ = dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
	_ = dispatch.UseValidation();
});

var provider = services.BuildServiceProvider();

// Initialize the local message bus.
// AddDispatch() registers a keyed IMessageBus named "Local" for in-process messaging.
// Resolving it here ensures the bus is initialized before dispatching messages.
// In ASP.NET Core / Generic Host apps, this happens automatically via hosted services.
_ = provider.GetRequiredKeyedService<IMessageBus>("Local");

// Get the dispatcher and create a context.
var dispatcher = provider.GetRequiredService<IDispatcher>();
var context = DispatchContextInitializer.CreateDefaultContext(provider);

// Dispatch a command that returns a response.
Console.WriteLine("Dispatching PingCommand...");
var response = await dispatcher.DispatchAsync<PingCommand, string>(
	new PingCommand { Text = "Hello" },
	context, cancellationToken: default).ConfigureAwait(false);
Console.WriteLine($"Command response: {response.ReturnValue}");
Console.WriteLine();

// Dispatch an event (can have multiple handlers).
Console.WriteLine("Dispatching PingEvent...");
var eventResult = await dispatcher.DispatchAsync(
	new PingEvent("Greetings"),
	context, cancellationToken: default).ConfigureAwait(false);
Console.WriteLine($"Event dispatched (Success: {eventResult.Succeeded})");
Console.WriteLine();

Console.WriteLine("================================================");
Console.WriteLine("  Sample Complete!");
Console.WriteLine("================================================");

// ============================================================================
// Messages and handlers
// (normally one type per file — inlined here to keep the sample to a single file)
// ============================================================================

/// <summary>
/// A simple ping command that returns a pong response.
/// Commands represent intent to perform an action and may return a result.
/// </summary>
internal sealed record PingCommand : IDispatchAction<string>
{
	/// <summary>Gets or initializes the text to include in the ping.</summary>
	public string Text { get; init; } = string.Empty;
}

/// <summary>Handles the <see cref="PingCommand"/> and returns a pong response.</summary>
internal sealed class PingCommandHandler : IActionHandler<PingCommand, string>
{
	/// <inheritdoc />
	public Task<string> HandleAsync(PingCommand action, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(action);
		Console.WriteLine($"[PingCommandHandler] Handling: {action.Text}");
		return Task.FromResult($"Pong: {action.Text}");
	}
}

/// <summary>
/// A simple ping event.
/// Events represent facts that have occurred and can have multiple handlers.
/// </summary>
internal sealed record PingEvent(string Message) : IDispatchEvent;

/// <summary>Handles the <see cref="PingEvent"/>. Events can have multiple handlers.</summary>
internal sealed class PingHandler : IEventHandler<PingEvent>
{
	/// <inheritdoc />
	public Task HandleAsync(PingEvent eventMessage, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(eventMessage);
		Console.WriteLine($"[PingHandler] Event received: {eventMessage.Message}");
		return Task.CompletedTask;
	}
}
