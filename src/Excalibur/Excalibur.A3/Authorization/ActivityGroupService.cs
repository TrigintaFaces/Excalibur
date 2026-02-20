// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Headers;
using System.Text.Json;

using Excalibur.A3.Authentication;
using Excalibur.A3.Authorization.Grants;
using Excalibur.Data.Abstractions;
using Excalibur.Dispatch.Abstractions;
using Excalibur.Domain;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace Excalibur.A3.Authorization;

/// <summary>
/// Provides services for managing activity groups.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="ActivityGroupService" /> class. </remarks>
public sealed class ActivityGroupService(
	HttpClient httpClient,
	ICorrelationId correlationId,
	IDbConnection connection,
	IAuthenticationToken token,
	IActivityGroupRequestProvider activityGroupRequestProvider,
	IGrantRequestProvider grantRequestProvider,
	ILogger<ActivityGroupService> logger,
	IDistributedCache cache) : IActivityGroupService
{
	/// <inheritdoc />
	public async Task<bool> ExistsAsync(string activityGroupName)
	{
		var activityGroupExists = activityGroupRequestProvider.ActivityGroupExists(activityGroupName, CancellationToken.None);
		return await activityGroupExists.ResolveAsync(connection).ConfigureAwait(false);
	}

	/// <inheritdoc />
	[RequiresUnreferencedCode("JSON deserialization may require unreferenced types for reflection-based operations")]
	[RequiresDynamicCode("JSON deserialization uses reflection to dynamically create and populate types")]
	public async Task SyncActivityGroupsAsync()
	{
		var endpoint = $"api/v1/*/applications/{ApplicationContext.ApplicationName}/activity-groups";
		using var requestMessage = CreateMessage(endpoint);
		using var response = await httpClient.SendAsync(requestMessage).ConfigureAwait(false);
		var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

		if (!response.IsSuccessStatusCode)
		{
			var reason = JsonSerializer.Deserialize<string>(body);
			var exception =
				new OperationFailedException(nameof(SyncActivityGroupsAsync), "ActivityGroup", (int)response.StatusCode, reason);
			logger.LogErrorActivityGroups(reason ?? "unknown", exception);

			throw exception;
		}

		var activityGroups = JsonSerializer.Deserialize<IEnumerable<ActivityGroup>>(body);

		var deleteAllActivityGroups = activityGroupRequestProvider.DeleteAllActivityGroups(CancellationToken.None);
		_ = await deleteAllActivityGroups.ResolveAsync(connection).ConfigureAwait(false);

		var activitiesInGroups = activityGroups
			?.SelectMany(a => a.Activities.Select(act => new { ActivityGroupName = a.Name, act.ActivityName, a.TenantId })).ToArray() ?? [];

		if (activitiesInGroups.Length > 0)
		{
			foreach (var activityGroup in activitiesInGroups)
			{
				var createActivityGroup = activityGroupRequestProvider.CreateActivityGroup(
					activityGroup.TenantId,
					activityGroup.ActivityGroupName,
					activityGroup.ActivityName,
					CancellationToken.None);

				_ = await createActivityGroup.ResolveAsync(connection).ConfigureAwait(false);
			}
		}

		await cache.RemoveAsync(AuthorizationCacheKey.ForActivityGroups()).ConfigureAwait(false);
	}

	/// <inheritdoc />
	[RequiresUnreferencedCode("JSON deserialization may require unreferenced types for reflection-based operations")]
	[RequiresDynamicCode("JSON deserialization uses reflection to dynamically create and populate types")]
	public async Task SyncActivityGroupGrantsAsync(string userId)
	{
		var endpoint = $"api/v1/*/{userId}/grants/activity-groups";

		// Make the HTTP request (implementation would need proper HTTP client)
		var response = await httpClient.GetAsync(new Uri(endpoint, UriKind.Relative)).ConfigureAwait(false);
		var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

		if (!response.IsSuccessStatusCode)
		{
			var reason = JsonSerializer.Deserialize<string>(body);
			var exception = new OperationFailedException(nameof(SyncActivityGroupGrantsAsync), "ActivityGroupGrant", (int)response.StatusCode,
				reason);
			logger.LogErrorActivityGrants(reason ?? "unknown", exception);

			throw exception;
		}

		var results = JsonSerializer.Deserialize<IEnumerable<ActivityGroupGrant>>(body);

		var grants = results?.Select(a => new
		{
			UserId = userId,
			a.TenantId,
			GrantType = GrantType.ActivityGroup,
			Qualifier = a.ActivityGroupName,
			a.ExpiresOn,
			GrantedBy = token.FullName,
		}).ToArray() ?? [];

		var deleteActivityGroupGrantsByUserId = grantRequestProvider.DeleteActivityGroupGrantsByUserId(userId, GrantType.ActivityGroup, CancellationToken.None);
		_ = await deleteActivityGroupGrantsByUserId.ResolveAsync(connection).ConfigureAwait(false);

		if (grants.Length > 0)
		{
			foreach (var grant in grants)
			{
				var insertActivityGroupGrant = grantRequestProvider.InsertActivityGroupGrant(
					grant.UserId, grant.UserId, grant.TenantId, grant.GrantType, grant.Qualifier, grant.ExpiresOn, grant.GrantedBy, CancellationToken.None);

				_ = await insertActivityGroupGrant.ResolveAsync(connection).ConfigureAwait(false);
			}
		}

		await cache.RemoveAsync(AuthorizationCacheKey.ForGrants(userId)).ConfigureAwait(false);
	}

	/// <inheritdoc />
	[RequiresUnreferencedCode("JSON deserialization may require unreferenced types for reflection-based operations")]
	[RequiresDynamicCode("JSON deserialization uses reflection to dynamically create and populate types")]
	public async Task SyncAllActivityGroupGrantsAsync()
	{
		const string Endpoint = "api/v1/*/grants/activity-groups";

		// Make the HTTP request (implementation would need proper HTTP client)
		var response = await httpClient.GetAsync(new Uri(Endpoint, UriKind.Relative)).ConfigureAwait(false);
		var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

		if (!response.IsSuccessStatusCode)
		{
			var reason = JsonSerializer.Deserialize<string>(body);
			var exception = new OperationFailedException(nameof(SyncAllActivityGroupGrantsAsync), "ActivityGroupGrant", (int)response.StatusCode,
				reason);
			logger.LogErrorActivityGrants(reason ?? "unknown", exception);

			throw exception;
		}

		var results = JsonSerializer.Deserialize<IEnumerable<ActivityGroupGrant>>(body);

		var grants = results?.Select(a => new
		{
			a.UserId,
			a.TenantId,
			GrantType = GrantType.ActivityGroup,
			Qualifier = a.ActivityGroupName,
			a.ExpiresOn,
			GrantedBy = token.FullName,
		}).ToArray() ?? [];

		var getDistinctActivityGroupGrantUserIds = grantRequestProvider.GetDistinctActivityGroupGrantUserIds(GrantType.ActivityGroup, CancellationToken.None);
		var userIds = await getDistinctActivityGroupGrantUserIds.ResolveAsync(connection).ConfigureAwait(false);

		var deleteAllActivityGroupGrants = grantRequestProvider.DeleteAllActivityGroupGrants(GrantType.ActivityGroup, CancellationToken.None);
		_ = await deleteAllActivityGroupGrants.ResolveAsync(connection).ConfigureAwait(false);

		if (grants.Length > 0)
		{
			foreach (var grant in grants)
			{
				var insertActivityGroupGrant = grantRequestProvider.InsertActivityGroupGrant(
					grant.UserId, grant.UserId, grant.TenantId, grant.GrantType, grant.Qualifier, grant.ExpiresOn, grant.GrantedBy, CancellationToken.None);

				_ = await insertActivityGroupGrant.ResolveAsync(connection).ConfigureAwait(false);
			}
		}

		await Task.WhenAll(userIds.Select(u => cache.RemoveAsync(AuthorizationCacheKey.ForGrants(u)))).ConfigureAwait(false);
	}

	private HttpRequestMessage CreateMessage(string endpoint)
	{
		var message = new HttpRequestMessage(HttpMethod.Get, endpoint);

		message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Jwt ?? string.Empty);
		message.Headers.Add(ExcaliburHeaderNames.CorrelationId, correlationId.ToString());

		return message;
	}

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
}
