// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.A3.Authorization;
using Excalibur.A3.Governance.AccessReviews;
using Excalibur.A3.Governance.Stores.InMemory;

using Microsoft.Extensions.Logging;

using Tests.Shared.Helpers;

namespace Excalibur.A3.Governance.Tests.AccessReviews;

/// <summary>
/// Binds the RevokeUnreviewed expiry policy to what it actually does.
/// </summary>
/// <remarks>
/// <para>
/// The campaign record is the audit evidence a later reader treats as proof that a review completed and
/// that unreviewed access was withdrawn. It used to be written "regardless" — with no grant store
/// registered, with a store that could not be queried, or after a deletion that exhausted its retries,
/// the campaign still moved to Expired and the log still said the grants had been revoked.
/// </para>
/// <para>
/// Each behaviour is asserted in a pair. The safety arm proves no completion is recorded for revocation
/// that did not happen; the liveness arm proves a correctly configured campaign still revokes and still
/// completes. The safety arm alone would be satisfied by a policy branch that did nothing at all.
/// </para>
/// <para>
/// The revocation arms run against the real in-memory grant store (resolved exactly as a consumer gets it,
/// from <c>AddExcaliburA3Core()</c>) and assert the query the policy
/// SENT. A fake that ignored its arguments hid the defect this suite now pins: the policy asked for grants
/// with an empty tenant, type and qualifier, every real store read those as exact values and returned
/// nothing, and the campaign was recorded as complete having revoked nothing.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class AccessReviewRevokeUnreviewedShould : UnitTestBase
{
	private const int GrantStoreMissingEventId = 3525;
	private const int GrantQueryStoreMissingEventId = 3526;
	private const int CampaignLeftOpenEventId = 3532;
	private const int CampaignExpiredRevokedEventId = 3522;
	private const int CampaignScopeUnresolvableEventId = 3533;

	private static readonly AccessReviewScope DefaultScope = new(AccessReviewScopeType.AllGrants, null);

	private static Grant SampleGrant() =>
		new("user-1", "User One", "tenant-1", Excalibur.A3.Authorization.Grants.GrantType.Role, "reader",
			null, "admin", DateTimeOffset.UtcNow.AddDays(-60));

	/// <summary>
	/// The real in-memory grant store, observed: it records every query it is sent and every grant it is asked
	/// to delete, and can be told to fail deletion.
	/// </summary>
	private sealed class RecordingGrantStore : IGrantStore, IGrantQueryStore
	{
		private readonly IGrantStore _inner = new ServiceCollection().AddExcaliburA3Core().Services
			.BuildServiceProvider().GetRequiredService<IGrantStore>();
		private readonly bool _deleteThrows;

		public RecordingGrantStore(IEnumerable<Grant> grants, bool deleteThrows = false)
		{
			_deleteThrows = deleteThrows;
			foreach (var grant in grants)
			{
				// The in-memory store completes synchronously; asserting that keeps seeding free of sync-over-async.
				_inner.SaveGrantAsync(grant, CancellationToken.None).IsCompletedSuccessfully.ShouldBeTrue();
			}
		}

		public List<(string TenantId, string? UserId, string? GrantType, string? Qualifier)> Queries { get; } = [];

		public List<string> Deleted { get; } = [];

		private IGrantQueryStore InnerQuery => (IGrantQueryStore)_inner.GetService(typeof(IGrantQueryStore))!;

		public Task<IReadOnlyList<Grant>> Remaining(string userId) =>
			_inner.GetAllGrantsAsync(userId, includeExpired: true, CancellationToken.None);

		public Task<IReadOnlyList<Grant>> GetMatchingGrantsAsync(
			string tenantId, string? userId, string? grantType, string? qualifier, CancellationToken cancellationToken)
		{
			Queries.Add((tenantId, userId, grantType, qualifier));
			return InnerQuery.GetMatchingGrantsAsync(tenantId, userId, grantType, qualifier, cancellationToken);
		}

		public Task<IReadOnlyList<Grant>> GetMatchingGrantsAcrossTenantsAsync(
			string? userId, string? grantType, string? qualifier, CancellationToken cancellationToken) =>
			throw new InvalidOperationException("a campaign's revoke must never read across tenants");

		public Task<IReadOnlyDictionary<string, object>> FindUserGrantsAsync(
			string userId, CancellationToken cancellationToken) =>
			InnerQuery.FindUserGrantsAsync(userId, cancellationToken);

		public async Task<int> DeleteGrantAsync(
			string userId, string tenantId, string grantType, string qualifier,
			string? revokedBy, DateTimeOffset? revokedOn, CancellationToken cancellationToken)
		{
			if (_deleteThrows)
			{
				throw new InvalidOperationException("grant store unavailable");
			}

			Deleted.Add($"{userId}|{tenantId}|{grantType}|{qualifier}");
			return await _inner.DeleteGrantAsync(userId, tenantId, grantType, qualifier, revokedBy, revokedOn, cancellationToken)
				.ConfigureAwait(false);
		}

		public Task<Grant?> GetGrantAsync(
			string userId, string tenantId, string grantType, string qualifier, CancellationToken cancellationToken) =>
			_inner.GetGrantAsync(userId, tenantId, grantType, qualifier, cancellationToken);

		public Task<IReadOnlyList<Grant>> GetAllGrantsAsync(string userId, CancellationToken cancellationToken) =>
			_inner.GetAllGrantsAsync(userId, cancellationToken);

		public Task<IReadOnlyList<Grant>> GetAllGrantsAsync(
			string userId, bool includeExpired, CancellationToken cancellationToken) =>
			_inner.GetAllGrantsAsync(userId, includeExpired, cancellationToken);

		public Task<int> SaveGrantAsync(Grant grant, CancellationToken cancellationToken) =>
			_inner.SaveGrantAsync(grant, cancellationToken);

		public Task<bool> GrantExistsAsync(
			string userId, string tenantId, string grantType, string qualifier, CancellationToken cancellationToken) =>
			_inner.GrantExistsAsync(userId, tenantId, grantType, qualifier, cancellationToken);
	}

	/// <summary>A grant store that does NOT provide the query capability the revoke path needs.</summary>
	private sealed class UnqueryableGrantStore : IGrantStore
	{
		public Task<int> DeleteGrantAsync(
			string userId, string tenantId, string grantType, string qualifier,
			string? revokedBy, DateTimeOffset? revokedOn, CancellationToken cancellationToken)
			=> Task.FromResult(0);

		public Task<Grant?> GetGrantAsync(
			string userId, string tenantId, string grantType, string qualifier, CancellationToken cancellationToken)
			=> Task.FromResult<Grant?>(null);

		public Task<IReadOnlyList<Grant>> GetAllGrantsAsync(string userId, CancellationToken cancellationToken)
			=> Task.FromResult<IReadOnlyList<Grant>>([]);

		public Task<IReadOnlyList<Grant>> GetAllGrantsAsync(
			string userId, bool includeExpired, CancellationToken cancellationToken)
			=> Task.FromResult<IReadOnlyList<Grant>>([]);

		public Task<int> SaveGrantAsync(Grant grant, CancellationToken cancellationToken)
			=> Task.FromResult(1);

		public Task<bool> GrantExistsAsync(
			string userId, string tenantId, string grantType, string qualifier, CancellationToken cancellationToken)
			=> Task.FromResult(false);
	}

	private static AccessReviewCampaignSummary ExpiredRevokeCampaign(
		DateTimeOffset expiredAt, int decidedItems = 0, AccessReviewScope? scope = null) =>
		new("campaign-1", "tenant-1", "Q1 Review", scope ?? DefaultScope, "admin",
			expiredAt.AddDays(-30), expiredAt,
			AccessReviewExpiryPolicy.RevokeUnreviewed, AccessReviewState.InProgress, 5, decidedItems);

	private static Grant G(string user, string tenant, string type, string qualifier) =>
		new(user, user, tenant, type, qualifier, null, "admin", DateTimeOffset.UtcNow.AddDays(-60));

	/// <summary>
	/// Grants in the campaign's tenant, in a neighbouring tenant, and in the untenanted partition -- with a role
	/// and a user that also exist elsewhere, so a revoke that is not confined to the campaign's tenant, or not
	/// confined to its scope, removes something it must not.
	/// </summary>
	private static Grant[] Estate() =>
	[
		G("user-1", "tenant-1", Excalibur.A3.Authorization.Grants.GrantType.Role, "Admin"),
		G("user-1", "tenant-1", "Activity", "orders.read"),
		G("user-2", "tenant-1", Excalibur.A3.Authorization.Grants.GrantType.Role, "Reader"),
		G("user-1", "tenant-2", Excalibur.A3.Authorization.Grants.GrantType.Role, "Admin"),
		G("user-1", TenantScope.UntenantedSentinel, Excalibur.A3.Authorization.Grants.GrantType.Role, "Admin"),
	];

	private static async Task<string[]> RemainingAsync(RecordingGrantStore store)
	{
		var all = new List<Grant>();
		all.AddRange(await store.Remaining("user-1").ConfigureAwait(false));
		all.AddRange(await store.Remaining("user-2").ConfigureAwait(false));
		return all.Select(g => $"{g.UserId}|{g.TenantId}|{g.GrantType}|{g.Qualifier}")
			.OrderBy(k => k, StringComparer.Ordinal).ToArray();
	}

	private static async Task<(RecordingGrantStore Grants, InMemoryAccessReviewStore Reviews, CapturingLogger<AccessReviewExpiryService> Logger)>
		SweepAsync(AccessReviewScope scope, int waitForEventId)
	{
		var expiresAt = DateTimeOffset.UtcNow.AddDays(-1);
		var reviews = new InMemoryAccessReviewStore();
		await reviews.SaveCampaignAsync(ExpiredRevokeCampaign(expiresAt, scope: scope), CancellationToken.None)
			.ConfigureAwait(false);

		var grants = new RecordingGrantStore(Estate());
		await using var provider = BuildHost(reviews, grants);
		var logger = new CapturingLogger<AccessReviewExpiryService>();

		await RunSweepAsync(provider, logger, () => logger.Entries.Any(e => e.EventId.Id == waitForEventId))
			.ConfigureAwait(false);

		return (grants, reviews, logger);
	}

	private static ServiceProvider BuildHost(IAccessReviewStore store, IGrantStore? grantStore)
	{
		var services = new ServiceCollection();
		_ = services.AddSingleton(store);

		if (grantStore is not null)
		{
			_ = services.AddSingleton(grantStore);
		}

		return services.BuildServiceProvider();
	}

	private static async Task RunSweepAsync(
		ServiceProvider provider,
		CapturingLogger<AccessReviewExpiryService> logger,
		Func<bool> until)
	{
		var options = Options.Create(new AccessReviewOptions
		{
			ExpiryCheckInterval = TimeSpan.FromMilliseconds(25),
			MaxRetryAttempts = 2,
			RetryBaseDelay = TimeSpan.FromMilliseconds(1)
		});

		var service = new AccessReviewExpiryService(
			provider.GetRequiredService<IServiceScopeFactory>(), options, logger, TimeProvider.System);

		await service.StartAsync(CancellationToken.None).ConfigureAwait(false);
		try
		{
			await WaitUntilAsync(until, TimeSpan.FromSeconds(10)).ConfigureAwait(false);
		}
		finally
		{
			await service.StopAsync(CancellationToken.None).ConfigureAwait(false);
		}
	}

	private static async Task AssertCampaignStillOpenAsync(InMemoryAccessReviewStore store, DateTimeOffset expiresAt)
	{
		var stored = await store.GetCampaignAsync("campaign-1", CancellationToken.None).ConfigureAwait(false);
		stored.ShouldNotBeNull();
		stored.State.ShouldBe(AccessReviewState.InProgress);
		stored.ExpiresAt.ShouldBe(expiresAt);
	}

	// ---- SAFETY: no grant store at all ----

	[Fact]
	public async Task NotRecordCompletion_WhenNoGrantStoreIsRegistered()
	{
		var expiresAt = DateTimeOffset.UtcNow.AddDays(-1);
		var store = new InMemoryAccessReviewStore();
		await store.SaveCampaignAsync(ExpiredRevokeCampaign(expiresAt), CancellationToken.None).ConfigureAwait(false);

		await using var provider = BuildHost(store, grantStore: null);
		var logger = new CapturingLogger<AccessReviewExpiryService>();

		await RunSweepAsync(provider, logger,
			() => logger.Entries.Any(e => e.EventId.Id == GrantStoreMissingEventId)).ConfigureAwait(false);

		var reported = logger.Entries.Where(e => e.EventId.Id == GrantStoreMissingEventId).ToList();
		reported.ShouldNotBeEmpty();
		reported[0].Level.ShouldBe(LogLevel.Error);
		reported[0].Message.ShouldContain("campaign-1");

		await AssertCampaignStillOpenAsync(store, expiresAt).ConfigureAwait(false);
	}

	// ---- SAFETY: a grant store that cannot be queried revokes nothing ----

	[Fact]
	public async Task NotRecordCompletion_WhenTheGrantStoreCannotBeQueried()
	{
		var expiresAt = DateTimeOffset.UtcNow.AddDays(-1);
		var store = new InMemoryAccessReviewStore();
		await store.SaveCampaignAsync(ExpiredRevokeCampaign(expiresAt), CancellationToken.None).ConfigureAwait(false);

		await using var provider = BuildHost(store, new UnqueryableGrantStore());
		var logger = new CapturingLogger<AccessReviewExpiryService>();

		await RunSweepAsync(provider, logger,
			() => logger.Entries.Any(e => e.EventId.Id == CampaignLeftOpenEventId)).ConfigureAwait(false);

		logger.Entries.ShouldContain(e => e.EventId.Id == GrantQueryStoreMissingEventId);
		await AssertCampaignStillOpenAsync(store, expiresAt).ConfigureAwait(false);
	}

	// ---- SAFETY: a grant whose deletion exhausted its retries is still access left in place ----

	[Fact]
	public async Task NotRecordCompletion_WhenAGrantCouldNotBeDeleted()
	{
		var expiresAt = DateTimeOffset.UtcNow.AddDays(-1);
		var store = new InMemoryAccessReviewStore();
		await store.SaveCampaignAsync(ExpiredRevokeCampaign(expiresAt), CancellationToken.None).ConfigureAwait(false);

		await using var provider = BuildHost(store, new RecordingGrantStore([SampleGrant()], deleteThrows: true));
		var logger = new CapturingLogger<AccessReviewExpiryService>();

		await RunSweepAsync(provider, logger,
			() => logger.Entries.Any(e => e.EventId.Id == CampaignLeftOpenEventId)).ConfigureAwait(false);

		await AssertCampaignStillOpenAsync(store, expiresAt).ConfigureAwait(false);
	}

	// ---- LIVENESS: a correctly configured campaign still revokes and still completes ----

	[Fact]
	public async Task RevokeGrantsThenRecordCompletion_WhenTheGrantStoreWorks()
	{
		var expiresAt = DateTimeOffset.UtcNow.AddDays(-1);
		var store = new InMemoryAccessReviewStore();
		await store.SaveCampaignAsync(ExpiredRevokeCampaign(expiresAt), CancellationToken.None).ConfigureAwait(false);

		var grantStore = new RecordingGrantStore([SampleGrant()]);
		await using var provider = BuildHost(store, grantStore);
		var logger = new CapturingLogger<AccessReviewExpiryService>();

		await RunSweepAsync(provider, logger,
			() => logger.Entries.Any(e => e.EventId.Id == CampaignExpiredRevokedEventId)).ConfigureAwait(false);

		grantStore.Deleted.ShouldContain(d => d.StartsWith("user-1|", StringComparison.Ordinal));

		var stored = await store.GetCampaignAsync("campaign-1", CancellationToken.None).ConfigureAwait(false);
		stored.ShouldNotBeNull();
		stored.State.ShouldBe(AccessReviewState.Expired);
	}

	// ---- LIVENESS: a fully reviewed campaign has nothing to revoke and still completes ----

	[Fact]
	public async Task RecordCompletion_WhenNothingWasLeftUnreviewed()
	{
		var expiresAt = DateTimeOffset.UtcNow.AddDays(-1);
		var store = new InMemoryAccessReviewStore();
		await store.SaveCampaignAsync(ExpiredRevokeCampaign(expiresAt, decidedItems: 5), CancellationToken.None)
			.ConfigureAwait(false);

		// No grant store, and nothing that needed revoking: the receipt is honest, so it must still be
		// written. Refusing here would stall every fully-reviewed campaign in a host with no grant store.
		await using var provider = BuildHost(store, grantStore: null);
		var logger = new CapturingLogger<AccessReviewExpiryService>();

		await RunSweepAsync(provider, logger,
			() => logger.Entries.Any(e => e.EventId.Id == CampaignExpiredRevokedEventId)).ConfigureAwait(false);

		var stored = await store.GetCampaignAsync("campaign-1", CancellationToken.None).ConfigureAwait(false);
		stored.ShouldNotBeNull();
		stored.State.ShouldBe(AccessReviewState.Expired);
	}

	// ---- SAFETY + LIVENESS: every scope revokes exactly its own grants, inside the campaign's tenant ----

	[Fact]
	public async Task RevokeEveryGrantInTheCampaignsTenant_AndNothingOutsideIt_ForAnAllGrantsScope()
	{
		var (grants, reviews, _) = await SweepAsync(DefaultScope, CampaignExpiredRevokedEventId).ConfigureAwait(false);

		grants.Queries.ShouldBe([("tenant-1", (string?)null, (string?)null, (string?)null)],
			"the policy must ask for the campaign's tenant with no other constraint -- never an empty-string filter");
		(await RemainingAsync(grants).ConfigureAwait(false)).ShouldBe(
			[
				"user-1|__untenanted__|Role|Admin",
				"user-1|tenant-2|Role|Admin",
			],
			customMessage: "every grant in tenant-1 must be revoked, and no grant in any other tenant");
		(await reviews.GetCampaignAsync("campaign-1", CancellationToken.None).ConfigureAwait(false))!.State
			.ShouldBe(AccessReviewState.Expired);
	}

	[Fact]
	public async Task RevokeOnlyTheRolesGrants_ForAByRoleScope()
	{
		var (grants, _, _) = await SweepAsync(
			new AccessReviewScope(AccessReviewScopeType.ByRole, "Admin"), CampaignExpiredRevokedEventId).ConfigureAwait(false);

		grants.Queries.ShouldBe([("tenant-1", (string?)null, (string?)Excalibur.A3.Authorization.Grants.GrantType.Role, (string?)"Admin")]);
		(await RemainingAsync(grants).ConfigureAwait(false)).ShouldBe(
			[
				"user-1|__untenanted__|Role|Admin",
				"user-1|tenant-1|Activity|orders.read",
				"user-1|tenant-2|Role|Admin",
				"user-2|tenant-1|Role|Reader",
			],
			customMessage: "only the Admin role grant in tenant-1 is in scope");
	}

	[Fact]
	public async Task RevokeOnlyTheUsersGrants_ForAByUserScope()
	{
		var (grants, _, _) = await SweepAsync(
			new AccessReviewScope(AccessReviewScopeType.ByUser, "user-1"), CampaignExpiredRevokedEventId).ConfigureAwait(false);

		grants.Queries.ShouldBe([("tenant-1", (string?)"user-1", (string?)null, (string?)null)]);
		(await RemainingAsync(grants).ConfigureAwait(false)).ShouldBe(
			[
				"user-1|__untenanted__|Role|Admin",
				"user-1|tenant-2|Role|Admin",
				"user-2|tenant-1|Role|Reader",
			],
			customMessage: "only user-1's grants in tenant-1 are in scope");
	}

	/// <summary>
	/// FAIL CLOSED. A ByUser or ByRole scope with no value names no grants. Read as "unconstrained" it would
	/// revoke every grant in the tenant on the strength of a malformed record; it must revoke nothing and leave
	/// the campaign open instead.
	/// </summary>
	[Theory]
	[InlineData(AccessReviewScopeType.ByUser)]
	[InlineData(AccessReviewScopeType.ByRole)]
	public async Task RevokeNothingAndLeaveTheCampaignOpen_WhenAScopeHasNoValue(AccessReviewScopeType type)
	{
		var (grants, reviews, logger) = await SweepAsync(
			new AccessReviewScope(type, null), CampaignScopeUnresolvableEventId).ConfigureAwait(false);

		grants.Queries.ShouldBeEmpty("a scope that names no grants must not be sent to the store at all");
		grants.Deleted.ShouldBeEmpty("nothing may be revoked on the strength of a scope that names no grants");
		(await RemainingAsync(grants).ConfigureAwait(false)).Length.ShouldBe(Estate().Length);
		logger.Entries.ShouldContain(e => e.EventId.Id == CampaignScopeUnresolvableEventId && e.Level == LogLevel.Error);
		(await reviews.GetCampaignAsync("campaign-1", CancellationToken.None).ConfigureAwait(false))!.State
			.ShouldBe(AccessReviewState.InProgress, "a campaign that revoked nothing must not be recorded as complete");
	}
}
