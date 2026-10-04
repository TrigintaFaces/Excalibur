// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.EventSourcing.TieredStorage;

namespace Excalibur.EventSourcing.Tests.TieredStorage;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Pattern", "EventSourcing")]
public sealed class ColdArchiveBatchShould
{
	private static readonly KeyedTenantPartition Tenant = KeyedTenantPartition.Scoped("tenant-a");
	private static StoredEvent Event(long version) =>
		new($"event-{version}", "aggregate", "Order", "Created", [1, 2], [3], version, DateTimeOffset.UnixEpoch);

	[Theory]
	[InlineData(0, 1)]
	[InlineData(5, -1)]
	public void RequireTheInitialVersionBeforeAcknowledgingAPrefix(long first, long expected)
	{
		ColdArchiveBatch.ContiguousDurablePrefix([Event(first), Event(first + 1)]).ShouldBe(expected);
	}

	[Fact]
	public void StopTheReceiptAtTheFirstMissingVersion() =>
		ColdArchiveBatch.ContiguousDurablePrefix([Event(0), Event(1), Event(3)]).ShouldBe(1);

	[Fact]
	public void ResolveAnUnsortedCompletePrefix() =>
		ColdArchiveBatch.ContiguousDurablePrefix([Event(1), Event(0)]).ShouldBe(1);

	[Fact]
	public void FreezeCallerOwnedPayloadAndMetadataBeforeAwaitingStorage()
	{
		var original = Event(0);
		var snapshot = ColdArchiveBatch.Snapshot([original]);
		original.EventData![0] = 99;
		original.Metadata![0] = 99;
		snapshot[0].EventData.ShouldBe(new byte[] { 1, 2 });
		snapshot[0].Metadata.ShouldBe(new byte[] { 3 });
	}

	[Fact]
	public void CompareRetryPayloadsByContentAndAcceptLegacyKeyProvenance()
	{
		var existing = Event(0);
		var retry = existing with { EventData = [1, 2], Metadata = [3], TenantId = Tenant.TenantId };
		ColdArchiveBatch.GetAdditions(Tenant, "aggregate", [existing], [retry]).ShouldBeEmpty();
	}

	[Theory]
	[InlineData(0, 42)]
	[InlineData(42, 0)]
	public void AcceptLegacyPositionProvenanceWithoutRewritingIt(long storedPosition, long retryPosition)
	{
		var existing = Event(0) with { GlobalPosition = storedPosition };
		var retry = existing with { GlobalPosition = retryPosition };
		ColdArchiveBatch.GetAdditions(Tenant, "aggregate", [existing], [retry]).ShouldBeEmpty();
		existing.GlobalPosition.ShouldBe(storedPosition);
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public void RejectNegativePositionProvenance(bool malformedExisting)
	{
		var malformed = Event(0) with { GlobalPosition = -1 };
		Should.Throw<InvalidOperationException>(() => ColdArchiveBatch.GetAdditions(
			Tenant, "aggregate", malformedExisting ? [malformed] : [], malformedExisting ? [Event(0)] : [malformed]));
	}

	[Theory]
	[InlineData("id")]
	[InlineData("payload")]
	[InlineData("metadata")]
	[InlineData("type")]
	[InlineData("aggregate")]
	[InlineData("tenant")]
	[InlineData("timestamp")]
	[InlineData("position")]
	[InlineData("missing")]
	[InlineData("erased")]
	public void RejectConflictingRetriesBeforeAcknowledging(string conflict)
	{
		var existing = Event(0) with { GlobalPosition = 1 };
		var retry = conflict switch
		{
			"id" => existing with { EventId = "different" },
			"payload" => existing with { EventData = [9] },
			"metadata" => existing with { Metadata = null },
			"type" => existing with { AggregateType = "Other" },
			"aggregate" => existing with { AggregateId = "other" },
			"tenant" => existing with { TenantId = "Tenant-a" },
			"timestamp" => existing with { Timestamp = DateTimeOffset.UnixEpoch.AddSeconds(1) },
			"position" => existing with { GlobalPosition = 2 },
			"erased" => existing with { EventType = "$erased" },
			_ => existing with { EventData = null },
		};
		Should.Throw<InvalidOperationException>(() => ColdArchiveBatch.GetAdditions(Tenant, "aggregate", [existing], [retry]));
	}

	[Fact]
	public void RejectAnEventIdReusedAtAnotherVersion() =>
		Should.Throw<InvalidOperationException>(() => ColdArchiveBatch.GetAdditions(
			Tenant, "aggregate", [Event(0)], [Event(1) with { EventId = "event-0" }]));

	[Fact]
	public void RejectAFreshErasureMarkerEvenWithReplacementBytes() =>
		Should.Throw<InvalidOperationException>(() => ColdArchiveBatch.GetAdditions(
			Tenant, "aggregate", [], [Event(0) with { EventType = "$erased" }]));

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public void RejectDuplicatesWithinEitherInput(bool duplicateExisting)
	{
		IReadOnlyList<StoredEvent> duplicated = [Event(0), Event(0)];
		Should.Throw<InvalidOperationException>(() => ColdArchiveBatch.GetAdditions(
			Tenant, "aggregate", duplicateExisting ? duplicated : [], duplicateExisting ? [Event(1)] : duplicated));
	}
}
