// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

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
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class AccessReviewRevokeUnreviewedShould : UnitTestBase
{
	private const int GrantStoreMissingEventId = 3525;
	private const int GrantQueryStoreMissingEventId = 3526;
	private const int CampaignLeftOpenEventId = 3532;
	private const int CampaignExpiredRevokedEventId = 3522;

	private static readonly AccessReviewScope DefaultScope = new(AccessReviewScopeType.AllGrants, null);

	private static Grant SampleGrant() =>
		new("user-1", "User One", "tenant-1", Excalibur.A3.Authorization.Grants.GrantType.Role, "reader",
			null, "admin", DateTimeOffset.UtcNow.AddDays(-60));

	/// <summary>A grant store that can be queried and records what it was asked to delete.</summary>
	private sealed class RecordingGrantStore(IEnumerable<Grant> grants, bool deleteThrows = false)
		: IGrantStore, IGrantQueryStore
	{
		private readonly List<Grant> _grants = [.. grants];

		public List<string> Deleted { get; } = [];

		public Task<IReadOnlyList<Grant>> GetMatchingGrantsAsync(
			string? userId, string tenantId, string grantType, string qualifier, CancellationToken cancellationToken)
			=> Task.FromResult<IReadOnlyList<Grant>>(_grants);

		public Task<IReadOnlyDictionary<string, object>> FindUserGrantsAsync(
			string userId, CancellationToken cancellationToken)
			=> Task.FromResult<IReadOnlyDictionary<string, object>>(new Dictionary<string, object>());

		public Task<int> DeleteGrantAsync(
			string userId, string tenantId, string grantType, string qualifier,
			string? revokedBy, DateTimeOffset? revokedOn, CancellationToken cancellationToken)
		{
			if (deleteThrows)
			{
				throw new InvalidOperationException("grant store unavailable");
			}

			Deleted.Add(userId);
			return Task.FromResult(1);
		}

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

	private static AccessReviewCampaignSummary ExpiredRevokeCampaign(DateTimeOffset expiredAt, int decidedItems = 0) =>
		new("campaign-1", "Q1 Review", DefaultScope, "admin",
			expiredAt.AddDays(-30), expiredAt,
			AccessReviewExpiryPolicy.RevokeUnreviewed, AccessReviewState.InProgress, 5, decidedItems);

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

		grantStore.Deleted.ShouldContain("user-1");

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
}
