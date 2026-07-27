// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.EventSourcing.Views;

/// <summary>
/// The single definition of what it means for a materialized view store to be usable by an exactly-once
/// projection, and of what to tell an operator when it is not.
/// </summary>
/// <remarks>
/// The requirement is enforced in two places — at host startup, so a misconfigured deployment never begins
/// serving, and in <see cref="MaterializedViewProcessor"/>'s constructor, so the write path cannot be reached
/// with a store that would degrade to at-least-once. Both call this, rather than restating the rule, because
/// two copies of a guarantee drift into one copy of a guarantee.
/// </remarks>
internal static class AtomicViewStoreRequirement
{
	/// <summary>
	/// Throws when <paramref name="viewStore"/> cannot persist a view and its checkpoint atomically.
	/// </summary>
	/// <param name="viewStore">The store to check.</param>
	/// <returns>The store, narrowed to <see cref="IAtomicMaterializedViewStore"/>.</returns>
	/// <exception cref="InvalidOperationException">
	/// The store does not implement <see cref="IAtomicMaterializedViewStore"/>, or implements it but has
	/// atomic writes disabled by configuration.
	/// </exception>
	public static IAtomicMaterializedViewStore Require(IMaterializedViewStore viewStore)
	{
		ArgumentNullException.ThrowIfNull(viewStore);

		if (viewStore is not IAtomicMaterializedViewStore atomicStore)
		{
			throw new InvalidOperationException(
				$"The materialized view store '{viewStore.GetType().Name}' cannot persist a view and its "
				+ $"checkpoint as one atomic operation, so it cannot back an exactly-once projection: a crash "
				+ $"between the two writes would replay the last event and double-count an accumulating view. "
				+ $"Elasticsearch and OpenSearch have no cross-index transaction and cannot provide this "
				+ $"guarantee. For an exactly-once projection, register an atomic store — MongoDB with "
				+ $"UseTransactions enabled, SQL Server, or PostgreSQL. Alternatively, run only an at-least-once "
				+ $"idempotent (upsert-by-key) projection, which tolerates the replay and does not require atomic "
				+ $"checkpointing.");
		}

		// Read the option that governs the behaviour, never a proxy for it: a store may be capable of atomic
		// writes and still have them switched off, and that deployment must not start.
		if (!atomicStore.SupportsAtomicWrites)
		{
			throw new InvalidOperationException(
				$"The materialized view store '{viewStore.GetType().Name}' supports atomic view and checkpoint "
				+ $"writes, but they are disabled by its current configuration, so a crash would replay the "
				+ $"last event and double-count the view. Enable atomic writes on the store's options (for "
				+ $"MongoDB, set UseTransactions = true on MongoDbMaterializedViewStoreOptions), or register a "
				+ $"store whose atomic writes are unconditional (SQL Server or PostgreSQL).");
		}

		return atomicStore;
	}
}
