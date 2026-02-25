// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions.Messaging;
using Excalibur.Dispatch.Diagnostics;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Delivery;

/// <summary>
/// Simple in-memory dispatcher used for tests and default hosting.
/// </summary>
public sealed partial class InMemoryEventStoreDispatcher(ILogger<InMemoryEventStoreDispatcher> logger) : IEventStoreDispatcher
{
	/// <summary>
	/// Initializes the in-memory dispatcher with the specified dispatcher ID.
	/// </summary>
	/// <param name="dispatcherId"> The unique identifier for this dispatcher instance. </param>
	public void Init(string dispatcherId) => LogEventStoreDispatcherInitialized(dispatcherId);

	/// <summary>
	/// Performs a no-operation dispatch since this is an in-memory implementation.
	/// </summary>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A completed task. </returns>
	public async Task DispatchAsync(CancellationToken cancellationToken) =>

		// no-op dispatcher
		await Task.CompletedTask.ConfigureAwait(false);

	// Source-generated logging methods
	[LoggerMessage(DeliveryEventId.EventStoreDispatcherInitialized, LogLevel.Information,
		"EventStore dispatcher initialized with ID '{DispatcherId}'")]
	private partial void LogEventStoreDispatcherInitialized(string dispatcherId);
}
