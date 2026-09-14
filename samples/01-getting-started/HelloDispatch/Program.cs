// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

// HelloDispatch: one package, one file. Dispatches a command (with a response) and an
// event (fire-and-forget, any number of handlers) through validation middleware.

using Excalibur.Dispatch;
using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Validation;

using HelloDispatch;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

var services = new ServiceCollection();

services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));

services.AddDispatch(dispatch =>
{
	_ = dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
	_ = dispatch.UseValidation();
});

// A console app must resolve the "Local" IMessageBus AddDispatch() registers before
// dispatching; ASP.NET Core / Generic Host apps do this automatically via a hosted service.
var provider = services.BuildServiceProvider();
_ = provider.GetRequiredKeyedService<IMessageBus>("Local");

var dispatcher = provider.GetRequiredService<IDispatcher>();
var context = DispatchContextInitializer.CreateDefaultContext(provider);

var response = await dispatcher.DispatchAsync<PingCommand, string>(
	new PingCommand { Text = "Hello" }, context, cancellationToken: default).ConfigureAwait(false);
Console.WriteLine($"Command response: {response.ReturnValue}");

var eventResult = await dispatcher.DispatchAsync(
	new PingEvent("Greetings"), context, cancellationToken: default).ConfigureAwait(false);
Console.WriteLine($"Event dispatched (Success: {eventResult.Succeeded})");

namespace HelloDispatch
{
	/// <summary>A command that returns a response. Represents intent to perform an action.</summary>
	internal sealed record PingCommand : IDispatchAction<string>
	{
		public string Text { get; init; } = string.Empty;
	}

	/// <summary>Handles <see cref="PingCommand"/> and returns a pong response.</summary>
	internal sealed class PingCommandHandler : IActionHandler<PingCommand, string>
	{
		public Task<string> HandleAsync(PingCommand action, CancellationToken cancellationToken)
		{
			ArgumentNullException.ThrowIfNull(action);
			return Task.FromResult($"Pong: {action.Text}");
		}
	}

	/// <summary>An event. Represents a fact that occurred and may have any number of handlers.</summary>
	internal sealed record PingEvent(string Message) : IDispatchEvent;

	/// <summary>Handles <see cref="PingEvent"/>.</summary>
	internal sealed class PingHandler : IEventHandler<PingEvent>
	{
		public Task HandleAsync(PingEvent eventMessage, CancellationToken cancellationToken)
		{
			ArgumentNullException.ThrowIfNull(eventMessage);
			Console.WriteLine($"[PingHandler] Event received: {eventMessage.Message}");
			return Task.CompletedTask;
		}
	}
}
