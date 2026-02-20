// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Middleware;

/// <summary>
/// Service interface for managing outbox operations.
/// </summary>
public interface IOutboxService
{
	/// <summary>
	/// Stages a message in the outbox for later delivery.
	/// </summary>
	/// <param name="entry"> The outbox entry to stage. </param>
	/// <param name="transaction"> Optional transaction context. </param>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns> A task representing the staging operation. </returns>
	Task StageMessageAsync(
		OutboxEntry entry,
		object? transaction,
		CancellationToken cancellationToken);
}
