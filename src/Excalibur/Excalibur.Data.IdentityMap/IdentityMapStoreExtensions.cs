// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Data.IdentityMap;

/// <summary>
/// Typed extension methods for <see cref="IIdentityMapStore"/> providing compile-time safety
/// for aggregate ID types.
/// </summary>
public static class IdentityMapStoreExtensions
{
	/// <summary>
	/// Resolves an external identifier to a typed aggregate ID.
	/// </summary>
	/// <typeparam name="TKey">The aggregate ID type (e.g., <see cref="Guid"/>, <see cref="int"/>, <see cref="long"/>).</typeparam>
	/// <param name="store">The identity map store.</param>
	/// <param name="externalSystem">The external system name.</param>
	/// <param name="externalId">The external identifier value.</param>
	/// <param name="aggregateType">The target aggregate type name.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The typed aggregate ID, or <see langword="null"/> if no mapping exists.</returns>
	public static async ValueTask<TKey?> ResolveAsync<TKey>(
		this IIdentityMapStore store,
		string externalSystem,
		string externalId,
		string aggregateType,
		CancellationToken cancellationToken)
		where TKey : struct, IParsable<TKey>
	{
		ArgumentNullException.ThrowIfNull(store);

		var result = await store.ResolveAsync(externalSystem, externalId, aggregateType, cancellationToken)
			.ConfigureAwait(false);

		if (result is null)
		{
			return null;
		}

		return TKey.Parse(result, null);
	}

	/// <summary>
	/// Creates or updates a mapping between an external identifier and a typed aggregate ID.
	/// </summary>
	/// <typeparam name="TKey">The aggregate ID type.</typeparam>
	/// <param name="store">The identity map store.</param>
	/// <param name="externalSystem">The external system name.</param>
	/// <param name="externalId">The external identifier value.</param>
	/// <param name="aggregateType">The aggregate type name.</param>
	/// <param name="aggregateId">The internal aggregate ID to map to.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	public static ValueTask BindAsync<TKey>(
		this IIdentityMapStore store,
		string externalSystem,
		string externalId,
		string aggregateType,
		TKey aggregateId,
		CancellationToken cancellationToken)
		where TKey : notnull
	{
		ArgumentNullException.ThrowIfNull(store);

		return store.BindAsync(
			externalSystem,
			externalId,
			aggregateType,
			aggregateId.ToString()!,
			cancellationToken);
	}

	/// <summary>
	/// Creates a mapping only if no mapping exists, using a typed aggregate ID.
	/// </summary>
	/// <typeparam name="TKey">The aggregate ID type.</typeparam>
	/// <param name="store">The identity map store.</param>
	/// <param name="externalSystem">The external system name.</param>
	/// <param name="externalId">The external identifier value.</param>
	/// <param name="aggregateType">The aggregate type name.</param>
	/// <param name="aggregateId">The aggregate ID to bind if no mapping exists.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>
	/// A result containing the aggregate ID (either newly bound or existing)
	/// and whether the binding was newly created.
	/// </returns>
	public static ValueTask<IdentityBindResult> TryBindAsync<TKey>(
		this IIdentityMapStore store,
		string externalSystem,
		string externalId,
		string aggregateType,
		TKey aggregateId,
		CancellationToken cancellationToken)
		where TKey : notnull
	{
		ArgumentNullException.ThrowIfNull(store);

		return store.TryBindAsync(
			externalSystem,
			externalId,
			aggregateType,
			aggregateId.ToString()!,
			cancellationToken);
	}
}
