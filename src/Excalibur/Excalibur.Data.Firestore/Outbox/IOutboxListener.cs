// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Data.Firestore.Outbox;

/// <summary>
/// Provides real-time listening capabilities for outbox message changes.
/// </summary>
/// <remarks>
/// <para>
/// Implementations subscribe to database change notifications (e.g., Firestore snapshot listeners)
/// to receive immediate notification when new outbox messages are staged, reducing polling latency.
/// </para>
/// <para>
/// Follows the Microsoft IHostedService pattern with Start/Stop lifecycle methods (2 methods).
/// </para>
/// </remarks>
public interface IOutboxListener
{
	/// <summary>
	/// Starts listening for outbox message changes.
	/// </summary>
	/// <param name="cancellationToken"> Token to monitor for cancellation requests. </param>
	/// <returns> A task representing the asynchronous start operation. </returns>
	Task StartListeningAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Stops listening for outbox message changes.
	/// </summary>
	/// <param name="cancellationToken"> Token to monitor for cancellation requests. </param>
	/// <returns> A task representing the asynchronous stop operation. </returns>
	Task StopListeningAsync(CancellationToken cancellationToken);
}
