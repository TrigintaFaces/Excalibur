// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections.Immutable;

using Excalibur.Dispatch;

namespace Excalibur.A3.Authorization.Stores.InMemory;

/// <summary>
/// In-memory implementation of <see cref="IGrantStore"/>, <see cref="IGrantQueryStore"/>,
/// <see cref="IActivityGroupGrantStore"/> and <see cref="IActivityGroupGrantReplacement"/> backed by one
/// immutable map.
/// </summary>
/// <remarks>
/// <para>
/// Intended for development, testing, and standalone scenarios where no persistent store
/// (SQL Server, etc.) is configured. Registered as a singleton fallback via
/// <c>TryAddSingleton</c> in <c>AddExcaliburA3()</c>.
/// </para>
/// <para>
/// <b>Every read observes one whole state.</b> The grants are an immutable map behind a single reference. A
/// reader takes the reference once and answers from that snapshot; a writer builds the next map and swaps the
/// reference atomically, retrying if another writer got there first. So
/// <see cref="ReplaceActivityGroupGrantsAsync"/> removes a set of grants and writes another in one step, which
/// a dictionary emptied and refilled in place cannot offer: a reader between the two saw a partial set, and
/// the authorization decision denies on a missing grant.
/// </para>
/// </remarks>
internal sealed class InMemoryGrantStore
	: IGrantStore, IGrantQueryStore, IActivityGroupGrantStore, IActivityGroupGrantReplacement
{
	private readonly TimeProvider _timeProvider;

	private ImmutableDictionary<string, Grant> _grants =
		ImmutableDictionary.Create<string, Grant>(StringComparer.Ordinal);

	/// <summary>
	/// Initializes a new instance of the <see cref="InMemoryGrantStore"/> class.
	/// </summary>
	/// <param name="timeProvider">
	/// Time source used to evaluate grant expiry. Defaults to <see cref="TimeProvider.System"/>
	/// when not supplied; tests may pass a fake provider for deterministic expiry evaluation.
	/// </param>
	public InMemoryGrantStore(TimeProvider? timeProvider = null) =>
		_timeProvider = timeProvider ?? TimeProvider.System;

	// Read once per operation, so every answer comes from a single state.
	private ImmutableDictionary<string, Grant> Snapshot => Volatile.Read(ref _grants);

	/// <inheritdoc />
	public Task<Grant?> GetGrantAsync(
		string userId,
		string tenantId,
		string grantType,
		string qualifier,
		CancellationToken cancellationToken)
	{
		var key = BuildKey(userId, tenantId, grantType, qualifier);
		_ = Snapshot.TryGetValue(key, out var grant);
		return Task.FromResult(grant);
	}

	/// <inheritdoc />
	public Task<IReadOnlyList<Grant>> GetAllGrantsAsync(
		string userId,
		CancellationToken cancellationToken) =>
		GetAllGrantsAsync(userId, includeExpired: false, cancellationToken);

	/// <inheritdoc />
	public Task<IReadOnlyList<Grant>> GetAllGrantsAsync(
		string userId,
		bool includeExpired,
		CancellationToken cancellationToken)
	{
		var now = _timeProvider.GetUtcNow();

		var results = Snapshot.Values
			.Where(g => string.Equals(g.UserId, userId, StringComparison.Ordinal))
			.Where(g => includeExpired || g.IsActive(now))
			.ToList();

		return Task.FromResult<IReadOnlyList<Grant>>(results);
	}

	/// <inheritdoc />
	public Task<int> SaveGrantAsync(Grant grant, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(grant);

		var key = BuildKey(grant.UserId, grant.TenantId, grant.GrantType, grant.Qualifier);
		_ = ImmutableInterlocked.AddOrUpdate(ref _grants, key, grant, (_, _) => grant);
		return Task.FromResult(1);
	}

	/// <inheritdoc />
	public Task<int> DeleteGrantAsync(
		string userId,
		string tenantId,
		string grantType,
		string qualifier,
		string? revokedBy,
		DateTimeOffset? revokedOn,
		CancellationToken cancellationToken)
	{
		var key = BuildKey(userId, tenantId, grantType, qualifier);
		return Task.FromResult(ImmutableInterlocked.TryRemove(ref _grants, key, out _) ? 1 : 0);
	}

	/// <inheritdoc />
	public Task<bool> GrantExistsAsync(
		string userId,
		string tenantId,
		string grantType,
		string qualifier,
		CancellationToken cancellationToken)
	{
		var key = BuildKey(userId, tenantId, grantType, qualifier);
		return Task.FromResult(Snapshot.ContainsKey(key));
	}

	/// <inheritdoc />
	public object? GetService(Type serviceType)
	{
		ArgumentNullException.ThrowIfNull(serviceType);

		if (serviceType == typeof(IGrantQueryStore))
		{
			return this;
		}

		if (serviceType == typeof(IActivityGroupGrantStore))
		{
			return this;
		}

		if (serviceType == typeof(IActivityGroupGrantReplacement))
		{
			return this;
		}

		return null;
	}

	// -- IGrantQueryStore --

	/// <inheritdoc />
	public Task<IReadOnlyList<Grant>> GetMatchingGrantsAsync(
		string tenantId,
		string? userId,
		string? grantType,
		string? qualifier,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
		ThrowIfEmptyFilter(userId, grantType, qualifier);

		return Task.FromResult<IReadOnlyList<Grant>>(Match(tenantId, userId, grantType, qualifier));
	}

	/// <inheritdoc />
	public Task<IReadOnlyList<Grant>> GetMatchingGrantsAcrossTenantsAsync(
		string? userId,
		string? grantType,
		string? qualifier,
		CancellationToken cancellationToken)
	{
		ThrowIfEmptyFilter(userId, grantType, qualifier);

		return Task.FromResult<IReadOnlyList<Grant>>(Match(tenantId: null, userId, grantType, qualifier));
	}

	/// <inheritdoc />
	public Task<IReadOnlyDictionary<string, object>> FindUserGrantsAsync(
		string userId,
		CancellationToken cancellationToken)
	{
		var results = Snapshot.Values
			.Where(g => string.Equals(g.UserId, userId, StringComparison.Ordinal))
			.ToDictionary(
				g => BuildScopeKey(g.TenantId, g.GrantType, g.Qualifier),
				g => (object)g,
				StringComparer.Ordinal);

		return Task.FromResult<IReadOnlyDictionary<string, object>>(results);
	}

	// -- IActivityGroupGrantStore --

	/// <inheritdoc />
	public Task<int> DeleteActivityGroupGrantsByUserIdAsync(
		string userId,
		string grantType,
		CancellationToken cancellationToken) =>
		Task.FromResult(RemoveWhere(g =>
			string.Equals(g.UserId, userId, StringComparison.Ordinal)
			&& string.Equals(g.GrantType, grantType, StringComparison.Ordinal)).Count);

	/// <inheritdoc />
	public Task<int> DeleteAllActivityGroupGrantsAsync(
		string grantType,
		CancellationToken cancellationToken) =>
		Task.FromResult(RemoveWhere(g =>
			string.Equals(g.GrantType, grantType, StringComparison.Ordinal)).Count);

	/// <inheritdoc />
	public Task<int> InsertActivityGroupGrantAsync(
		string userId,
		string fullName,
		string tenantId,
		string grantType,
		string qualifier,
		DateTimeOffset? expiresOn,
		string grantedBy,
		CancellationToken cancellationToken)
	{
		var grant = new Grant(
			userId,
			fullName,
			tenantId,
			grantType,
			qualifier,
			expiresOn,
			grantedBy,
			DateTimeOffset.UtcNow);

		var key = BuildKey(userId, tenantId, grantType, qualifier);
		_ = ImmutableInterlocked.AddOrUpdate(ref _grants, key, grant, (_, _) => grant);
		return Task.FromResult(1);
	}

	/// <inheritdoc />
	public Task<IReadOnlyList<string>> GetDistinctActivityGroupGrantUserIdsAsync(
		string grantType,
		CancellationToken cancellationToken)
	{
		var userIds = Snapshot.Values
			.Where(g => string.Equals(g.GrantType, grantType, StringComparison.Ordinal))
			.Select(g => g.UserId)
			.Distinct(StringComparer.Ordinal)
			.ToList();

		return Task.FromResult<IReadOnlyList<string>>(userIds);
	}

	// -- IActivityGroupGrantReplacement --

	/// <inheritdoc />
	public Task<IReadOnlyCollection<string>> ReplaceActivityGroupGrantsAsync(
		string grantType,
		ActivityGroupGrantSnapshot snapshot,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(snapshot);

		// Refused before anything is removed: an empty estate-wide snapshot, or one mixing grant types, cannot
		// be applied whole, so it is not applied at all.
		ActivityGroupGrantSnapshot.ValidateAsEstateReplacement(snapshot, grantType);

		return Task.FromResult(Replace(
			g => string.Equals(g.GrantType, grantType, StringComparison.Ordinal),
			snapshot));
	}

	/// <inheritdoc />
	public Task<IReadOnlyCollection<string>> ReplaceActivityGroupGrantsForUserAsync(
		string userId,
		string grantType,
		ActivityGroupGrantSnapshot snapshot,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(snapshot);

		// An EMPTY snapshot is accepted here and is the whole point of the per-user member: that user now holds
		// no grants of this type, and every one of theirs is revoked.
		ActivityGroupGrantSnapshot.ValidateAsUserReplacement(snapshot, userId, grantType);

		return Task.FromResult(Replace(
			g => string.Equals(g.UserId, userId, StringComparison.Ordinal)
				&& string.Equals(g.GrantType, grantType, StringComparison.Ordinal),
			snapshot));
	}

	/// <summary>
	/// Removes every grant matching <paramref name="scope"/> and writes <paramref name="snapshot"/> in their
	/// place, in one exchange, reporting the users whose grants were removed.
	/// </summary>
	/// <remarks>
	/// The removal and the insertion are one transformation of one map, so a reader holding the reference sees
	/// the whole previous set or the whole new one. The previous users are assigned on every attempt, so the
	/// ones reported come from the state actually swapped in rather than from an attempt that lost the race.
	/// </remarks>
	private IReadOnlyCollection<string> Replace(Func<Grant, bool> scope, ActivityGroupGrantSnapshot snapshot)
	{
		IReadOnlyCollection<string> previousUsers = [];
		var grantedOn = _timeProvider.GetUtcNow();

		_ = ImmutableInterlocked.Update(
			ref _grants,
			current =>
			{
				var doomed = current.Where(e => scope(e.Value)).ToArray();

				previousUsers =
					[.. doomed.Select(static e => e.Value.UserId).Distinct(StringComparer.Ordinal)];

				return current
					.RemoveRange(doomed.Select(static e => e.Key))
					.SetItems(snapshot.Entries.Select(e => KeyValuePair.Create(
						BuildKey(e.UserId, e.TenantId, e.GrantType, e.Qualifier),
						new Grant(
							e.UserId,
							e.FullName,
							e.TenantId,
							e.GrantType,
							e.Qualifier,
							e.ExpiresOn,
							e.GrantedBy,
							grantedOn))));
			});

		return previousUsers;
	}

	/// <summary>
	/// Removes every grant matching <paramref name="scope"/> in one exchange, reporting what was removed.
	/// </summary>
	private IReadOnlyCollection<string> RemoveWhere(Func<Grant, bool> scope)
	{
		IReadOnlyCollection<string> removed = [];

		_ = ImmutableInterlocked.Update(
			ref _grants,
			current =>
			{
				var doomed = current.Where(e => scope(e.Value)).Select(static e => e.Key).ToArray();
				removed = doomed;

				return current.RemoveRange(doomed);
			});

		return removed;
	}

	// Escaped before joining, so a term containing the separator cannot shift the meaning of the key:
	// without it ("a:b", "c", "d") and ("a", "b:c", "d") compose the same string and one tenant reads
	// another tenant's grant. This store is in-memory, so the composition is never persisted.
	private static string BuildKey(string userId, string tenantId, string grantType, string qualifier) =>
		SegmentedKey.Compose(userId, tenantId, grantType, qualifier);

	private static string BuildScopeKey(string tenantId, string grantType, string qualifier) =>
		SegmentedKey.Compose(tenantId, grantType, qualifier);

	// A revoked grant is removed, so every held grant is non-revoked; the filters are ordinal equality, and a
	// null filter -- never an empty one -- leaves its field unconstrained.
	private List<Grant> Match(string? tenantId, string? userId, string? grantType, string? qualifier) =>
		Snapshot.Values
			.Where(g =>
				(tenantId is null || string.Equals(g.TenantId, tenantId, StringComparison.Ordinal)) &&
				(userId is null || string.Equals(g.UserId, userId, StringComparison.Ordinal)) &&
				(grantType is null || string.Equals(g.GrantType, grantType, StringComparison.Ordinal)) &&
				(qualifier is null || string.Equals(g.Qualifier, qualifier, StringComparison.Ordinal)))
			.ToList();

	private static void ThrowIfEmptyFilter(string? userId, string? grantType, string? qualifier)
	{
		if (userId is not null)
		{
			ArgumentException.ThrowIfNullOrWhiteSpace(userId);
		}

		if (grantType is not null)
		{
			ArgumentException.ThrowIfNullOrWhiteSpace(grantType);
		}

		if (qualifier is not null)
		{
			ArgumentException.ThrowIfNullOrWhiteSpace(qualifier);
		}
	}
}
