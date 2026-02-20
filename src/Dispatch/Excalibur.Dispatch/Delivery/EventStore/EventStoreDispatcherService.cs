// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions.Messaging;
using Excalibur.Dispatch.Diagnostics;
using Excalibur.Dispatch.Options.Delivery;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Delivery;

/// <summary>
/// Background service that continuously dispatches events from the event store.
/// </summary>
public partial class EventStoreDispatcherService(
	IEventStoreDispatcher eventStoreDispatcher,
	IOptions<EventStoreDispatcherOptions> options,
	ILogger<EventStoreDispatcherService> logger)
	: BackgroundService
{
	private readonly TimeSpan _pollInterval = options.Value.PollInterval;
	private readonly string _dispatcherId = Uuid7Extensions.GenerateString();

	/// <summary>
	/// Executes the background service to continuously process events from the event store.
	/// </summary>
	/// <param name="stoppingToken"> The cancellation token that signals when the service should stop. </param>
	/// <returns> A task representing the asynchronous operation. </returns>
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		LogServiceStarted();

		eventStoreDispatcher.Init(_dispatcherId);

		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				await eventStoreDispatcher.DispatchAsync(stoppingToken).ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				LogErrorProcessing(ex);
			}

			await Task.Delay(_pollInterval, stoppingToken).ConfigureAwait(false);
		}

		LogServiceStopped();
	}

	// Source-generated logging methods (Sprint 360 - EventId Migration Phase 1)
	[LoggerMessage(DeliveryEventId.EventStoreServiceStarted, LogLevel.Information,
		"EventStoreDispatcherService started.")]
	private partial void LogServiceStarted();

	[LoggerMessage(DeliveryEventId.EventStoreErrorProcessing, LogLevel.Error,
		"Error while processing EventStore.")]
	private partial void LogErrorProcessing(Exception ex);

	[LoggerMessage(DeliveryEventId.EventStoreServiceStopped, LogLevel.Information,
		"EventStoreDispatcherService stopped.")]
	private partial void LogServiceStopped();
}
