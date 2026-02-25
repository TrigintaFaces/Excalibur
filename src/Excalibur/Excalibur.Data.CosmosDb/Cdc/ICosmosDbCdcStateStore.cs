// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Data.CosmosDb.Cdc;

/// <summary>
/// Defines the contract for managing CosmosDb CDC continuation token state.
/// </summary>
/// <remarks>
/// Extends the shared <see cref="ICdcStateStore"/> contract with CosmosDb-specific
/// strongly-typed position accessors.
/// </remarks>
public interface ICosmosDbCdcStateStore : ICdcStateStore, IAsyncDisposable, IDisposable
{
	/// <summary>
	/// Gets the last saved CosmosDb-typed position for a processor.
	/// </summary>
	/// <param name="processorName">The unique processor name.</param>
	/// <param name="cancellationToken">A token to observe for cancellation requests.</param>
	/// <returns>The last saved position, or null if no position exists.</returns>
	new Task<CosmosDbCdcPosition?> GetPositionAsync(
		string processorName,
		CancellationToken cancellationToken);

	/// <summary>
	/// Saves the current CosmosDb-typed position for a processor.
	/// </summary>
	/// <param name="processorName">The unique processor name.</param>
	/// <param name="position">The position to save.</param>
	/// <param name="cancellationToken">A token to observe for cancellation requests.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	Task SavePositionAsync(
		string processorName,
		CosmosDbCdcPosition position,
		CancellationToken cancellationToken);

	/// <summary>
	/// Deletes the saved position for a processor.
	/// </summary>
	/// <param name="processorName">The unique processor name.</param>
	/// <param name="cancellationToken">A token to observe for cancellation requests.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	new Task DeletePositionAsync(
		string processorName,
		CancellationToken cancellationToken);
}

/// <summary>
/// Represents a CDC state entry for CosmosDb position tracking.
/// </summary>
public sealed class CosmosDbCdcStateEntry
{
	/// <summary>
	/// Gets or sets the processor name.
	/// </summary>
	public string ProcessorName { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the serialized position (base64).
	/// </summary>
	public string PositionData { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets when this state was last updated.
	/// </summary>
	public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

	/// <summary>
	/// Gets or sets the total number of events processed.
	/// </summary>
	public long EventCount { get; set; }
}
