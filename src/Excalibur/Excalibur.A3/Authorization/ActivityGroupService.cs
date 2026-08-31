// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

using Excalibur.A3.Authentication;
using Excalibur.A3.Authorization.Grants;
using Excalibur.Data;
using Excalibur.Dispatch;
using Excalibur.Domain;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

using ExcaliburHeaderNames = Excalibur.Application.ExcaliburHeaderNames;

namespace Excalibur.A3.Authorization;

/// <summary>
/// Provides services for managing activity groups.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="ActivityGroupService" /> class. </remarks>
public sealed partial class ActivityGroupService(
	HttpClient httpClient,
	ICorrelationId correlationId,
	IAuthenticationToken token,
	IActivityGroupStore activityGroupStore,
	IActivityGroupGrantStore activityGroupGrantStore,
	ILogger<ActivityGroupService> logger,
	IDistributedCache cache) : IActivityGroupService
{
	/// <inheritdoc />
	public async Task<bool> ExistsAsync(string activityGroupName, CancellationToken cancellationToken)
	{
		return await activityGroupStore.ActivityGroupExistsAsync(activityGroupName, cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task SyncActivityGroupsAsync(CancellationToken cancellationToken)
	{
		var endpoint = $"api/v1/*/applications/{ApplicationContext.ApplicationName}/activity-groups";
		using var requestMessage = CreateMessage(endpoint);
		using var response = await httpClient.SendAsync(requestMessage, cancellationToken).ConfigureAwait(false);
		var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

		if (!response.IsSuccessStatusCode)
		{
			var reason = JsonSerializer.Deserialize(body, ActivityGroupJsonContext.Default.String);
			var exception =
				new OperationFailedException(nameof(SyncActivityGroupsAsync), "ActivityGroup", (int)response.StatusCode, reason);
			logger.LogErrorActivityGroups(reason ?? "unknown", exception);

			throw exception;
		}

		var activityGroups = JsonSerializer.Deserialize(body, ActivityGroupJsonContext.Default.IEnumerableActivityGroup);

		_ = await activityGroupStore.DeleteAllActivityGroupsAsync(cancellationToken).ConfigureAwait(false);

		var activitiesInGroups = activityGroups
			?.SelectMany(a => a.Activities.Select(act => new { ActivityGroupName = a.Name, act.ActivityName, a.TenantId })).ToArray() ?? [];

		if (activitiesInGroups.Length > 0)
		{
			foreach (var activityGroup in activitiesInGroups)
			{
				_ = await activityGroupStore.CreateActivityGroupAsync(
					RequireTenant(activityGroup.TenantId, activityGroup.ActivityGroupName),
					activityGroup.ActivityGroupName,
					activityGroup.ActivityName,
					cancellationToken).ConfigureAwait(false);
			}
		}

		await cache.RemoveAsync(AuthorizationCacheKey.ForActivityGroups(), cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task SyncActivityGroupGrantsAsync(string userId, CancellationToken cancellationToken)
	{
		var endpoint = $"api/v1/*/{userId}/grants/activity-groups";

		// Make the HTTP request (implementation would need proper HTTP client)
		var response = await httpClient.GetAsync(new Uri(endpoint, UriKind.Relative), cancellationToken).ConfigureAwait(false);
		var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

		if (!response.IsSuccessStatusCode)
		{
			var reason = JsonSerializer.Deserialize(body, ActivityGroupJsonContext.Default.String);
			var exception = new OperationFailedException(nameof(SyncActivityGroupGrantsAsync), "ActivityGroupGrant", (int)response.StatusCode,
				reason);
			logger.LogErrorActivityGrants(reason ?? "unknown", exception);

			throw exception;
		}

		var results = JsonSerializer.Deserialize(body, ActivityGroupJsonContext.Default.IEnumerableActivityGroupGrant);

		var grants = results?.Select(a => new
		{
			UserId = userId,
			a.TenantId,
			GrantType = GrantType.ActivityGroup,
			Qualifier = a.ActivityGroupName,
			a.ExpiresOn,
			GrantedBy = token.FullName,
		}).ToArray() ?? [];

		_ = await activityGroupGrantStore.DeleteActivityGroupGrantsByUserIdAsync(userId, GrantType.ActivityGroup, cancellationToken).ConfigureAwait(false);

		if (grants.Length > 0)
		{
			foreach (var grant in grants)
			{
				_ = await activityGroupGrantStore.InsertActivityGroupGrantAsync(
					grant.UserId, grant.UserId, RequireTenant(grant.TenantId, grant.Qualifier), grant.GrantType, grant.Qualifier, grant.ExpiresOn, grant.GrantedBy, cancellationToken).ConfigureAwait(false);
			}
		}

		await cache.RemoveAsync(AuthorizationCacheKey.ForGrants(userId), cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task SyncAllActivityGroupGrantsAsync(CancellationToken cancellationToken)
	{
		const string Endpoint = "api/v1/*/grants/activity-groups";

		// Make the HTTP request (implementation would need proper HTTP client)
		var response = await httpClient.GetAsync(new Uri(Endpoint, UriKind.Relative), cancellationToken).ConfigureAwait(false);
		var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

		if (!response.IsSuccessStatusCode)
		{
			var reason = JsonSerializer.Deserialize(body, ActivityGroupJsonContext.Default.String);
			var exception = new OperationFailedException(nameof(SyncAllActivityGroupGrantsAsync), "ActivityGroupGrant", (int)response.StatusCode,
				reason);
			logger.LogErrorActivityGrants(reason ?? "unknown", exception);

			throw exception;
		}

		var results = JsonSerializer.Deserialize(body, ActivityGroupJsonContext.Default.IEnumerableActivityGroupGrant);

		var grants = results?.Select(a => new
		{
			a.UserId,
			a.TenantId,
			GrantType = GrantType.ActivityGroup,
			Qualifier = a.ActivityGroupName,
			a.ExpiresOn,
			GrantedBy = token.FullName,
		}).ToArray() ?? [];

		var userIds = await activityGroupGrantStore.GetDistinctActivityGroupGrantUserIdsAsync(GrantType.ActivityGroup, cancellationToken).ConfigureAwait(false);

		_ = await activityGroupGrantStore.DeleteAllActivityGroupGrantsAsync(GrantType.ActivityGroup, cancellationToken).ConfigureAwait(false);

		if (grants.Length > 0)
		{
			foreach (var grant in grants)
			{
				_ = await activityGroupGrantStore.InsertActivityGroupGrantAsync(
					grant.UserId, grant.UserId, RequireTenant(grant.TenantId, grant.Qualifier), grant.GrantType, grant.Qualifier, grant.ExpiresOn, grant.GrantedBy, cancellationToken).ConfigureAwait(false);
			}
		}

		await Task.WhenAll(userIds.Select(u => cache.RemoveAsync(AuthorizationCacheKey.ForGrants(u), cancellationToken))).ConfigureAwait(false);
	}

	private HttpRequestMessage CreateMessage(string endpoint)
	{
		var message = new HttpRequestMessage(HttpMethod.Get, endpoint);

		message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Jwt ?? string.Empty);
		message.Headers.Add(ExcaliburHeaderNames.CorrelationId, correlationId.ToString());

		return message;
	}

	/// <summary>
	/// Returns the tenant identifier, rejecting a payload that omits it.
	/// </summary>
	/// <param name="tenantId">The tenant identifier supplied by the remote payload.</param>
	/// <param name="subject">The activity group or qualifier the entry describes, used in the error message.</param>
	/// <returns>The non-empty tenant identifier.</returns>
	/// <exception cref="InvalidOperationException">Thrown when the payload omits the tenant identifier.</exception>
	private static string RequireTenant(string? tenantId, string subject) =>
		!string.IsNullOrEmpty(tenantId)
			? tenantId
			: throw new InvalidOperationException(
				$"The activity-group payload entry '{subject}' does not specify a tenant. Every activity group and grant belongs to exactly one tenant.");

	private sealed record ActivityGroup
	{
		public string? TenantId { get; init; }

		public required string Name { get; init; }

		public required string Description { get; init; }

		public required List<Activity> Activities { get; set; }
	}

	private sealed record Activity
	{
		public required string ApplicationName { get; init; }

		public required string ActivityName { get; init; }
	}

	private sealed record ActivityGroupGrant
	{
		public string? TenantId { get; init; }

		public required string ActivityGroupName { get; init; }

		public DateTimeOffset? ExpiresOn { get; init; }

		public required string UserId { get; init; }
	}

	/// <summary>
	/// Source-generated serializer metadata for the activity-group sync payloads. Keeps the
	/// deserialization path reflection-free so the service stays usable under trimming and
	/// ahead-of-time compilation.
	/// </summary>
	[JsonSerializable(typeof(string))]
	[JsonSerializable(typeof(IEnumerable<ActivityGroup>))]
	[JsonSerializable(typeof(IEnumerable<ActivityGroupGrant>))]
	private sealed partial class ActivityGroupJsonContext : JsonSerializerContext;
}
