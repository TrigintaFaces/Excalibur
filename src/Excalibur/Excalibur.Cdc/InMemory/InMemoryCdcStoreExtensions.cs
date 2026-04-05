// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Cdc.InMemory;

/// <summary>
/// Extension methods for <see cref="IInMemoryCdcStore"/>.
/// </summary>
public static class InMemoryCdcStoreExtensions
{
	/// <summary>Adds multiple simulated CDC changes to the store.</summary>
	public static void AddChanges(this IInMemoryCdcStore store, IEnumerable<InMemoryCdcChange> changes)
	{
		ArgumentNullException.ThrowIfNull(store);
		foreach (var change in changes)
		{
			store.AddChange(change);
		}
	}

	/// <summary>Gets all changes in the history.</summary>
	public static IReadOnlyList<InMemoryCdcChange> GetHistory(this IInMemoryCdcStore store)
	{
		ArgumentNullException.ThrowIfNull(store);
		if (store is IInMemoryCdcStoreHistory history)
		{
			return history.GetHistory();
		}

		return [];
	}
}
