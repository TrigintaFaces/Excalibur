// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Messaging;

using Microsoft.Extensions.Hosting;

namespace Excalibur.Dispatch.Delivery;

/// <summary>
/// Background service that manages the outbox pattern for reliable message delivery in hosted environments. This service provides
/// continuous outbox processing with automatic startup, graceful shutdown, and proper resource cleanup for enterprise messaging applications.
/// </summary>
/// <param name="outbox"> Outbox implementation for message queuing and processing operations. </param>
public class OutboxService(IOutboxDispatcher outbox) : BackgroundService
{
	private readonly string _dispatcherId = Uuid7Extensions.GenerateString();

	/// <summary>
	/// Stops the outbox service with graceful shutdown, ensuring proper resource cleanup and pending message processing. This method
	/// coordinates service termination with outbox disposal to prevent message loss during shutdown.
	/// </summary>
	/// <param name="cancellationToken"> Cancellation token for coordinated shutdown timing. </param>
	/// <returns> Task representing the asynchronous service shutdown operation. </returns>
	public override async Task StopAsync(CancellationToken cancellationToken)
	{
		await base.StopAsync(cancellationToken).ConfigureAwait(false);
		await outbox.DisposeAsync().ConfigureAwait(false);
	}

	/// <summary>
	/// Executes the background outbox processing loop for continuous message dispatch operations. This method runs the outbox dispatcher
	/// with the unique service identifier, providing reliable message delivery throughout the application lifecycle.
	/// </summary>
	/// <param name="stoppingToken"> Cancellation token that triggers when the service should stop processing. </param>
	/// <returns> Task representing the long-running outbox processing operation. </returns>
	protected override Task ExecuteAsync(CancellationToken stoppingToken) => outbox.RunOutboxDispatchAsync(_dispatcherId, stoppingToken);
}
