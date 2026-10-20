// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using System.Text.Json;

using Excalibur.A3.Authentication;
using Excalibur.A3.Authorization.PolicyData;
using Excalibur.Dispatch;

using Excalibur.Dispatch.Caching;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.A3.Authorization;

/// <summary>
/// Provides an implementation for an authorization policy provider.
/// </summary>
/// <remarks>
/// The provider sets up policy data for authorization using activities, activity groups, and grants for a given user,
/// then evaluates them via pure C# grant checking.
/// </remarks>
/// <param name="activityGroups"> A collection of activity groups. </param>
/// <param name="userGrants"> A collection of grants for authorization purposes. </param>
/// <param name="currentUser"> The current authenticated user token. </param>
/// <param name="cache"> Distributed cache used for caching authorization data. </param>
/// <param name="timeProvider">Time source for checking grant expiration on each acquisition.</param>
	/// <param name="tenantContext">
	/// The ambient tenant context. Required: this store partitions rows by tenant, and it resolves that
	/// partition from here, so there is no state in which the partition is undecided. A single-tenant host
	/// receives the framework default context and operates as the one canonical tenant.
	/// </param>
/// <param name="cacheOptions">
/// Bounds how long a cached grant or activity-group catalogue is served; see
/// <see cref="AuthorizationCacheOptions.AbsoluteExpirationRelativeToNow"/>.
/// </param>
internal sealed class AuthorizationPolicyProvider(
	ActivityGroups activityGroups,
	UserGrants userGrants,
	IAuthenticationToken currentUser,
	ITenantContext tenantContext,
	IOptions<AuthorizationCacheOptions> cacheOptions,
	[FromKeyedServices(DistributedCacheServiceKeys.ApplicationScoped)] IDistributedCache? cache = null,
	TimeProvider? timeProvider = null
) : IAuthorizationPolicyProvider
{
	private readonly IDistributedCache _cache = AuthorizationCachePrerequisiteValidator.RequireCache(cache);
	private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;


	/// <inheritdoc />
	/// <exception cref="InvalidOperationException">
	/// Thrown when <see cref="IAuthenticationToken.UserId"/> is null or
	/// <see cref="ITenantContext.TenantId"/> is null or empty.
	/// </exception>
	/// <remarks>
	/// Kept <c>async</c> deliberately, even though the body has no <c>await</c> of its own: a validation
	/// failure must reach the caller as a FAULTED TASK, the normal TAP contract for a Task-returning
	/// method, not as a synchronous throw out of the method call itself. A non-async method that throws
	/// before constructing its Task changes that contract silently for any caller who calls first and
	/// awaits later.
	/// </remarks>
	public async Task<IAuthorizationPolicy> GetPolicyAsync()
	{
		if (currentUser.UserId is null)
		{
			throw new InvalidOperationException("User ID is required for authorization policy.");
		}

		if (string.IsNullOrEmpty(tenantContext.TenantId))
		{
			throw new InvalidOperationException(
				"Tenant ID is required for authorization policy. " +
				"Establish the ambient tenant (TenantContextHolder.BeginScope / tenant middleware) before evaluating authorization.");
		}

		// A policy is a snapshot. A new acquisition, including another call in the same scope,
		// must not reuse a snapshot from before a committed revocation.
		var userId = currentUser.UserId;
		var tenantId = tenantContext.TenantId;
		for (var attempt = 0; attempt < 3; attempt++)
		{
			var revision = await userGrants.RevisionAsync(userId, CancellationToken.None).ConfigureAwait(false);
			var policy = await BuildPolicyAsync(userId, tenantId, revision).ConfigureAwait(false);
			var finalRevision = revision is null ? null : await userGrants.RevisionAsync(userId, CancellationToken.None).ConfigureAwait(false);
			if (!string.Equals(userId, currentUser.UserId, StringComparison.Ordinal)
				|| !string.Equals(tenantId, tenantContext.TenantId, StringComparison.Ordinal))
			{
				throw new InvalidOperationException("Authorization identity changed while acquiring a policy.");
			}
			if (revision is null || revision == finalRevision)
			{
				return policy;
			}
		}

		throw new InvalidOperationException("Authorization grants changed repeatedly while acquiring a policy. Retry authorization.");
	}

	private async Task<IAuthorizationPolicy> BuildPolicyAsync(string userId, string tenantId, long? revision)
	{
		// NOTE: IPolicyProvider<T>.GetPolicyAsync() does not accept CancellationToken.
		// CancellationToken.None is used here because the interface contract does not support cancellation.
		var authData = await LoadPolicyDataAsync(userId, tenantId, revision, CancellationToken.None).ConfigureAwait(false);

		return new AuthorizationPolicy(
			authData.Grants,
			authData.ActivityGroups,
			tenantContext,
			userId);
	}

	/// <summary>
	/// Loads the policy data required for grant evaluation.
	/// </summary>
	/// <param name="userId"> The user identifier for which the policy data is being loaded. </param>
	/// <param name="tenantId"> The tenant whose activity groups the policy is built from. </param>
	/// <param name="revision">Authoritative revision captured before this acquisition.</param>
	/// <param name="cancellationToken"> A token to cancel the asynchronous operation. </param>
	/// <returns> The authorization data containing grants, activity groups, and activities. </returns>
	private async Task<AuthorizationData> LoadPolicyDataAsync(string userId, string tenantId, long? revision, CancellationToken cancellationToken)
	{
		// Relational stores can share the scoped IDomainDb connection. Complete the grant
		// snapshot transaction before issuing the catalogue query on that connection.
		var grants = await GetGrantsAsync(userId, revision, cancellationToken).ConfigureAwait(false);
		var groups = await GetActivityGroupsAsync(tenantId, cancellationToken).ConfigureAwait(false);
		return new AuthorizationData(grants, groups);
	}

	/// <summary>
	/// Retrieves grants for the specified user, using a cached value if available.
	/// </summary>
	/// <param name="userId"> The user identifier. </param>
	/// <param name="revision">Authoritative revision captured before this acquisition.</param>
	/// <param name="cancellationToken"> A token to cancel the asynchronous operation. </param>
	/// <returns> A dictionary of grants for the user. </returns>
	private async Task<IDictionary<string, object>> GetGrantsAsync(string userId, long? revision, CancellationToken cancellationToken)
	{
		if (revision is null)
		{
			// Stores without an authoritative revision cannot safely share grant snapshots in a cache.
			return await userGrants.ValueAsync(userId, cancellationToken).ConfigureAwait(false);
		}
		if (!userGrants.SupportsSharedCache)
		{
			var volatileSnapshot = await userGrants.SnapshotAsync(userId, cancellationToken).ConfigureAwait(false)
				?? throw new InvalidOperationException("The grant store lost its atomic snapshot capability.");
			if (!string.Equals(volatileSnapshot.UserId, userId, StringComparison.Ordinal))
			{
				throw new InvalidOperationException("The grant snapshot belongs to a different user.");
			}
			return ToGrantDictionary(volatileSnapshot.Grants);
		}

		var key = SegmentedKey.Compose(AuthorizationCacheKey.ForGrants(userId), "revision-v1", revision.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
		var cachedBytes = await _cache.GetAsync(key, cancellationToken).ConfigureAwait(false);
		var cached = cachedBytes is null ? null : JsonSerializer.Deserialize(cachedBytes, GrantCacheJsonContext.Default.GrantCacheDocument);
		if (cached is not null && cached.Revision == revision.Value && string.Equals(cached.UserId, userId, StringComparison.Ordinal))
		{
			return ToGrantDictionary(cached.Grants);
		}

		var snapshot = await userGrants.SnapshotAsync(userId, cancellationToken).ConfigureAwait(false)
			?? throw new InvalidOperationException("The grant store lost its atomic snapshot capability.");
		if (!string.Equals(snapshot.UserId, userId, StringComparison.Ordinal))
		{
			throw new InvalidOperationException("The grant snapshot belongs to a different user.");
		}

		// Label data with the revision of its coherent snapshot, never a separate revision read.
		key = SegmentedKey.Compose(AuthorizationCacheKey.ForGrants(userId), "revision-v1", snapshot.Revision.ToString(System.Globalization.CultureInfo.InvariantCulture));
		var bytes = JsonSerializer.SerializeToUtf8Bytes(new GrantCacheDocument(userId, snapshot.Revision, snapshot.Grants.ToArray()), GrantCacheJsonContext.Default.GrantCacheDocument);
		await _cache.SetAsync(key, bytes, new DistributedCacheEntryOptions
		{
			SlidingExpiration = TimeSpan.FromMinutes(5),
			AbsoluteExpirationRelativeToNow = cacheOptions.Value.AbsoluteExpirationRelativeToNow,
		}, cancellationToken).ConfigureAwait(false);

		return ToGrantDictionary(snapshot.Grants);
	}

	private IDictionary<string, object> ToGrantDictionary(IEnumerable<Grant> grants)
	{
		var now = _timeProvider.GetUtcNow();
		return grants.Where(g => g.IsActive(now)).ToDictionary(
			g => SegmentedKey.Compose(g.TenantId, g.GrantType, g.Qualifier), g => (object)g, StringComparer.Ordinal);
	}

	/// <summary>
	/// Retrieves one tenant's activity groups, using a cached value if available.
	/// </summary>
	/// <param name="tenantId"> The tenant whose activity groups to read. </param>
	/// <param name="cancellationToken"> A token to cancel the asynchronous operation. </param>
	/// <returns> A dictionary of that tenant's activity groups. </returns>
	/// <remarks>
	/// The cache entry is addressed per tenant, because the document it holds is one tenant's catalogue.
	/// Sharing one entry across tenants would serve a document composed for one tenant to all of them, and
	/// a tenant finds none of its own groups under another's composed keys — so every activity-group grant
	/// would be denied, silently.
	/// </remarks>
	private async Task<IReadOnlyDictionary<string, IReadOnlyCollection<string>>> GetActivityGroupsAsync(
		string tenantId,
		CancellationToken cancellationToken)
	{
		var key = AuthorizationCacheKey.ForActivityGroups(tenantId);
		if (!userGrants.SupportsSharedCache)
		{
			return await activityGroups.ValueAsync(tenantId, cancellationToken).ConfigureAwait(false);
		}
		var bytes = await _cache.GetAsync(key, cancellationToken).ConfigureAwait(false);
		var document = bytes is null ? null : JsonSerializer.Deserialize(bytes, GrantCacheJsonContext.Default.ActivityGroupCacheDocument);
		if (document is not null && string.Equals(document.TenantId, tenantId, StringComparison.Ordinal)
			&& document.ExpiresOn > _timeProvider.GetUtcNow())
		{
			return document.Groups.ToDictionary(pair => pair.Key, pair => (IReadOnlyCollection<string>)pair.Value, StringComparer.Ordinal);
		}
		var groups = await activityGroups.ValueAsync(tenantId, cancellationToken).ConfigureAwait(false);
		var expires = _timeProvider.GetUtcNow() + cacheOptions.Value.AbsoluteExpirationRelativeToNow;
		var owned = groups.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray(), StringComparer.Ordinal);
		var encoded = JsonSerializer.SerializeToUtf8Bytes(new ActivityGroupCacheDocument(tenantId, expires, owned),
			GrantCacheJsonContext.Default.ActivityGroupCacheDocument);
		await _cache.SetAsync(key, encoded, new DistributedCacheEntryOptions
		{
			SlidingExpiration = TimeSpan.FromHours(1),
			AbsoluteExpirationRelativeToNow = cacheOptions.Value.AbsoluteExpirationRelativeToNow,
		}, cancellationToken).ConfigureAwait(false);
		return groups;
	}

	/// <summary>
	/// Internal record holding the loaded authorization data.
	/// </summary>
	private sealed record AuthorizationData(
		IDictionary<string, object> Grants,
		IReadOnlyDictionary<string, IReadOnlyCollection<string>> ActivityGroups);
}

