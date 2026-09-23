// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections.Immutable;

using Excalibur.Dispatch;

namespace Excalibur.A3.Authorization.Stores.InMemory;

/// <summary>
/// In-memory implementation of <see cref="IActivityGroupStore"/> backed by one immutable map.
/// </summary>
/// <remarks>
/// <para>
/// Intended for development, testing, and standalone scenarios where no persistent store
/// (SQL Server, etc.) is configured. Registered as a singleton fallback via
/// <c>TryAddSingleton</c> in <c>AddExcaliburA3()</c>.
/// </para>
/// <para>
/// Every key on this store carries the tenant, composed through <see cref="SegmentedKey"/> so that no
/// two (tenant, name) pairs can produce the same key. A group name is unique only WITHIN a tenant:
/// keying on the bare name let two tenants' groups of the same name collide, and
/// <see cref="FindActivityGroupsAsync"/> then returned one fused group carrying both tenants'
/// activities, which an authorization decision reads as a grant.
/// </para>
/// <para>
/// <see cref="FindActivityGroupsAsync"/> therefore returns groups keyed by the COMPOSED
/// (tenant, name) key, not by the bare name. A caller looks a group up by composing the same pair.
/// </para>
/// <para>
/// <b>Every read observes one whole state.</b> The catalogue is an immutable map behind a single reference.
/// A reader takes the reference once and answers from that snapshot; a writer builds the next map and swaps
/// the reference atomically. So <see cref="ReplaceAllActivityGroupsAsync"/> is observed either not at all or
/// completely, which a mutable dictionary cleared and refilled in place cannot offer: a reader between the
/// clear and the refill saw an empty or partial catalogue.
/// </para>
/// </remarks>
internal sealed class InMemoryActivityGroupStore : IActivityGroupStore
{
	/// <summary>
	/// Stores activity group entries keyed by the composed (tenant, name, activity) key.
	/// </summary>
	private ImmutableDictionary<string, Row> _entries = ImmutableDictionary.Create<string, Row>(StringComparer.Ordinal);

	// Read once per operation, so every answer comes from a single state.
	private ImmutableDictionary<string, Row> Snapshot => Volatile.Read(ref _entries);

	/// <inheritdoc />
	public Task<bool> ActivityGroupExistsAsync(
		string tenantId,
		string activityGroupName,
		CancellationToken cancellationToken)
	{
		// Both terms, because a group name is unique only within a tenant: matching on the name alone
		// answers for the whole estate and reports another tenant's group as this tenant's.
		var exists = Snapshot.Values
			.Any(e => string.Equals(e.TenantId, tenantId, StringComparison.Ordinal)
				&& string.Equals(e.Name, activityGroupName, StringComparison.Ordinal));

		return Task.FromResult(exists);
	}

	/// <inheritdoc />
	public Task<IReadOnlyDictionary<string, IReadOnlyCollection<string>>> FindActivityGroupsAsync(
		string tenantId,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);

		// Composed with the tenant, so two tenants' groups of the same name are two entries rather than
		// one fused entry. Grouping on the bare name merged them, and the merged value satisfied the
		// decision path's membership test for BOTH tenants.
		// The tenant term SELECTS, and the composed key still IDENTIFIES. Both are needed and they do
		// different jobs: without the filter every caller receives every tenant's catalogue and can read
		// it, and without the composition two tenants' same-named groups fuse into one entry whose
		// membership satisfies both. Returning bare names here would re-open the escalation this store
		// was changed to close.
		var groups = Snapshot.Values
			.Where(e => string.Equals(e.TenantId, tenantId, StringComparison.Ordinal))
			.GroupBy(e => SegmentedKey.Compose(e.TenantId, e.Name), StringComparer.Ordinal)
			.ToDictionary(
				g => g.Key,
				IReadOnlyCollection<string> (g) => g.Select(e => e.ActivityName).ToList());

		return Task.FromResult<IReadOnlyDictionary<string, IReadOnlyCollection<string>>>(groups);
	}

	/// <inheritdoc />
	public Task<int> DeleteActivityGroupsForTenantAsync(string tenantId, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);

		// Matched on the entry's tenant rather than on a key prefix. The key is composed, so a prefix test
		// would appear to work and would also match a DIFFERENT tenant whose identifier merely begins with
		// these characters -- "acme" would take "acme-corp" with it. The escaped segment is the identity;
		// the string it renders to is not.
		var removed = 0;

		_ = ImmutableInterlocked.Update(
			ref _entries,
			current =>
			{
				var doomed = current
					.Where(e => string.Equals(e.Value.TenantId, tenantId, StringComparison.Ordinal))
					.Select(e => e.Key)
					.ToArray();

				// Assigned on every attempt, so the count reported is the one from the state actually swapped in.
				removed = doomed.Length;

				return current.RemoveRange(doomed);
			});

		return Task.FromResult(removed);
	}

	/// <inheritdoc />
	public Task<IReadOnlyCollection<string>> ReplaceAllActivityGroupsAsync(
		ActivityGroupCatalogue catalogue,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(catalogue);

		var next = catalogue.Entries
			.ToImmutableDictionary(
				static e => BuildKey(e.TenantId, e.Name, e.ActivityName),
				static e => new Row(e.TenantId, e.Name, e.ActivityName),
				StringComparer.Ordinal);

		// The exchange returns exactly the state it replaced, so the previous tenants are the ones this replace
		// removed, and no write can fall between observing them and replacing them.
		var previous = Interlocked.Exchange(ref _entries, next);

		return Task.FromResult<IReadOnlyCollection<string>>(
			[.. previous.Values.Select(static e => e.TenantId).Distinct(StringComparer.Ordinal)]);
	}

	/// <inheritdoc />
	public Task<int> CreateActivityGroupAsync(
		string tenantId,
		string name,
		string activityName,
		CancellationToken cancellationToken)
	{
		// The interface states this parameter is "Required; must be non-empty", and the tenant is now part
		// of the key rather than a field beside it, so the precondition is enforced here instead of being
		// documented and ignored. Accepting a blank tenant wrote an entry no tenant-scoped read can
		// address, and the untenanted partition is a declared VALUE, never an absence.
		ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);

		var key = BuildKey(tenantId, name, activityName);
		var row = new Row(tenantId, name, activityName);

		// Adds only when absent (no overwrite), reporting whether this call added it.
		var added = false;

		_ = ImmutableInterlocked.Update(
			ref _entries,
			current =>
			{
				added = !current.ContainsKey(key);

				return added ? current.Add(key, row) : current;
			});

		return Task.FromResult(added ? 1 : 0);
	}

	// The tenant is part of the identity, not a field beside it: without it, two tenants registering the
	// same group and activity collided and TryAdd silently reported 0 rows for the second.
	private static string BuildKey(string tenantId, string name, string activityName) =>
		SegmentedKey.Compose(tenantId, name, activityName);

	/// <summary>
	/// Internal record representing a single activity-group-to-activity mapping.
	/// </summary>
	private sealed record Row(string TenantId, string Name, string ActivityName);
}
