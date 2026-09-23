// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.A3.Governance.AccessReviews;
using Excalibur.A3.Governance.Events;
using Excalibur.Dispatch;
using Excalibur.Domain.Model;
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.Implementation;

using FakeItEasy;

using Microsoft.Extensions.Options;

using Shouldly;

using Xunit;

namespace Excalibur.A3.Governance.Tests.AccessReviews;

/// <summary>
/// jmt6uh — genuine read-path regression coverage for AccessReviewCampaign, the ONE aggregate of the
/// bead's three (Grant, Role, AccessReviewCampaign) with ZERO existing test files pairing GetByIdAsync
/// with the type at all (0 co-occurrences, verified 2026-07-22 and re-confirmed here). Companion coverage
/// for Grant/Role lives in GrantAndRoleRehydrationShould.cs (Excalibur.A3.Tests) — see that file's remarks
/// for why the tz2fks failure mode (an event silently never stamped) is structurally unreachable today
/// (IDomainEvent no longer carries Version at all; <see cref="HistoricEvent"/> sources it from the
/// envelope exclusively), and why real repository-level coverage is still genuinely new value regardless.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
public sealed class AccessReviewCampaignRehydrationShould
{
	/// <summary>LIVENESS + SAFETY — a campaign persisted through two real events rehydrates correctly and in isolation.</summary>
	[Fact]
	public async Task RehydrateACampaign_WithCorrectStateVersionAndNoCrossStreamLeakage()
	{
		var serializer = new JsonEventSerializer(TestEventTypeRegistry.Instance);
		var campaignId = "campaign-1";
		var otherCampaignId = "campaign-2";
		var scope = new AccessReviewScope(AccessReviewScopeType.AllGrants, null);
		var items = new List<AccessReviewItem>
		{
			new("user-1", "tenant-1:role:admin", DateTimeOffset.UtcNow.AddDays(-30), null),
		};

		IDomainEvent created = new AccessReviewCampaignCreated
		{
			CampaignId = campaignId,
			TenantId = "tenant-1",
			CampaignName = "Q3 Admin Review",
			Scope = scope,
			CreatedBy = "creator-1",
			StartsAt = DateTimeOffset.UtcNow.AddMinutes(-10),
			ExpiresAt = DateTimeOffset.UtcNow.AddDays(30),
			ExpiryPolicy = AccessReviewExpiryPolicy.RevokeUnreviewed,
			Items = items,
		};
		IDomainEvent started = new AccessReviewCampaignStarted { CampaignId = campaignId };

		IDomainEvent otherCreated = new AccessReviewCampaignCreated
		{
			CampaignId = otherCampaignId,
			TenantId = "tenant-1",
			CampaignName = "Q4 Viewer Review",
			Scope = scope,
			CreatedBy = "creator-1",
			StartsAt = DateTimeOffset.UtcNow.AddMinutes(-5),
			ExpiresAt = DateTimeOffset.UtcNow.AddDays(30),
			ExpiryPolicy = AccessReviewExpiryPolicy.NotifyAndExtend,
			Items = items,
		};

		var eventStore = A.Fake<IEventStore>();
		_ = A.CallTo(() => eventStore.LoadAsync(campaignId, "AccessReviewCampaign", A<long>._, A<CancellationToken>._))
			.Returns(new List<StoredEvent>
			{
				ToStoredEvent(serializer, created, campaignId, "AccessReviewCampaign", 0),
				ToStoredEvent(serializer, started, campaignId, "AccessReviewCampaign", 1),
			});
		_ = A.CallTo(() => eventStore.LoadAsync(otherCampaignId, "AccessReviewCampaign", A<long>._, A<CancellationToken>._))
			.Returns(new List<StoredEvent> { ToStoredEvent(serializer, otherCreated, otherCampaignId, "AccessReviewCampaign", 0) });

		var repository = new EventSourcedRepository<AccessReviewCampaign>(
			eventStore, serializer, AccessReviewCampaign.Create, Options.Create(new EventSourcedRepositoryOptions()));

		var result = await repository.GetByIdAsync(campaignId, CancellationToken.None);

		result.ShouldNotBeNull();
		result.Version.ShouldBe(2, "two events (created, started) were appended — Version is the applied count");
		result.CampaignName.ShouldBe("Q3 Admin Review", "LIVENESS: state must come from the events actually applied");
		result.ExpiryPolicy.ShouldBe(AccessReviewExpiryPolicy.RevokeUnreviewed);
		result.State.ShouldBe(
			AccessReviewState.InProgress,
			"the AccessReviewCampaignStarted event must have applied on top of Created — a version-loss "
			+ "regression would stop replay after the first event and leave State at Created");

		// SAFETY: a different campaign's stream does not carry this stream's Started transition.
		var other = await repository.GetByIdAsync(otherCampaignId, CancellationToken.None);
		other.ShouldNotBeNull();
		other.CampaignName.ShouldBe("Q4 Viewer Review");
		other.State.ShouldBe(AccessReviewState.Created, "a different campaign's stream must not leak this stream's Started transition");
		other.Version.ShouldBe(1, "the other stream has exactly one event");
	}

	private static StoredEvent ToStoredEvent(
		JsonEventSerializer serializer, IDomainEvent domainEvent, string aggregateId, string aggregateType, long version) =>
		new(
			EventId: Guid.NewGuid().ToString(),
			AggregateId: aggregateId,
			AggregateType: aggregateType,
			EventType: serializer.GetTypeName(domainEvent.GetType()),
			EventData: serializer.SerializeEvent(domainEvent),
			Metadata: null,
			Version: version,
			Timestamp: DateTimeOffset.UtcNow);

	/// <summary>
	/// Both events here are internal types (AccessReviewCampaignCreated/Started), so JsonEventSerializer's
	/// (allowAssemblyScan: true) CLR-type-name scan does not apply (it resolves by [MessageName] string,
	/// which is not what SearchLoadedAssemblies matches on) — a tiny explicit registry is the correct,
	/// minimal fix, not a broader reflection opt-in.
	/// </summary>
	private sealed class TestEventTypeRegistry : Excalibur.Dispatch.IEventTypeRegistry
	{
		public static readonly TestEventTypeRegistry Instance = new();

		private static readonly Dictionary<string, Type> ByName = new(StringComparer.Ordinal)
		{
			[Excalibur.Dispatch.MessageNameHelper.GetName(typeof(AccessReviewCampaignCreated))] = typeof(AccessReviewCampaignCreated),
			[Excalibur.Dispatch.MessageNameHelper.GetName(typeof(AccessReviewCampaignStarted))] = typeof(AccessReviewCampaignStarted),
		};

		public Type? ResolveType(string eventTypeName) => ByName.GetValueOrDefault(eventTypeName);

		public string? GetTypeName(Type eventType) => Excalibur.Dispatch.MessageNameHelper.GetName(eventType);
	}
}
