// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.EventSourcing.Abstractions;

/// <summary>
/// Wraps a projection with its current version for optimistic concurrency.
/// </summary>
/// <typeparam name="TProjection">The projection type.</typeparam>
/// <remarks>
/// <para>
/// Use this type with <see cref="IVersionedProjectionStore{TProjection}.GetVersionedAsync"/>
/// to read a projection along with its version, then pass the version to
/// <see cref="IVersionedProjectionStore{TProjection}.UpsertVersionedAsync"/> for
/// optimistic concurrency checks.
/// </para>
/// <code>
/// var result = await store.GetVersionedAsync("order-123", ct);
/// if (result is not null)
/// {
///     // Modify the projection...
///     await store.UpsertVersionedAsync("order-123", result.Projection, result.Version, ct);
/// }
/// </code>
/// </remarks>
public sealed class VersionedProjection<TProjection>(TProjection projection, long version)
{
	/// <summary>
	/// Gets the projection instance.
	/// </summary>
	/// <value>The projection state.</value>
	public TProjection Projection { get; } = projection;

	/// <summary>
	/// Gets the current version of the projection. Starts at 1 for the first insert
	/// and increments with each subsequent update.
	/// </summary>
	/// <value>The current version number.</value>
	public long Version { get; } = version;
}
