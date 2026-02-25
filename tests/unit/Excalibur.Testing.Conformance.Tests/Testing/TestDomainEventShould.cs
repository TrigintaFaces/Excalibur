// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Testing.Conformance;

using Shouldly;

using Xunit;

namespace Excalibur.Tests.Testing;

/// <summary>
/// Unit tests for <see cref="TestDomainEvent"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Testing")]
public sealed class TestDomainEventShould
{
	[Fact]
	public void Have_Default_EventId_As_NewGuid()
	{
		// Arrange & Act
		var evt = new TestDomainEvent();

		// Assert
		evt.EventId.ShouldNotBeNullOrEmpty();
		Guid.TryParse(evt.EventId, out _).ShouldBeTrue("EventId should be a valid GUID");
	}

	[Fact]
	public void Have_Default_AggregateId_As_Empty()
	{
		// Arrange & Act
		var evt = new TestDomainEvent();

		// Assert
		evt.AggregateId.ShouldBe(string.Empty);
	}

	[Fact]
	public void Have_Default_Version_As_Zero()
	{
		// Arrange & Act
		var evt = new TestDomainEvent();

		// Assert
		evt.Version.ShouldBe(0);
	}

	[Fact]
	public void Have_Default_OccurredAt_Near_UtcNow()
	{
		// Arrange
		var before = DateTime.UtcNow;

		// Act
		var evt = new TestDomainEvent();

		// Assert
		var after = DateTime.UtcNow;
		evt.OccurredAt.ShouldBeGreaterThanOrEqualTo(before);
		evt.OccurredAt.ShouldBeLessThanOrEqualTo(after);
	}

	[Fact]
	public void Have_EventType_As_TypeName()
	{
		// Arrange & Act
		var evt = new TestDomainEvent();

		// Assert
		evt.EventType.ShouldBe(nameof(TestDomainEvent));
	}

	[Fact]
	public void Have_Default_Metadata_As_Null()
	{
		// Arrange & Act
		var evt = new TestDomainEvent();

		// Assert
		evt.Metadata.ShouldBeNull();
	}

	[Fact]
	public void Have_Default_Payload_As_TestPayload()
	{
		// Arrange & Act
		var evt = new TestDomainEvent();

		// Assert
		evt.Payload.ShouldBe("test-payload");
	}

	[Fact]
	public void Allow_Setting_All_Properties_Via_Init()
	{
		// Arrange
		var eventId = "custom-event-id";
		var aggregateId = "custom-aggregate-id";
		var version = 42L;
		var occurredAt = new DateTime(2025, 1, 15, 10, 30, 0, DateTimeKind.Utc);
		var metadata = new Dictionary<string, object> { ["key"] = "value" };
		var payload = "custom-payload";

		// Act
		var evt = new TestDomainEvent
		{
			EventId = eventId,
			AggregateId = aggregateId,
			Version = version,
			OccurredAt = occurredAt,
			Metadata = metadata,
			Payload = payload
		};

		// Assert
		evt.EventId.ShouldBe(eventId);
		evt.AggregateId.ShouldBe(aggregateId);
		evt.Version.ShouldBe(version);
		evt.OccurredAt.ShouldBe(occurredAt);
		evt.Metadata.ShouldBe(metadata);
		evt.Payload.ShouldBe(payload);
	}

	[Fact]
	public void Create_Event_With_AggregateId_And_Version()
	{
		// Arrange
		var aggregateId = "test-aggregate-123";
		var version = 5L;

		// Act
		var evt = TestDomainEvent.Create(aggregateId, version);

		// Assert
		evt.ShouldNotBeNull();
		evt.AggregateId.ShouldBe(aggregateId);
		evt.Version.ShouldBe(version);
	}

	[Fact]
	public void Create_Event_With_Unique_EventId()
	{
		// Arrange
		var aggregateId = "test-aggregate";
		var version = 1L;

		// Act
		var evt1 = TestDomainEvent.Create(aggregateId, version);
		var evt2 = TestDomainEvent.Create(aggregateId, version);

		// Assert
		evt1.EventId.ShouldNotBe(evt2.EventId, "Each Create call should generate a unique EventId");
	}

	[Fact]
	public void Create_Event_With_OccurredAt_Near_UtcNow()
	{
		// Arrange
		var before = DateTime.UtcNow;
		var aggregateId = "test-aggregate";
		var version = 1L;

		// Act
		var evt = TestDomainEvent.Create(aggregateId, version);

		// Assert
		var after = DateTime.UtcNow;
		evt.OccurredAt.ShouldBeGreaterThanOrEqualTo(before);
		evt.OccurredAt.ShouldBeLessThanOrEqualTo(after);
	}

	[Fact]
	public void Create_Event_With_Payload_Including_Version()
	{
		// Arrange
		var aggregateId = "test-aggregate";
		var version = 7L;

		// Act
		var evt = TestDomainEvent.Create(aggregateId, version);

		// Assert
		evt.Payload.ShouldBe($"payload-v{version}");
	}

	[Fact]
	public void Be_Record_Type_With_Value_Equality()
	{
		// Arrange
		var eventId = "fixed-id";
		var aggregateId = "agg-1";
		var version = 1L;
		var occurredAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

		// Act
		var evt1 = new TestDomainEvent
		{
			EventId = eventId,
			AggregateId = aggregateId,
			Version = version,
			OccurredAt = occurredAt,
			Payload = "same"
		};

		var evt2 = new TestDomainEvent
		{
			EventId = eventId,
			AggregateId = aggregateId,
			Version = version,
			OccurredAt = occurredAt,
			Payload = "same"
		};

		// Assert
		evt1.ShouldBe(evt2);
		evt1.GetHashCode().ShouldBe(evt2.GetHashCode());
	}

	[Fact]
	public void Not_Be_Equal_When_Properties_Differ()
	{
		// Arrange
		var evt1 = new TestDomainEvent { AggregateId = "agg-1" };
		var evt2 = new TestDomainEvent { AggregateId = "agg-2" };

		// Assert
		evt1.ShouldNotBe(evt2);
	}
}
