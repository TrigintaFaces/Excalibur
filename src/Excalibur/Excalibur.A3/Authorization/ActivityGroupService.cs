// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

using Excalibur.A3.Authentication;
using Excalibur.A3.Authorization.Grants;
using Excalibur.Data;
using Excalibur.Dispatch;
using Excalibur.Domain;

using Excalibur.Dispatch.Caching;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
	IOptions<ActivityGroupSyncOptions> syncOptions,
	ILogger<ActivityGroupService> logger,
	[FromKeyedServices(DistributedCacheServiceKeys.ApplicationScoped)] IDistributedCache cache) : IActivityGroupService
{
	/// <inheritdoc />
	public async Task<bool> ExistsAsync(string tenantId, string activityGroupName, CancellationToken cancellationToken)
	{
		return await activityGroupStore.ActivityGroupExistsAsync(tenantId, activityGroupName, cancellationToken)
			.ConfigureAwait(false);
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

		// VALIDATION PRECEDES DESTRUCTION, and the order is the whole correctness of this method.
		//
		// This is a wholesale replacement: it removes every tenant's groups and then repopulates from the
		// payload. Validating each entry while repopulating -- which is where the check used to live --
		// means a single entry naming no tenant empties the catalogue for EVERY tenant and then throws with
		// nothing restored. The payload is rejected, correctly, and the estate is already gone. Resolving
		// every tenant up front makes an unappliable payload fail while the catalogue is still intact.
		var activitiesInGroups = activityGroups
			?.SelectMany(a => a.Activities.Select(act => new
			{
				ActivityGroupName = a.Name,
				act.ActivityName,
				TenantId = RequireTenant(a.TenantId, a.Name),
			})).ToArray() ?? [];

		activitiesInGroups = RequireCompleteSnapshot(
			activitiesInGroups, (int)response.StatusCode, nameof(SyncActivityGroupsAsync), "activity group");

		// Constructing the catalogue validates every entry -- present, free of leading and trailing whitespace,
		// within the schema's widths -- so a payload one store would refuse is refused here, for every store,
		// while the existing catalogue is still intact.
		var catalogue = new ActivityGroupCatalogue(activitiesInGroups.Select(static g =>
			new ActivityGroupEntry(g.TenantId, g.ActivityGroupName, g.ActivityName)));

		// The tenants that held groups before the replace, reported by the replace itself from under its lock.
		// A tenant the payload dropped appears only here, and its cached catalogue would otherwise keep
		// conferring activities that no longer exist.
		IReadOnlyCollection<string> previousTenants = [];

		// Both sides of the replacement: the tenants that HAD groups (whose entry is now an over-grant) and
		// the tenants that HAVE them (whose entry is now stale). Evaluated at each invalidation, so the
		// previous tenants are included once the replace has reported them.
		//
		// If the replace commits and its reply is lost, the previous tenants are never learned and a dropped
		// tenant's cached catalogue is not invalidated here. It is still bounded: every cached entry expires
		// at most AuthorizationCacheOptions.AbsoluteExpirationRelativeToNow after it was written.
		IEnumerable<string> StaleKeys() =>
			previousTenants
				.Concat(catalogue.TenantIds)
				.Select(AuthorizationCacheKey.ForActivityGroups);

		await MutateThenInvalidateAsync(
			async () => previousTenants = await activityGroupStore
				.ReplaceAllActivityGroupsAsync(catalogue, cancellationToken)
				.ConfigureAwait(false),
			StaleKeys,
			cancellationToken).ConfigureAwait(false);
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

		// A BODY THAT DID NOT DESERIALIZE IS A FAILURE, NEVER AN EMPTY SET. This sync accepts an empty snapshot
		// below -- a user who now belongs to no group -- so the two must be told apart here: "the authority
		// returned nothing" revokes, "the fetch did not produce a list" is raised.
		var results = RequireDeserializedRows(
			JsonSerializer.Deserialize(body, ActivityGroupJsonContext.Default.IEnumerableActivityGroupGrant),
			(int)response.StatusCode,
			nameof(SyncActivityGroupGrantsAsync));

		// VALIDATION PRECEDES DESTRUCTION, as in the group sync: every row's tenant is resolved here, before
		// anything is deleted. Resolving it inside the insert loop meant one tenantless row removed every
		// activity-group grant the user held and then threw with nothing restored. Constructing the snapshot
		// validates every other term the same way.
		var snapshot = new ActivityGroupGrantSnapshot(results.Select(a => new ActivityGroupGrantEntry(
			userId,
			userId,
			RequireTenant(a.TenantId, a.ActivityGroupName),
			GrantType.ActivityGroup,
			a.ActivityGroupName,
			a.ExpiresOn,
			token.FullName)));

		// Resolved BEFORE the mutation is started, so a composition the host asked to be atomic and is not
		// fails without even invalidating a cache entry.
		var grantReplacement = GrantReplacement;

		// NO refusal of an empty snapshot here, unlike the two estate-wide syncs, and the asymmetry is the
		// requirement rather than an oversight: a user who now holds no activity-group grants is an ordinary
		// state and every grant they held must be revoked. Refusing it would keep revoked access alive.
		await MutateThenInvalidateAsync(
			async () =>
			{
				if (grantReplacement is { } replacement)
				{
					_ = await replacement.ReplaceActivityGroupGrantsForUserAsync(
						userId, GrantType.ActivityGroup, snapshot, cancellationToken).ConfigureAwait(false);

					return;
				}

				await DeleteThenInsertAsync(
					() => activityGroupGrantStore.DeleteActivityGroupGrantsByUserIdAsync(
						userId, GrantType.ActivityGroup, cancellationToken),
					snapshot,
					cancellationToken).ConfigureAwait(false);
			},
			() => [AuthorizationCacheKey.ForGrants(userId)],
			cancellationToken).ConfigureAwait(false);
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

		// Every row's tenant is resolved before anything is deleted; see the per-user sync.
		var entries = results?.Select(a => new ActivityGroupGrantEntry(
			a.UserId,
			a.UserId,
			RequireTenant(a.TenantId, a.ActivityGroupName),
			GrantType.ActivityGroup,
			a.ActivityGroupName,
			a.ExpiresOn,
			token.FullName)).ToArray() ?? [];

		// THIS sync is estate-wide, so an empty payload IS refused: it would revoke every user's
		// activity-group grants at once, and an empty response cannot be told apart from an upstream filter,
		// scope or schema change returning nothing.
		entries = RequireCompleteSnapshot(
			entries, (int)response.StatusCode, nameof(SyncAllActivityGroupGrantsAsync), "activity group grant");

		var snapshot = new ActivityGroupGrantSnapshot(entries);

		// Resolved BEFORE the mutation is started, so a composition the host asked to be atomic and is not
		// fails without even invalidating a cache entry.
		var grantReplacement = GrantReplacement;

		// The users that held grants before the replace, reported by the replace itself from inside its
		// transaction. A user the payload dropped appears only here, and their cached grants would otherwise
		// keep conferring activities that no longer exist.
		IReadOnlyCollection<string> previousUsers = [];

		// Both sides: users who HAD grants (whose cached grants may now over-grant) and users the payload
		// grants to (whose cached grants may be missing new ones). Evaluated at each invalidation, so the
		// previous users are included once the replace has reported them.
		IEnumerable<string> StaleKeys() =>
			previousUsers.Concat(snapshot.UserIds).Select(AuthorizationCacheKey.ForGrants);

		await MutateThenInvalidateAsync(
			async () =>
			{
				if (grantReplacement is { } replacement)
				{
					previousUsers = await replacement.ReplaceActivityGroupGrantsAsync(
						GrantType.ActivityGroup, snapshot, cancellationToken).ConfigureAwait(false);

					return;
				}

				// Without the capability the previous users cannot be read from the delete, so they are read
				// before it -- outside any lock, and therefore possibly already stale. That is one of the
				// costs of the non-atomic path, not a shape to copy.
				previousUsers = await activityGroupGrantStore
					.GetDistinctActivityGroupGrantUserIdsAsync(GrantType.ActivityGroup, cancellationToken)
					.ConfigureAwait(false);

				await DeleteThenInsertAsync(
					() => activityGroupGrantStore.DeleteAllActivityGroupGrantsAsync(
						GrantType.ActivityGroup, cancellationToken),
					snapshot,
					cancellationToken).ConfigureAwait(false);
			},
			StaleKeys,
			cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Gets the configured grant store's atomic-replace capability, or <see langword="null"/> when it has none
	/// and the host has accepted the non-atomic sync.
	/// </summary>
	/// <exception cref="InvalidOperationException">
	/// Thrown when the store has no atomic replace and
	/// <see cref="ActivityGroupSyncOptions.GrantSyncAtomicity"/> is
	/// <see cref="GrantSyncAtomicity.Required"/>. Nothing has been read or changed at this point.
	/// </exception>
	/// <remarks>
	/// The start-up prerequisite validator raises the same refusal earlier, from the registration. This is
	/// the enforcement: it reads the store the sync was actually handed, which is the only place the answer
	/// cannot be wrong -- a store registered through a factory has no type on its descriptor for the
	/// start-up check to read.
	/// </remarks>
	private IActivityGroupGrantReplacement? GrantReplacement
	{
		get
		{
			if (activityGroupGrantStore is IActivityGroupGrantReplacement replacement)
			{
				return replacement;
			}

			return syncOptions.Value.GrantSyncAtomicity == GrantSyncAtomicity.BestEffort
				? null
				: throw new InvalidOperationException(
					$"{activityGroupGrantStore.GetType().FullName} cannot replace a set of activity-group grants "
					+ $"in one step, and {nameof(ActivityGroupSyncOptions)}."
					+ $"{nameof(ActivityGroupSyncOptions.GrantSyncAtomicity)} is "
					+ $"{nameof(GrantSyncAtomicity.Required)}. Either compose a store that replaces atomically "
					+ "-- the SQL Server, PostgreSQL and in-memory stores do -- or accept the partial-state "
					+ $"window explicitly with services.Configure<{nameof(ActivityGroupSyncOptions)}>(o => o."
					+ $"{nameof(ActivityGroupSyncOptions.GrantSyncAtomicity)} = "
					+ $"{nameof(GrantSyncAtomicity)}.{nameof(GrantSyncAtomicity.BestEffort)}). Nothing was "
					+ "changed.");
		}
	}

	/// <summary>
	/// Applies <paramref name="snapshot"/> by deleting the grants in scope and inserting the new ones, for a
	/// store that cannot do both in one step.
	/// </summary>
	/// <remarks>
	/// <b>This path has a window and it is the reason the option naming it exists.</b> Between the delete and
	/// the last insert a reader observes a partial set of grants and is denied access the snapshot confers,
	/// and a failure part-way leaves the set partial until the next successful sync -- there is no rollback.
	/// </remarks>
	/// <param name="delete">Removes the grants in scope.</param>
	/// <param name="snapshot">The grants to write in their place.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	private async Task DeleteThenInsertAsync(
		Func<Task<int>> delete,
		ActivityGroupGrantSnapshot snapshot,
		CancellationToken cancellationToken)
	{
		_ = await delete().ConfigureAwait(false);

		foreach (var entry in snapshot.Entries)
		{
			_ = await activityGroupGrantStore.InsertActivityGroupGrantAsync(
				entry.UserId,
				entry.FullName,
				entry.TenantId,
				entry.GrantType,
				entry.Qualifier,
				entry.ExpiresOn,
				entry.GrantedBy,
				cancellationToken).ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Runs a sync mutation so that NO path through it leaves the authorization cache serving what the
	/// mutation changed.
	/// </summary>
	/// <remarks>
	/// <para>
	/// A cached entry lives until it is invalidated or its absolute bound passes, and it is kept alive by
	/// reads until then. Invalidating only after a successful mutation therefore leaves a failed sync's stale
	/// entries serving for up to that bound -- the groups or grants the completed portion of the sync removed.
	/// </para>
	/// <para>
	/// Invalidating only BEFORE the mutation is not enough either: a read between that invalidation and the
	/// end of the mutation re-caches the old state. So the keys are invalidated before the mutation, which
	/// errs toward denial, and again after it on every path. On the failure path the invalidation runs
	/// without the caller's token -- a cancelled token must not cancel the cleanup it made necessary -- and
	/// if the invalidation itself fails, both failures are reported rather than the second hiding the first.
	/// </para>
	/// </remarks>
	private async Task MutateThenInvalidateAsync(
		Func<Task> mutate,
		Func<IEnumerable<string>> keys,
		CancellationToken cancellationToken)
	{
		await InvalidateAsync(keys(), cancellationToken).ConfigureAwait(false);

		try
		{
			await mutate().ConfigureAwait(false);
		}
		catch (Exception mutationFailure)
		{
			try
			{
				await InvalidateAsync(keys(), CancellationToken.None).ConfigureAwait(false);
			}
			catch (Exception invalidationFailure)
			{
				throw new AggregateException(mutationFailure, invalidationFailure);
			}

			throw;
		}

		await InvalidateAsync(keys(), cancellationToken).ConfigureAwait(false);
	}

	private Task InvalidateAsync(IEnumerable<string> keys, CancellationToken cancellationToken) =>
		Task.WhenAll(keys.Distinct(StringComparer.Ordinal).Select(key => cache.RemoveAsync(key, cancellationToken)));

	private HttpRequestMessage CreateMessage(string endpoint)
	{
		var message = new HttpRequestMessage(HttpMethod.Get, endpoint);

		message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Jwt ?? string.Empty);
		message.Headers.Add(ExcaliburHeaderNames.CorrelationId, correlationId.ToString());

		return message;
	}

	/// <summary>
	/// Returns the payload rows, refusing a refresh that cannot be confirmed to be a complete snapshot.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>These Sync members are FULL REFRESHES</b> -- they remove the existing set and rewrite it from the
	/// payload. The remote authority is the source of truth and the local store is a projection of it, which
	/// is the intended contract and is not what this guard changes.
	/// </para>
	/// <para>
	/// <b>What it changes is how an AMBIGUOUS payload is resolved.</b> An empty result is indistinguishable
	/// between "the authority legitimately has none" and "an upstream filter, an authorization scope or a
	/// schema change returned nothing", and the framework has no way to tell which. Resolving that ambiguity
	/// by DELETING is the one reading that cannot be undone: a well-formed 200 with an empty body would
	/// remove every activity-group grant for every user and insert none, so every user loses every
	/// activity-group permission at once -- from a response that never indicated an error.
	/// </para>
	/// <para>
	/// <b>So emptying the store is an EXPLICIT act and never an inferred one.</b> The refusal is raised
	/// before anything is removed, in the same shape the method already uses for a non-success response, so
	/// the prior state survives untouched and the caller learns why.
	/// </para>
	/// </remarks>
	/// <typeparam name="T">The payload row type.</typeparam>
	/// <param name="rows">The rows projected from the response body.</param>
	/// <param name="statusCode">The status code the authority returned, carried into the failure.</param>
	/// <param name="operation">The sync operation, used in the error message.</param>
	/// <param name="subject">What the rows describe, used in the error message.</param>
	/// <returns>The rows, when there is at least one.</returns>
	/// <exception cref="OperationFailedException">Thrown when the payload carries no rows.</exception>
	private static T[] RequireCompleteSnapshot<T>(T[] rows, int statusCode, string operation, string subject) =>
		rows.Length > 0
			? rows
			: throw new OperationFailedException(
				operation,
				"ActivityGroup",
				statusCode,
				$"The {operation} response carried no {subject} rows. This is a full refresh, so applying an "
				+ "empty payload would delete the existing set and write nothing in its place -- and an empty "
				+ "response cannot be distinguished from an upstream filter, scope or schema change. Refusing "
				+ "rather than erasing: nothing was deleted and the existing set is intact.");

	/// <summary>
	/// Returns the deserialized rows, refusing a response body that did not produce a list at all.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>For the per-user sync, "no rows" and "no list" are different answers and only one of them is
	/// applied.</b> An empty list means the user now holds no activity-group grants, and every one of theirs
	/// is revoked. A null result means the body was not a list — a truncated response, a changed envelope, a
	/// schema drift — and applying it as "no grants" would revoke the user's access on the strength of a fetch
	/// that failed. A failure is raised; it is never handed on as an empty set.
	/// </para>
	/// </remarks>
	/// <typeparam name="T">The payload row type.</typeparam>
	/// <param name="rows">The deserialization result.</param>
	/// <param name="statusCode">The status code the authority returned, carried into the failure.</param>
	/// <param name="operation">The sync operation, used in the error message.</param>
	/// <returns>The rows, which may be empty.</returns>
	/// <exception cref="OperationFailedException">Thrown when the body did not deserialize to a list.</exception>
	private static IEnumerable<T> RequireDeserializedRows<T>(IEnumerable<T>? rows, int statusCode, string operation) =>
		rows ?? throw new OperationFailedException(
			operation,
			"ActivityGroupGrant",
			statusCode,
			$"The {operation} response body did not carry a list of grants. An empty list would be applied -- it "
			+ "means the user now holds none -- but a body that did not deserialize is a failed fetch, and "
			+ "revoking on the strength of one would remove access no authority asked to remove. Nothing was "
			+ "deleted and the existing grants are intact.");

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
