// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Delivery.Handlers;

/// <summary>
/// Reports, once at start-up, that the dispatch pipeline was composed without a single message handler.
/// </summary>
/// <remarks>
/// A host with no handlers is legal — a send-only application that publishes to a transport and consumes
/// nothing is a supported shape — so this warns and lets the host start rather than throwing.
/// <para>
/// It warns at all because the two failure modes are asymmetric. Dispatching an action or a query with no
/// handler throws on the first attempt, so that mistake announces itself. Publishing an EVENT with no
/// handler only logs, and only when an event is actually published, so a composition that registered
/// nothing can run for a long time quietly discarding events. One line at start-up turns that into an
/// immediate, actionable signal.
/// </para>
/// <para>
/// The check runs at start-up rather than at registration time so that handlers registered after the
/// pipeline was composed still count. A composition that has handlers logs nothing.
/// </para>
/// </remarks>
internal sealed partial class NoHandlersRegisteredStartupWarning(
	IHandlerRegistry handlerRegistry,
	ILogger<NoHandlersRegisteredStartupWarning> logger) : IHostedService
{
	private readonly IHandlerRegistry _handlerRegistry =
		handlerRegistry ?? throw new ArgumentNullException(nameof(handlerRegistry));

	private readonly ILogger<NoHandlersRegisteredStartupWarning> _logger =
		logger ?? throw new ArgumentNullException(nameof(logger));

	/// <inheritdoc />
	public Task StartAsync(CancellationToken cancellationToken)
	{
		if (_handlerRegistry.GetAll().Count == 0)
		{
			LogNoHandlersRegistered(_logger);
		}

		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

	[LoggerMessage(
		EventId = 2409,
		Level = LogLevel.Warning,
		Message = "No message handlers are registered, so every published event will be discarded and every "
			+ "action or query will throw on dispatch. Register handlers with AddDiscoveredHandlers() for a "
			+ "trimmed or ahead-of-time compiled application, or with AddHandlersFromAssembly(assembly) to "
			+ "find them by scanning. Ignore this if the application only sends.")]
	private static partial void LogNoHandlersRegistered(ILogger logger);
}
