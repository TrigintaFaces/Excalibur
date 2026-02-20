// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.EventSourcing.Projections;

namespace Excalibur.EventSourcing.Tests.Core.Projections;

internal sealed class MultiStreamTestState
{
	public int Counter { get; set; }
}

/// <summary>
/// Concrete event type for testing MultiStreamProjection.Apply handler dispatch.
/// FakeItEasy proxies have different GetType() than the registered interface,
/// so we need a concrete type whose GetType() matches the handler registration key.
/// </summary>
public sealed class MultiStreamConcreteTestEvent : IDomainEvent
{
	public string EventId { get; init; } = Guid.NewGuid().ToString();
	public string AggregateId { get; init; } = "test-agg";
	public long Version { get; init; }
	public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
	public string EventType { get; init; } = nameof(MultiStreamConcreteTestEvent);
	public IDictionary<string, object>? Metadata { get; init; }
}

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class MultiStreamProjectionShould
{
	[Fact]
	public void StartWithEmptyStreamsAndCategories()
	{
		// Act
		var projection = new MultiStreamProjection<MultiStreamTestState>();

		// Assert
		projection.Streams.ShouldBeEmpty();
		projection.Categories.ShouldBeEmpty();
		projection.HandledEventTypes.ShouldBeEmpty();
	}

	[Fact]
	public void ApplyEventWhenHandlerRegisteredViaBuilder()
	{
		// Arrange - use builder to add handler (AddHandler is internal)
		var builder = new MultiStreamProjectionBuilder<MultiStreamTestState>();
		var projection = builder
			.When<MultiStreamConcreteTestEvent>((state, _) => state.Counter++)
			.Build();

		var state = new MultiStreamTestState();
		var evt = new MultiStreamConcreteTestEvent();

		// Act
		var result = projection.Apply(state, evt);

		// Assert
		result.ShouldBeTrue();
		state.Counter.ShouldBe(1);
	}

	[Fact]
	public void ReturnFalseWhenNoHandlerForEvent()
	{
		// Arrange
		var projection = new MultiStreamProjection<MultiStreamTestState>();
		var state = new MultiStreamTestState();
		var evt = new MultiStreamConcreteTestEvent();

		// Act
		var result = projection.Apply(state, evt);

		// Assert
		result.ShouldBeFalse();
		state.Counter.ShouldBe(0);
	}

	[Fact]
	public void ThrowWhenApplyProjectionIsNull()
	{
		// Arrange
		var projection = new MultiStreamProjection<MultiStreamTestState>();
		var evt = new MultiStreamConcreteTestEvent();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => projection.Apply(null!, evt));
	}

	[Fact]
	public void ThrowWhenApplyEventIsNull()
	{
		// Arrange
		var projection = new MultiStreamProjection<MultiStreamTestState>();
		var state = new MultiStreamTestState();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => projection.Apply(state, null!));
	}

	[Fact]
	public void TrackHandledEventTypesViaBuilder()
	{
		// Arrange - use builder to add handler (AddHandler is internal)
		var builder = new MultiStreamProjectionBuilder<MultiStreamTestState>();
		var projection = builder
			.When<MultiStreamConcreteTestEvent>((_, _) => { })
			.Build();

		// Assert
		projection.HandledEventTypes.ShouldContain(typeof(MultiStreamConcreteTestEvent));
		projection.HandledEventTypes.Count.ShouldBe(1);
	}
}
