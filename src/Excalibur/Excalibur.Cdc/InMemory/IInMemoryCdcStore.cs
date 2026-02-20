// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Cdc.InMemory;

/// <summary>
/// Defines the contract for an in-memory CDC change store.
/// </summary>
/// <remarks>
/// <para>
/// This interface provides methods to add, retrieve, and manage CDC changes
/// in memory for testing scenarios.
/// </para>
/// </remarks>
public interface IInMemoryCdcStore
{
	/// <summary>
	/// Adds a simulated CDC change to the store.
	/// </summary>
	/// <param name="change">The change to add.</param>
	void AddChange(InMemoryCdcChange change);

	/// <summary>
	/// Adds multiple simulated CDC changes to the store.
	/// </summary>
	/// <param name="changes">The changes to add.</param>
	void AddChanges(IEnumerable<InMemoryCdcChange> changes);

	/// <summary>
	/// Gets all pending changes that have not been processed.
	/// </summary>
	/// <param name="maxCount">The maximum number of changes to retrieve.</param>
	/// <returns>A collection of pending changes.</returns>
	IReadOnlyList<InMemoryCdcChange> GetPendingChanges(int maxCount);

	/// <summary>
	/// Marks the specified changes as processed.
	/// </summary>
	/// <param name="changes">The changes to mark as processed.</param>
	void MarkAsProcessed(IEnumerable<InMemoryCdcChange> changes);

	/// <summary>
	/// Clears all changes from the store.
	/// </summary>
	void Clear();

	/// <summary>
	/// Gets the total count of pending changes.
	/// </summary>
	/// <returns>The number of pending changes.</returns>
	int GetPendingCount();

	/// <summary>
	/// Gets all changes in the history (if history preservation is enabled).
	/// </summary>
	/// <returns>A collection of all processed changes.</returns>
	IReadOnlyList<InMemoryCdcChange> GetHistory();
}
