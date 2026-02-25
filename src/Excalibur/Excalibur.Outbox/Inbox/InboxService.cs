// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Messaging;

using Excalibur.Outbox.Diagnostics;
using Excalibur.Outbox.Health;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Delivery;

/// <summary>
/// Background service that continuously processes messages from the inbox. Provides reliable message processing by running inbox dispatch
/// operations as a long-running background service with proper lifecycle management.
/// </summary>
public partial class InboxService : BackgroundService
{
	private readonly IInbox _inbox;
	private readonly BackgroundServiceHealthState? _healthState;
	private readonly ILogger<InboxService> _logger;
	private readonly TimeSpan _drainTimeout;
	private readonly string _dispatcherId = Uuid7Extensions.GenerateString();

	/// <summary>
	/// Initializes a new instance of the <see cref="InboxService"/> class.
	/// </summary>
	/// <param name="inbox">The inbox instance to process messages from.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="healthState">Optional health state tracker for health check integration.</param>
	/// <param name="drainTimeoutSeconds">The drain timeout in seconds for graceful shutdown. Default is 30.</param>
	public InboxService(
		IInbox inbox,
		ILogger<InboxService> logger,
		BackgroundServiceHealthState? healthState = null,
		int drainTimeoutSeconds = 30)
	{
		_inbox = inbox ?? throw new ArgumentNullException(nameof(inbox));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_healthState = healthState;
		_drainTimeout = TimeSpan.FromSeconds(drainTimeoutSeconds);
	}

	/// <summary>
	/// Stops the inbox service and ensures proper cleanup of inbox resources. Gracefully shuts down the background processing and disposes
	/// the inbox.
	/// </summary>
	/// <param name="cancellationToken"> Token to signal cancellation of the stop operation. </param>
	/// <returns> A task that represents the asynchronous stop operation. </returns>
	public override async Task StopAsync(CancellationToken cancellationToken)
	{
		_healthState?.MarkStopped();

		using var drainCts = new CancellationTokenSource(_drainTimeout);
		using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(
			cancellationToken, drainCts.Token);

		try
		{
			await base.StopAsync(combinedCts.Token).ConfigureAwait(false);
		}
		catch (OperationCanceledException) when (drainCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
		{
			LogDrainTimeoutExceeded(_drainTimeout);
		}

		await _inbox.DisposeAsync().ConfigureAwait(false);
	}

	/// <summary>
	/// Executes the inbox processing loop continuously until cancellation is requested. Runs the inbox dispatch operations with the unique
	/// dispatcher identifier.
	/// </summary>
	/// <param name="stoppingToken"> Token to signal when the service should stop processing. </param>
	/// <returns> A task that represents the long-running inbox processing operation. </returns>
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		_healthState?.MarkStarted();

		BackgroundServiceMetrics.RecordProcessingCycle(
			BackgroundServiceTypes.Inbox,
			BackgroundServiceResults.Success);

		try
		{
			_ = await _inbox.RunInboxDispatchAsync(_dispatcherId, stoppingToken).ConfigureAwait(false);
		}
		catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
		{
			// Normal shutdown
		}
		catch (Exception ex)
		{
			BackgroundServiceMetrics.RecordProcessingError(
				BackgroundServiceTypes.Inbox,
				ex.GetType().Name);
			throw;
		}
		finally
		{
			_healthState?.MarkStopped();
		}
	}

	[LoggerMessage(OutboxEventId.InboxBackgroundServiceDrainTimeout, LogLevel.Warning,
		"Inbox background service drain timeout exceeded ({DrainTimeout}).")]
	private partial void LogDrainTimeoutExceeded(TimeSpan drainTimeout);
}
