// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Cdc.Postgres;

/// <summary>
/// Defines the contract for a Postgres Change Data Capture (CDC) processor.
/// </summary>
public interface IPostgresCdcProcessor : IAsyncDisposable, IDisposable
{
	/// <summary>
	/// Starts processing CDC changes and invokes the event handler for each change.
	/// </summary>
	Task StartAsync(
		Func<PostgresDataChangeEvent, CancellationToken, Task> eventHandler,
		CancellationToken cancellationToken);

	/// <summary>
	/// Processes a single batch of CDC changes and returns.
	/// </summary>
	Task<int> ProcessBatchAsync(
		Func<PostgresDataChangeEvent, CancellationToken, Task> eventHandler,
		CancellationToken cancellationToken);

	/// <summary>
	/// Gets the current processing position.
	/// </summary>
	Task<PostgresCdcPosition> GetCurrentPositionAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Confirms that changes up to the specified position have been processed.
	/// </summary>
	Task ConfirmPositionAsync(
		PostgresCdcPosition position,
		CancellationToken cancellationToken);
}
