// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Testing.Builders;

namespace Excalibur.Dispatch.Testing.Tests.Builders;

[Trait("Category", "Unit")]
[Trait("Component", "Testing")]
public sealed class EventBuilderShould
{
	[Fact]
	public void BuildWithDefaults()
	{
		var evt = new EventBuilder().Build();
		evt.ShouldNotBeNull();
		evt.EventId.ShouldNotBeNullOrEmpty();
		evt.AggregateId.ShouldNotBeNullOrEmpty();
		evt.EventType.ShouldBe("TestEvent");
		evt.Data.ShouldBe(string.Empty);
		evt.OccurredAt.ShouldNotBe(default);
	}

	[Fact]
	public void SetEventId()
	{
		var evt = new EventBuilder()
			.WithEventId("evt-123")
			.Build();

		evt.EventId.ShouldBe("evt-123");
	}

	[Fact]
	public void SetAggregateId()
	{
		var evt = new EventBuilder()
			.WithAggregateId("agg-456")
			.Build();

		evt.AggregateId.ShouldBe("agg-456");
	}

	[Fact]
	public void SetVersion()
	{
		var evt = new EventBuilder()
			.WithVersion(42)
			.Build();

		evt.Version.ShouldBe(42);
	}

	[Fact]
	public void SetOccurredAt()
	{
		var ts = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
		var evt = new EventBuilder()
			.WithOccurredAt(ts)
			.Build();

		evt.OccurredAt.ShouldBe(ts);
	}

	[Fact]
	public void SetEventType()
	{
		var evt = new EventBuilder()
			.WithEventType("OrderPlaced")
			.Build();

		evt.EventType.ShouldBe("OrderPlaced");
	}

	[Fact]
	public void SetData()
	{
		var evt = new EventBuilder()
			.WithData("test-payload")
			.Build();

		evt.Data.ShouldBe("test-payload");
	}

	[Fact]
	public void SetMetadataDictionary()
	{
		var metadata = new Dictionary<string, object> { ["key1"] = "val1" };
		var evt = new EventBuilder()
			.WithMetadata(metadata)
			.Build();

		evt.Metadata.ShouldNotBeNull();
		evt.Metadata!["key1"].ShouldBe("val1");
	}

	[Fact]
	public void SetMetadataKeyValue()
	{
		var evt = new EventBuilder()
			.WithMetadata("key1", "val1")
			.WithMetadata("key2", 42)
			.Build();

		evt.Metadata.ShouldNotBeNull();
		evt.Metadata!["key1"].ShouldBe("val1");
		evt.Metadata!["key2"].ShouldBe(42);
	}

	[Fact]
	public void SupportFluentChaining()
	{
		var evt = new EventBuilder()
			.WithEventId("evt-1")
			.WithAggregateId("agg-1")
			.WithVersion(1)
			.WithEventType("Test")
			.WithData("payload")
			.WithMetadata("k", "v")
			.Build();

		evt.EventId.ShouldBe("evt-1");
		evt.AggregateId.ShouldBe("agg-1");
		evt.Version.ShouldBe(1);
	}

	[Fact]
	public void BuildManyWithSharedAggregateId()
	{
		var events = new EventBuilder()
			.WithAggregateId("agg-shared")
			.WithEventType("BatchEvent")
			.WithData("data")
			.BuildMany(5);

		events.Count.ShouldBe(5);
		events.ShouldAllBe(e => e.AggregateId == "agg-shared");
		events.ShouldAllBe(e => e.EventType == "BatchEvent");
	}

	[Fact]
	public void BuildManyWithIncrementingVersions()
	{
		var events = new EventBuilder()
			.WithAggregateId("agg-1")
			.BuildMany(3);

		events[0].Version.ShouldBe(0);
		events[1].Version.ShouldBe(1);
		events[2].Version.ShouldBe(2);
	}

	[Fact]
	public void BuildManyWithUniqueEventIds()
	{
		var events = new EventBuilder().BuildMany(5);
		events.Select(e => e.EventId).Distinct().Count().ShouldBe(5);
	}

	[Fact]
	public void BuildManyWithSuffixedData()
	{
		var events = new EventBuilder()
			.WithData("payload")
			.BuildMany(3);

		events[0].Data.ShouldBe("payload-0");
		events[1].Data.ShouldBe("payload-1");
		events[2].Data.ShouldBe("payload-2");
	}

	[Fact]
	public void BuildManyWithMetadata()
	{
		var events = new EventBuilder()
			.WithMetadata("shared-key", "shared-val")
			.BuildMany(2);

		events.ShouldAllBe(e => e.Metadata != null && (string)e.Metadata!["shared-key"] == "shared-val");
	}

	[Fact]
	public void BuildManyGeneratesAggregateIdWhenNotSet()
	{
		var events = new EventBuilder().BuildMany(3);
		// All should share the same aggregate ID
		events.Select(e => e.AggregateId).Distinct().Count().ShouldBe(1);
	}
}
