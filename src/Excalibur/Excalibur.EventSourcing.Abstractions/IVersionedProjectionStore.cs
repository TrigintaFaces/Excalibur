// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Abstractions;

namespace Excalibur.EventSourcing.Abstractions;

/// <summary>
/// ISP sub-interface for projection stores that support optimistic concurrency
/// via version tracking.
/// </summary>
/// <remarks>
/// <para>
/// Stores implementing this interface track an incrementing version number on each
/// projection. The version starts at 1 on first insert and increments with each
/// update, enabling consumers to detect stale reads and implement If-Match semantics.
/// </para>
/// <para>
/// This interface follows the established ISP sub-interface pattern
/// (see <see cref="IExistsProjectionStore{TProjection}"/>,
/// <see cref="ICursorProjectionStore{TProjection}"/>, etc.).
/// Consumers detect versioned store support via pattern matching:
/// </para>
/// <code>
/// if (store is IVersionedProjectionStore&lt;OrderSummary&gt; versioned)
/// {
///     var result = await versioned.GetVersionedAsync("order-123", ct);
///     if (result is not null)
///     {
///         // Modify projection...
///         await versioned.UpsertVersionedAsync("order-123", modified, result.Version, ct);
///     }
/// }
/// </code>
/// <para>
/// The projection engine itself does not use this interface during inline/async
/// processing — it is the sole writer with sequential-per-projection-ID semantics.
/// Version tracking is for <b>consumer read-path concurrency</b> (e.g., an API
/// controller reads a projection, modifies it, and writes it back with a version check).
/// </para>
/// </remarks>
/// <typeparam name="TProjection">The projection type. Must be a reference type.</typeparam>
public interface IVersionedProjectionStore<TProjection> : IProjectionStore<TProjection>
	where TProjection : class
{
	/// <summary>
	/// Gets a projection by its unique identifier along with its current version
	/// for optimistic concurrency checks.
	/// </summary>
	/// <param name="id">The projection identifier.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>
	/// A <see cref="VersionedProjection{TProjection}"/> containing the projection and its
	/// current version if found; otherwise, <c>null</c>.
	/// </returns>
	Task<VersionedProjection<TProjection>?> GetVersionedAsync(
		string id,
		CancellationToken cancellationToken);

	/// <summary>
	/// Creates or updates a projection with optimistic concurrency checking.
	/// </summary>
	/// <param name="id">The projection identifier.</param>
	/// <param name="projection">The projection to store.</param>
	/// <param name="expectedVersion">
	/// The expected current version. Pass <c>null</c> for initial insert (no concurrency check).
	/// When non-null, the store verifies the current version matches before applying the update.
	/// </param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	/// <exception cref="ConcurrencyException">
	/// Thrown when the stored version does not match <paramref name="expectedVersion"/>.
	/// </exception>
	Task UpsertVersionedAsync(
		string id,
		TProjection projection,
		long? expectedVersion,
		CancellationToken cancellationToken);
}
