// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.EventSourcing.TieredStorage;

namespace Excalibur.EventSourcing.Tests.TieredStorage;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Pattern", "EventSourcing")]
public sealed class ColdArchiveMigrationBaselineShould
{
	private static readonly KeyedTenantPartition Tenant = KeyedTenantPartition.Scoped("tenant-a");

	[Fact]
	public void PreserveSparseHistoryWhileAllowingGapFillsAndAppends()
	{
		var baseline = Capture([Event(2), Event(0)]);
		baseline.VerifyPreservedBy([Event(3), Event(1), Event(0), Event(2)]);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(2)]
	public void RejectRemovalOnEitherSideOfAGap(long removed)
	{
		var baseline = Capture([Event(0), Event(2)]);
		Should.Throw<InvalidOperationException>(() => baseline.VerifyPreservedBy([Event(removed == 0 ? 2 : 0)]));
	}

	[Theory]
	[InlineData("id")]
	[InlineData("aggregate")]
	[InlineData("aggregate-type")]
	[InlineData("event-type")]
	[InlineData("tenant")]
	[InlineData("version")]
	[InlineData("position")]
	[InlineData("timestamp")]
	[InlineData("timestamp-offset")]
	[InlineData("archived-at")]
	[InlineData("archive-offset")]
	[InlineData("archive-null")]
	[InlineData("payload")]
	[InlineData("metadata")]
	[InlineData("metadata-empty")]
	public void RejectChangesToCopiedRepresentation(string changed)
	{
		var original = Event(0);
		var candidate = changed switch
		{
			"id" => original with { EventId = "replacement" },
			"aggregate" => original with { AggregateId = "other" },
			"aggregate-type" => original with { AggregateType = "Other" },
			"event-type" => original with { EventType = "Other" },
			"tenant" => original with { TenantId = Tenant.TenantId },
			"version" => original with { Version = 1 },
			"position" => original with { GlobalPosition = 1 },
			"timestamp" => original with { Timestamp = original.Timestamp.AddTicks(1) },
			"timestamp-offset" => original with { Timestamp = original.Timestamp.ToOffset(TimeSpan.FromHours(1)) },
			"archived-at" => original with { ArchivedAt = original.ArchivedAt!.Value.AddTicks(1) },
			"archive-offset" => original with { ArchivedAt = original.ArchivedAt!.Value.ToOffset(TimeSpan.FromHours(1)) },
			"archive-null" => original with { ArchivedAt = null },
			"payload" => original with { EventData = [9] },
			"metadata" => original with { Metadata = [9] },
			_ => original with { Metadata = [] },
		};
		var baseline = Capture([original]);
		Should.Throw<InvalidOperationException>(() => baseline.VerifyPreservedBy([candidate]));
	}

	[Theory]
	[InlineData("duplicate-id")]
	[InlineData("duplicate-version")]
	[InlineData("tenant")]
	[InlineData("type")]
	[InlineData("payload")]
	[InlineData("erased")]
	[InlineData("negative-position")]
	[InlineData("negative-version")]
	[InlineData("blank-id")]
	[InlineData("blank-type")]
	public void RejectMalformedAdditionalEventsEvenWhenBaselineIsIntact(string defect)
	{
		var extra = defect switch
		{
			"duplicate-id" => Event(1) with { EventId = "event-0" },
			"duplicate-version" => Event(1) with { Version = 0 },
			"tenant" => Event(1) with { TenantId = "other" },
			"type" => Event(1) with { AggregateType = "Other" },
			"payload" => Event(1) with { EventData = null },
			"erased" => Event(1) with { EventType = "$erased" },
			"negative-position" => Event(1) with { GlobalPosition = -1 },
			"negative-version" => Event(1) with { Version = -1 },
			"blank-id" => Event(1) with { EventId = " " },
			_ => Event(1) with { EventType = " " },
		};
		var baseline = Capture([Event(0)]);
		Should.Throw<InvalidOperationException>(() => baseline.VerifyPreservedBy([Event(0), extra]));
		Should.Throw<InvalidOperationException>(() => Capture([Event(0), extra]));
	}

	[Fact]
	public void OwnSourcePayloadMetadataAndMembership()
	{
		var original = Event(0) with { Metadata = [7] };
		var source = new List<StoredEvent> { original };
		var baseline = Capture(source);
		original.EventData![0] = 99;
		original.Metadata![0] = 99;
		source.Clear();
		baseline.VerifyPreservedBy([Event(0) with { Metadata = [7] }]);
		Should.Throw<InvalidOperationException>(() => baseline.VerifyPreservedBy([original]));
	}

	[Fact]
	public void NeverAdoptTheDestinationsMutableBuffers()
	{
		var baseline = Capture([Event(0)]);
		var destination = Event(0);
		baseline.VerifyPreservedBy([destination]);
		destination.EventData![0] = 99;
		Should.Throw<InvalidOperationException>(() => baseline.VerifyPreservedBy([destination]));
		baseline.VerifyPreservedBy([Event(0)]);
	}

	[Fact]
	public void RefuseEmptyLegacyOwnershipOrMissingMigratedDestination()
	{
		Should.Throw<InvalidOperationException>(() => Capture([]));
		Should.Throw<InvalidOperationException>(() => Capture([Event(0)]).VerifyPreservedBy([]));
	}

	private static ColdArchiveMigrationBaseline Capture(IReadOnlyList<StoredEvent> source) =>
		ColdArchiveMigrationBaseline.Capture(Tenant, "aggregate", "Order", source);

	private static StoredEvent Event(long version) =>
		new($"event-{version}", "aggregate", "Order", "Created", [1, 2], null, version, DateTimeOffset.UnixEpoch)
		{
			ArchivedAt = DateTimeOffset.UnixEpoch.AddDays(1),
		};
}
