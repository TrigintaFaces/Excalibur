// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Cdc.Postgres;

/// <summary>
/// Defines the contract for managing Postgres CDC position state.
/// </summary>
public interface IPostgresCdcStateStore : ICdcStateStore, IAsyncDisposable, IDisposable
{
	/// <summary>Gets the last processed LSN position for a processor.</summary>
	Task<PostgresCdcPosition> GetLastPositionAsync(string processorId, string slotName, CancellationToken cancellationToken);

	/// <summary>Saves the current LSN position for a processor.</summary>
	Task SavePositionAsync(string processorId, string slotName, PostgresCdcPosition position, CancellationToken cancellationToken);

	/// <summary>Gets detailed state information for all tracked tables.</summary>
	Task<IReadOnlyList<PostgresCdcStateEntry>> GetAllStatesAsync(string processorId, CancellationToken cancellationToken);

	/// <summary>Saves detailed state for a specific table.</summary>
	Task SaveStateAsync(PostgresCdcStateEntry entry, CancellationToken cancellationToken);

	/// <summary>Clears all state for a processor.</summary>
	Task ClearStateAsync(string processorId, CancellationToken cancellationToken);
}

/// <summary>
/// Represents a CDC state entry for position tracking.
/// </summary>
public sealed class PostgresCdcStateEntry
{
	/// <summary>Gets or sets the processor identifier.</summary>
	public string ProcessorId { get; set; } = string.Empty;

	/// <summary>Gets or sets the replication slot name.</summary>
	public string SlotName { get; set; } = string.Empty;

	/// <summary>Gets or sets the table name (schema.table format).</summary>
	public string? TableName { get; set; }

	/// <summary>Gets or sets the LSN position as a string.</summary>
	public string Position { get; set; } = string.Empty;

	/// <summary>Gets or sets the last event timestamp.</summary>
	public DateTimeOffset? LastEventTime { get; set; }

	/// <summary>Gets or sets when this state was last updated.</summary>
	public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

	/// <summary>Gets or sets the total number of events processed.</summary>
	public long EventCount { get; set; }
}
