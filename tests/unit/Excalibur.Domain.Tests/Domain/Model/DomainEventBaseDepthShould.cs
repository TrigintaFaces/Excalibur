// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Domain.Model;

namespace Excalibur.Tests.Domain.Model;

/// <summary>
/// Depth coverage tests for <see cref="DomainEventBase"/>.
/// Covers all default property values and derived record behavior.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Domain")]
public sealed class DomainEventBaseDepthShould
{
	[Fact]
	public void DefaultValues_AreSetCorrectly()
	{
		// Arrange & Act
		var evt = new TestDomainEvent();

		// Assert
		evt.EventId.ShouldNotBeNullOrEmpty();
		Guid.TryParse(evt.EventId, out _).ShouldBeTrue();
		evt.AggregateId.ShouldBe(string.Empty);
		evt.Version.ShouldBe(0);
		evt.OccurredAt.ShouldBeGreaterThan(DateTimeOffset.UtcNow.AddMinutes(-1));
		evt.EventType.ShouldBe(nameof(TestDomainEvent));
		evt.Metadata.ShouldBeNull();
	}

	[Fact]
	public void EventType_ReturnsDerivedTypeName()
	{
		// Arrange & Act
		var evt = new OrderCreatedEvent("order-1", 100m);

		// Assert
		evt.EventType.ShouldBe(nameof(OrderCreatedEvent));
	}

	[Fact]
	public void AggregateId_CanBeOverridden()
	{
		// Arrange & Act
		var evt = new OrderCreatedEvent("order-1", 100m);

		// Assert
		evt.AggregateId.ShouldBe("order-1");
	}

	[Fact]
	public void Metadata_CanBeInitialized()
	{
		// Arrange
		var metadata = new Dictionary<string, object>
		{
			["source"] = "test",
			["correlationId"] = "corr-1",
		};

		// Act
		var evt = new TestDomainEvent { Metadata = metadata };

		// Assert
		evt.Metadata.ShouldNotBeNull();
		evt.Metadata!.Count.ShouldBe(2);
		evt.Metadata["source"].ShouldBe("test");
	}

	[Fact]
	public void Version_CanBeSet()
	{
		// Arrange & Act
		var evt = new TestDomainEvent { Version = 42 };

		// Assert
		evt.Version.ShouldBe(42);
	}

	[Fact]
	public void OccurredAt_CanBeOverridden()
	{
		// Arrange
		var fixedTime = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

		// Act
		var evt = new TestDomainEvent { OccurredAt = fixedTime };

		// Assert
		evt.OccurredAt.ShouldBe(fixedTime);
	}

	[Fact]
	public void EventId_CanBeOverridden()
	{
		// Arrange & Act
		var evt = new TestDomainEvent { EventId = "custom-id" };

		// Assert
		evt.EventId.ShouldBe("custom-id");
	}

	[Fact]
	public void TwoInstances_HaveDifferentEventIds()
	{
		// Arrange & Act
		var evt1 = new TestDomainEvent();
		var evt2 = new TestDomainEvent();

		// Assert
		evt1.EventId.ShouldNotBe(evt2.EventId);
	}

	[Fact]
	public void RecordEquality_WorksWithOverriddenProperties()
	{
		// Arrange
		var evt = new OrderCreatedEvent("order-1", 100m) { EventId = "fixed-id", Version = 1 };
		var copy = evt with { Total = 200m };

		// Assert
		copy.AggregateId.ShouldBe("order-1");
		copy.Total.ShouldBe(200m);
		copy.EventId.ShouldBe("fixed-id");
		copy.Version.ShouldBe(1);
	}

	private sealed record TestDomainEvent : DomainEventBase;

	private sealed record OrderCreatedEvent(string OrderId, decimal Total) : DomainEventBase
	{
		public override string AggregateId => OrderId;
	}
}
