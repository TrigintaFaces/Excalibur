// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Excalibur.A3.Governance.AccessReviews;
using Excalibur.A3.Governance.Stores.InMemory;

using Microsoft.Extensions.Logging;

using Tests.Shared.Helpers;
using Microsoft.Extensions.Time.Testing;

namespace Excalibur.A3.Governance.Tests.AccessReviews;

/// <summary>
/// Binds the NotifyAndExtend expiry policy to what it actually does.
/// </summary>
/// <remarks>
/// <para>
/// Each behaviour is asserted in a pair. The safety arm proves the audit trail never records a
/// notification that was not sent; the liveness arm proves a correctly configured campaign is still
/// notified and still extended. The safety arm alone would be satisfied by a policy branch that did
/// nothing at all, which is why both are here.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class AccessReviewNotifyAndExtendShould : UnitTestBase
{
	private const int NotifierMissingEventId = 3531;
	private const int CampaignExtendedEventId = 3523;
	private const int ExpiryCheckFailedEventId = 3520;

	private static readonly AccessReviewScope DefaultScope = new(AccessReviewScopeType.AllGrants, null);

	/// <summary>A notifier that records the campaigns it was asked to notify about.</summary>
	private sealed class RecordingNotifier : IAccessReviewNotifier
	{
		private readonly ConcurrentBag<string> _ids = [];

		public IReadOnlyCollection<string> NotifiedCampaignIds => [.. _ids];

		public Task NotifyCampaignExtendedAsync(string campaignId, CancellationToken cancellationToken)
		{
			_ids.Add(campaignId);
			return Task.CompletedTask;
		}
	}

	/// <summary>A notifier whose delivery fails, standing in for an unreachable mail or chat transport.</summary>
	private sealed class ThrowingNotifier : IAccessReviewNotifier
	{
		public Task NotifyCampaignExtendedAsync(string campaignId, CancellationToken cancellationToken)
			=> throw new InvalidOperationException("notification transport unavailable");
	}

	private static AccessReviewCampaignSummary ExpiredNotifyAndExtendCampaign(DateTimeOffset expiredAt) =>
		new("campaign-1", "Q1 Review", DefaultScope, "admin",
			expiredAt.AddDays(-30), expiredAt,
			AccessReviewExpiryPolicy.NotifyAndExtend, AccessReviewState.InProgress, 5, 0);

	private static ServiceProvider BuildHost(IAccessReviewStore store, IAccessReviewNotifier? notifier)
	{
		var services = new ServiceCollection();
		_ = services.AddSingleton(store);

		if (notifier is not null)
		{
			_ = services.AddSingleton(notifier);
		}

		return services.BuildServiceProvider();
	}

	private static async Task RunSweepAsync(
		ServiceProvider provider,
		CapturingLogger<AccessReviewExpiryService> logger,
		Func<bool> until,
		TimeProvider? timeProvider = null)
	{
		var options = Options.Create(new AccessReviewOptions
		{
			ExpiryCheckInterval = TimeSpan.FromMilliseconds(25),
			ExtensionDays = 7
		});

		var service = new AccessReviewExpiryService(
			provider.GetRequiredService<IServiceScopeFactory>(), options, logger, timeProvider ?? TimeProvider.System);

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


	// ---- The expiry decision follows the INJECTED clock, not the machine's ----
	//
	// Both halves are needed. "Not expired before the deadline" alone passes for a service that never
	// expires anything; "expired after" alone passes for one that expires everything on sight. Only the
	// pair shows the decision tracks the clock it was given -- which is the whole point of injecting it.
	[Fact]
	public async Task ExpireACampaignOnlyOnceTheInjectedClockPassesItsDeadline()
	{
		var expiresAt = new DateTimeOffset(2030, 1, 10, 0, 0, 0, TimeSpan.Zero);
		var clock = new FakeTimeProvider(expiresAt.AddDays(-1));

		var store = new InMemoryAccessReviewStore();
		await store.SaveCampaignAsync(ExpiredNotifyAndExtendCampaign(expiresAt), CancellationToken.None)
			.ConfigureAwait(false);

		await using var provider = BuildHost(store, new RecordingNotifier());
		var logger = new CapturingLogger<AccessReviewExpiryService>();

		// BEFORE the deadline on the injected clock. The machine clock is years past it, so a service
		// reading DateTimeOffset.UtcNow would expire this immediately.
		await RunSweepAsync(provider, logger, () => false, clock).ConfigureAwait(false);
		logger.Entries.ShouldNotContain(
			e => e.EventId.Id == CampaignExtendedEventId,
			"the campaign is not yet due on the injected clock; expiring it means the service is reading "
			+ "the machine clock rather than the one it was given");

		// AFTER: advance only the injected clock. Nothing else changes.
		clock.SetUtcNow(expiresAt.AddMinutes(1));
		await RunSweepAsync(provider, logger,
			() => logger.Entries.Any(e => e.EventId.Id == CampaignExtendedEventId), clock).ConfigureAwait(false);

		logger.Entries.ShouldContain(
			e => e.EventId.Id == CampaignExtendedEventId,
			"advancing the injected clock past the deadline must expire the campaign");
	}

	// ---- SAFETY: nothing is claimed, and nothing is changed, when no notifier is registered ----

	[Fact]
	public async Task NotClaimNotificationNorExtendDeadline_WhenNoNotifierIsRegistered()
	{
		var expiresAt = DateTimeOffset.UtcNow.AddDays(-1);
		var store = new InMemoryAccessReviewStore();
		await store.SaveCampaignAsync(ExpiredNotifyAndExtendCampaign(expiresAt), CancellationToken.None)
			.ConfigureAwait(false);

		await using var provider = BuildHost(store, notifier: null);
		var logger = new CapturingLogger<AccessReviewExpiryService>();

		await RunSweepAsync(provider, logger,
			() => logger.Entries.Any(e => e.EventId.Id == NotifierMissingEventId)).ConfigureAwait(false);

		// The misconfiguration is reported, at Error, naming the campaign.
		var reported = logger.Entries.Where(e => e.EventId.Id == NotifierMissingEventId).ToList();
		reported.ShouldNotBeEmpty();
		reported[0].Level.ShouldBe(LogLevel.Error);
		reported[0].Message.ShouldContain("campaign-1");

		// Nothing claims a notification happened.
		logger.Entries.ShouldNotContain(e => e.EventId.Id == CampaignExtendedEventId);
		logger.Entries.ShouldNotContain(e => e.Message.Contains("notified", StringComparison.OrdinalIgnoreCase));

		// And the campaign is left exactly as it was -- not extended, not expired.
		var stored = await store.GetCampaignAsync("campaign-1", CancellationToken.None).ConfigureAwait(false);
		stored.ShouldNotBeNull();
		stored.State.ShouldBe(AccessReviewState.InProgress);
		stored.ExpiresAt.ShouldBe(expiresAt);
	}

	// ---- LIVENESS: a configured campaign is still notified, and still extended ----

	[Fact]
	public async Task NotifyReviewersThenExtendDeadline_WhenNotifierIsRegistered()
	{
		var expiresAt = DateTimeOffset.UtcNow.AddDays(-1);
		var store = new InMemoryAccessReviewStore();
		await store.SaveCampaignAsync(ExpiredNotifyAndExtendCampaign(expiresAt), CancellationToken.None)
			.ConfigureAwait(false);

		var notifier = new RecordingNotifier();
		await using var provider = BuildHost(store, notifier);
		var logger = new CapturingLogger<AccessReviewExpiryService>();

		var before = DateTimeOffset.UtcNow;
		await RunSweepAsync(provider, logger,
			() => notifier.NotifiedCampaignIds.Contains("campaign-1")).ConfigureAwait(false);
		var after = DateTimeOffset.UtcNow;

		// The reviewers were actually notified.
		notifier.NotifiedCampaignIds.ShouldContain("campaign-1");

		// The deadline actually moved, by ExtensionDays, measured from the moment of extension.
		var stored = await store.GetCampaignAsync("campaign-1", CancellationToken.None).ConfigureAwait(false);
		stored.ShouldNotBeNull();
		stored.ExpiresAt.ShouldBeGreaterThanOrEqualTo(before.AddDays(7));
		stored.ExpiresAt.ShouldBeLessThanOrEqualTo(after.AddDays(7));

		// Extending keeps the campaign running; it is not an expiry.
		stored.State.ShouldBe(AccessReviewState.InProgress);

		// Only now may the log say so.
		logger.Entries.ShouldContain(e => e.EventId.Id == CampaignExtendedEventId);
	}

	// ---- SAFETY: a failed delivery is not a notification either ----

	[Fact]
	public async Task NotExtendDeadline_WhenNotifierFailsToDeliver()
	{
		var expiresAt = DateTimeOffset.UtcNow.AddDays(-1);
		var store = new InMemoryAccessReviewStore();
		await store.SaveCampaignAsync(ExpiredNotifyAndExtendCampaign(expiresAt), CancellationToken.None)
			.ConfigureAwait(false);

		await using var provider = BuildHost(store, new ThrowingNotifier());
		var logger = new CapturingLogger<AccessReviewExpiryService>();

		await RunSweepAsync(provider, logger,
			() => logger.Entries.Any(e => e.EventId.Id == ExpiryCheckFailedEventId)).ConfigureAwait(false);

		logger.Entries.ShouldNotContain(e => e.EventId.Id == CampaignExtendedEventId);

		var stored = await store.GetCampaignAsync("campaign-1", CancellationToken.None).ConfigureAwait(false);
		stored.ShouldNotBeNull();
		stored.ExpiresAt.ShouldBe(expiresAt);
		stored.State.ShouldBe(AccessReviewState.InProgress);
	}
}
