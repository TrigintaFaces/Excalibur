// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Data.IdentityMap;

/// <summary>
/// Provides identity resolution between external system identifiers and internal aggregate IDs.
/// </summary>
/// <remarks>
/// <para>
/// Used during CDC ingestion, API anti-corruption layers, and import pipelines to maintain
/// stable mappings between foreign keys and domain aggregate identifiers.
/// </para>
/// <para>
/// All identifiers are stored as strings at the persistence layer, following the framework
/// convention where aggregate roots normalize typed keys to strings. Use the typed extension
/// methods in <see cref="IdentityMapStoreExtensions"/> for compile-time safety.
/// </para>
/// </remarks>
public interface IIdentityMapStore
{
	/// <summary>
	/// Resolves an external identifier to its mapped aggregate ID.
	/// </summary>
	/// <param name="externalSystem">The external system name (e.g., "LegacyCore", "SAP").</param>
	/// <param name="externalId">The external identifier value.</param>
	/// <param name="aggregateType">The target aggregate type name (e.g., "Order", "Account").</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The mapped aggregate ID, or <see langword="null"/> if no mapping exists.</returns>
	ValueTask<string?> ResolveAsync(
		string externalSystem,
		string externalId,
		string aggregateType,
		CancellationToken cancellationToken);

	/// <summary>
	/// Creates or updates a mapping between an external identifier and an aggregate ID.
	/// </summary>
	/// <param name="externalSystem">The external system name.</param>
	/// <param name="externalId">The external identifier value.</param>
	/// <param name="aggregateType">The aggregate type name.</param>
	/// <param name="aggregateId">The internal aggregate ID to map to.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	ValueTask BindAsync(
		string externalSystem,
		string externalId,
		string aggregateType,
		string aggregateId,
		CancellationToken cancellationToken);

	/// <summary>
	/// Creates a mapping only if no mapping exists for the external identifier.
	/// Returns the existing aggregate ID if one is already bound.
	/// </summary>
	/// <param name="externalSystem">The external system name.</param>
	/// <param name="externalId">The external identifier value.</param>
	/// <param name="aggregateType">The aggregate type name.</param>
	/// <param name="aggregateId">The aggregate ID to bind if no mapping exists.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>
	/// A result containing the aggregate ID (either newly bound or existing)
	/// and whether the binding was newly created.
	/// </returns>
	ValueTask<IdentityBindResult> TryBindAsync(
		string externalSystem,
		string externalId,
		string aggregateType,
		string aggregateId,
		CancellationToken cancellationToken);

	/// <summary>
	/// Removes a mapping for an external identifier.
	/// </summary>
	/// <param name="externalSystem">The external system name.</param>
	/// <param name="externalId">The external identifier value.</param>
	/// <param name="aggregateType">The aggregate type name.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns><see langword="true"/> if a mapping was removed; <see langword="false"/> if no mapping existed.</returns>
	ValueTask<bool> UnbindAsync(
		string externalSystem,
		string externalId,
		string aggregateType,
		CancellationToken cancellationToken);

	/// <summary>
	/// Resolves a batch of external identifiers to their mapped aggregate IDs.
	/// </summary>
	/// <param name="externalSystem">The external system name (shared by all IDs in the batch).</param>
	/// <param name="externalIds">The external identifier values to resolve.</param>
	/// <param name="aggregateType">The target aggregate type name.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A dictionary mapping external IDs to aggregate IDs for entries that exist.</returns>
	/// <remarks>
	/// The default implementation falls back to sequential per-item <see cref="ResolveAsync"/> calls.
	/// Override in stores that support efficient batch lookups (e.g., SQL WHERE IN clause).
	/// </remarks>
	async ValueTask<IReadOnlyDictionary<string, string>> ResolveBatchAsync(
		string externalSystem,
		IReadOnlyList<string> externalIds,
		string aggregateType,
		CancellationToken cancellationToken)
	{
		var result = new Dictionary<string, string>(externalIds.Count);

		foreach (var externalId in externalIds)
		{
			var aggregateId = await ResolveAsync(externalSystem, externalId, aggregateType, cancellationToken)
				.ConfigureAwait(false);

			if (aggregateId is not null)
			{
				result[externalId] = aggregateId;
			}
		}

		return result;
	}
}
