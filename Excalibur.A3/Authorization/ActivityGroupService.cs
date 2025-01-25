using System.Data;
using System.Net.Http.Headers;

using Excalibur.A3.Authentication;
using Excalibur.A3.Authorization.Grants.Domain.Model;
using Excalibur.A3.Authorization.Grants.Domain.RequestProviders;
using Excalibur.Core;
using Excalibur.DataAccess.Exceptions;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

using Newtonsoft.Json;

namespace Excalibur.A3.Authorization;

/// <summary>
///     Provides services for managing activity groups.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="ActivityGroupService" /> class. </remarks>
public class ActivityGroupService(
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
	public async Task<bool> Exists(string activityGroupName)
	{
		var activityGroupExists = activityGroupRequestProvider.ActivityGroupExists(activityGroupName);
		return await activityGroupExists.ResolveAsync(connection).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task SyncActivityGroups()
	{
		var endpoint = $"api/v1/*/applications/{ApplicationContext.ApplicationName}/activity-groups";
		using var requestMessage = CreateMessage(endpoint);
		using var response = await httpClient.SendAsync(requestMessage).ConfigureAwait(false);
		var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

		if (!response.IsSuccessStatusCode)
		{
			var reason = JsonConvert.DeserializeObject<string>(body);
			var exception =
				new OperationFailedException(nameof(SyncActivityGroups), "ActivityGroup", (int)response.StatusCode, reason, null);
			logger.LogErrorActivityGroups(reason, exception);

			throw exception;
		}

		var activityGroups = JsonConvert.DeserializeObject<IEnumerable<ActivityGroup>>(body);

		var deleteAllActivityGroups = activityGroupRequestProvider.DeleteAllActivityGroups();
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
					CancellationToken.None
				);

				_ = await createActivityGroup.ResolveAsync(connection).ConfigureAwait(false);
			}
		}

		await cache.RemoveAsync(AuthorizationCacheKey.ForActivityGroups()).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task SyncActivityGroupGrants(string userId)
	{
		var endpoint = $"api/v1/*/{userId}/grants/activity-groups";
		using var requestMessage = CreateMessage(endpoint);
		using var response = await httpClient.SendAsync(requestMessage).ConfigureAwait(false);
		var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

		if (!response.IsSuccessStatusCode)
		{
			var reason = JsonConvert.DeserializeObject<string>(body);
			var exception = new OperationFailedException(nameof(SyncActivityGroupGrants), "ActivityGroupGrant", (int)response.StatusCode,
				reason, null);
			logger.LogErrorActivityGrants(reason, exception);

			throw exception;
		}

		var results = JsonConvert.DeserializeObject<IEnumerable<ActivityGroupGrant>>(body);

		var grants = results?.Select(a => new
		{
			UserId = userId,
			a.TenantId,
			GrantType = GrantType.ActivityGroup,
			Qualifier = a.ActivtyGroupName,
			a.ExpiresOn,
			GrantedBy = token.FullName
		}).ToArray() ?? [];

		var deleteActivityGroupGrantsByUserId = grantRequestProvider.DeleteActivityGroupGrantsByUserId(userId, GrantType.ActivityGroup);
		_ = await deleteActivityGroupGrantsByUserId.ResolveAsync(connection).ConfigureAwait(false);

		if (grants.Length > 0)
		{
			foreach (var grant in grants)
			{
				var insertActivityGroupGrant = grantRequestProvider.InsertActivityGroupGrant(
					grant.UserId, grant.UserId, grant.TenantId, grant.GrantType, grant.Qualifier, grant.ExpiresOn, grant.GrantedBy);

				_ = await insertActivityGroupGrant.ResolveAsync(connection).ConfigureAwait(false);
			}
		}

		await cache.RemoveAsync(AuthorizationCacheKey.ForGrants(userId)).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task SyncAllActivityGroupGrants()
	{
		const string Endpoint = "api/v1/*/grants/activity-groups";
		using var requestMessage = CreateMessage(Endpoint);
		using var response = await httpClient.SendAsync(requestMessage).ConfigureAwait(false);
		var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

		if (!response.IsSuccessStatusCode)
		{
			var reason = JsonConvert.DeserializeObject<string>(body);
			var exception = new OperationFailedException(nameof(SyncAllActivityGroupGrants), "ActivityGroupGrant", (int)response.StatusCode,
				reason, null);
			logger.LogErrorActivityGrants(reason, exception);

			throw exception;
		}

		var results = JsonConvert.DeserializeObject<IEnumerable<ActivityGroupGrant>>(body);

		var grants = results?.Select(a => new
		{
			a.UserId,
			a.TenantId,
			GrantType = GrantType.ActivityGroup,
			Qualifier = a.ActivtyGroupName,
			a.ExpiresOn,
			GrantedBy = token.FullName
		}).ToArray() ?? [];

		var getDistinctActivityGroupGrantUserIds = grantRequestProvider.GetDistinctActivityGroupGrantUserIds(GrantType.ActivityGroup);
		var userIds = await getDistinctActivityGroupGrantUserIds.ResolveAsync(connection).ConfigureAwait(false);

		var deleteAllActivityGroupGrants = grantRequestProvider.DeleteAllActivityGroupGrants(GrantType.ActivityGroup);
		_ = await deleteAllActivityGroupGrants.ResolveAsync(connection).ConfigureAwait(false);

		if (grants.Length > 0)
		{
			foreach (var grant in grants)
			{
				var insertActivityGroupGrant = grantRequestProvider.InsertActivityGroupGrant(
					grant.UserId, grant.UserId, grant.TenantId, grant.GrantType, grant.Qualifier, grant.ExpiresOn, grant.GrantedBy);

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

		public required string ActivtyGroupName { get; init; }

		public DateTimeOffset? ExpiresOn { get; init; }

		public required string UserId { get; init; }
	}
}

/// <summary>
///     Contains extension methods for structured logging in the application.
/// </summary>
internal static class LogExtensions
{
	private static readonly Action<ILogger, string, Exception?> SLogErrorActivityGroups =
		LoggerMessage.Define<string>(
			LogLevel.Error,
			new EventId(1, nameof(LogErrorActivityGroups)),
			"Failed to retrieve activity groups because {Reason}");

	private static readonly Action<ILogger, string, Exception?> SLogErrorActivityGrants =
		LoggerMessage.Define<string>(
			LogLevel.Error,
			new EventId(2, nameof(LogErrorActivityGrants)),
			"Failed to retrieve activity group grants because {Reason}");

	/// <summary>
	///     Logs an error when activity groups cannot be retrieved.
	/// </summary>
	/// <param name="logger"> The logger instance. </param>
	/// <param name="reason"> The reason for the failure. </param>
	/// <param name="exception"> The associated exception, if any. </param>
	public static void LogErrorActivityGroups(this ILogger logger, string? reason, Exception? exception) =>
		SLogErrorActivityGroups(logger, reason ?? "unknown", exception);

	/// <summary>
	///     Logs an error when activity group grants cannot be retrieved.
	/// </summary>
	/// <param name="logger"> The logger instance. </param>
	/// <param name="reason"> The reason for the failure. </param>
	/// <param name="exception"> The associated exception, if any. </param>
	public static void LogErrorActivityGrants(this ILogger logger, string? reason, Exception? exception) =>
		SLogErrorActivityGrants(logger, reason ?? "unknown", exception);
}
