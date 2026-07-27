// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Collections.ObjectModel;

using Excalibur.Dispatch;

namespace Excalibur.EventSourcing.Projections;

/// <summary>
/// In-memory implementation of <see cref="ICursorMapStore"/> for development and testing.
/// </summary>
/// <remarks>
/// <para>
/// Cursor maps are stored in a <see cref="ConcurrentDictionary{TKey, TValue}"/>
/// and are lost when the process restarts. For production use, use a durable
/// implementation such as <c>SqlServerCursorMapStore</c> or <c>PostgresCursorMapStore</c>.
/// </para>
/// </remarks>
internal sealed class InMemoryCursorMapStore : ICursorMapStore
{
	private readonly ConcurrentDictionary<string, ReadOnlyDictionary<string, long>> _cursors = new(StringComparer.Ordinal);
	private readonly ITenantContext? _tenantContext;

	/// <summary>
	/// Initializes a new instance of the <see cref="InMemoryCursorMapStore"/> class.
	/// </summary>
	/// <param name="tenantContext">
	/// The ambient tenant context, or <see langword="null"/> when multi-tenancy is not registered. Cursor
	/// maps are partitioned by the tenant this resolves — never by a tenant named by the caller.
	/// </param>
	public InMemoryCursorMapStore(ITenantContext? tenantContext = null) => _tenantContext = tenantContext;

	/// <summary>
	/// Resolves the partition every cursor map is confined to.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The cursor map was keyed on the projection NAME alone, so two tenants running the same projection
	/// shared one cursor. The failure is silent and one-directional in the worst way: a cursor advanced by
	/// another tenant makes this tenant's projector skip events it never processed — data missing from a
	/// read model permanently, with no error to alert on. (A cursor moved backwards merely reprojects,
	/// which an idempotent projection absorbs.)
	/// </para>
	/// <para>
	/// The tenant is AMBIENT and resolved here, never accepted as a parameter. A <c>tenantId</c> argument
	/// would let a caller widen the read by omitting it or redirect it by naming another tenant — the
	/// authorisation hole the compliance stores closed by refusing to consult such a field at all.
	/// </para>
	/// </remarks>
	/// <returns>The reserved partition key for the ambient tenant.</returns>
	private string ResolveTenantKey() =>
		KeyedTenantPartition.FromScope(TenantScope.FromContext(_tenantContext)).TenantId;

	/// <summary>
	/// The tenant/projection key separator: ASCII UNIT SEPARATOR (U+001F).
	/// </summary>
	/// <remarks>
	/// Written as an escape rather than a literal control character so it is visible in review and cannot
	/// be mangled by an editor or an encoding round-trip. A control character is used deliberately: any
	/// printable delimiter could legally occur inside a tenant identifier or a projection name, and then
	/// ("a", "b-c") and ("a-b", "c") would collapse to the SAME key -- a cross-tenant collision
	/// reintroduced by the very code meant to prevent one.
	/// </remarks>
	private const string PartitionSeparator = "\u001F";

	/// <summary>Builds the partition-qualified key.</summary>
	/// <param name="projectionName">The projection whose cursor map is addressed.</param>
	/// <returns>The composite key, scoped to the ambient tenant.</returns>
	private string KeyFor(string projectionName) =>
		string.Concat(ResolveTenantKey(), PartitionSeparator, projectionName);

	/// <inheritdoc />
	public Task<IReadOnlyDictionary<string, long>> GetCursorMapAsync(
		string projectionName,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrEmpty(projectionName);

		IReadOnlyDictionary<string, long> result = _cursors.TryGetValue(KeyFor(projectionName), out var map)
			? map
			: ReadOnlyDictionary<string, long>.Empty;

		return Task.FromResult(result);
	}

	/// <inheritdoc />
	public Task SaveCursorMapAsync(
		string projectionName,
		IReadOnlyDictionary<string, long> cursorMap,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrEmpty(projectionName);
		ArgumentNullException.ThrowIfNull(cursorMap);

		// Atomic replace: store an immutable snapshot
		_cursors[KeyFor(projectionName)] = new ReadOnlyDictionary<string, long>(
			new Dictionary<string, long>(cursorMap));

		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public Task ResetCursorMapAsync(
		string projectionName,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrEmpty(projectionName);

		_ = _cursors.TryRemove(KeyFor(projectionName), out _);

		return Task.CompletedTask;
	}
}
