// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.Queries;

using FakeItEasy;

using Shouldly;

using Xunit;

namespace Excalibur.EventSourcing.Tests.Core.Queries;

/// <summary>
/// Functional tests for <see cref="EventStoreTimeTravelQuery"/> covering
/// point-in-time, version-based, and time-range queries.
/// </summary>
[Trait("Category", "Unit")]
public sealed class TimeTravelQueryFunctionalShould
{
	private readonly IEventStore _eventStore = A.Fake<IEventStore>();
	private readonly EventStoreTimeTravelQuery _sut;

	private static readonly DateTimeOffset BaseTime = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

	public TimeTravelQueryFunctionalShould()
	{
		_sut = new EventStoreTimeTravelQuery(_eventStore);
	}

	private static StoredEvent CreateEvent(long version, int minutesOffset)
	{
		return new StoredEvent(
			EventId: Guid.NewGuid().ToString(),
			AggregateId: "agg-1",
			AggregateType: "Order",
			EventType: $"Event{version}",
			EventData: [],
			Metadata: null,
			Version: version,
			Timestamp: BaseTime.AddMinutes(minutesOffset),
			IsDispatched: false);
	}

	[Fact]
	public async Task GetEventsAtPointInTime_ShouldReturnEventsBeforeTimestamp()
	{
		// Arrange
		var events = new List<StoredEvent>
		{
			CreateEvent(0, 0),   // BaseTime
			CreateEvent(1, 10),  // BaseTime + 10min
			CreateEvent(2, 20),  // BaseTime + 20min
			CreateEvent(3, 30),  // BaseTime + 30min
		};
		A.CallTo(() => _eventStore.LoadAsync("agg-1", "Order", A<CancellationToken>._))
			.Returns(events);

		// Act - get events at 15 minutes after base time
		var result = await _sut.GetEventsAtPointInTimeAsync("agg-1", "Order", BaseTime.AddMinutes(15), CancellationToken.None);

		// Assert - should return events at minutes 0 and 10
		result.Count.ShouldBe(2);
		result[0].Version.ShouldBe(0);
		result[1].Version.ShouldBe(1);
	}

	[Fact]
	public async Task GetEventsAtVersion_ShouldReturnEventsUpToVersion()
	{
		// Arrange
		var events = new List<StoredEvent>
		{
			CreateEvent(0, 0),
			CreateEvent(1, 10),
			CreateEvent(2, 20),
			CreateEvent(3, 30),
		};
		A.CallTo(() => _eventStore.LoadAsync("agg-1", "Order", A<CancellationToken>._))
			.Returns(events);

		// Act
		var result = await _sut.GetEventsAtVersionAsync("agg-1", "Order", 2, CancellationToken.None);

		// Assert - should return versions 0, 1, 2
		result.Count.ShouldBe(3);
		result.All(e => e.Version <= 2).ShouldBeTrue();
	}

	[Fact]
	public async Task GetEventsInTimeRange_ShouldReturnEventsWithinRange()
	{
		// Arrange
		var events = new List<StoredEvent>
		{
			CreateEvent(0, 0),   // excluded
			CreateEvent(1, 10),  // included
			CreateEvent(2, 20),  // included
			CreateEvent(3, 30),  // excluded
		};
		A.CallTo(() => _eventStore.LoadAsync("agg-1", "Order", A<CancellationToken>._))
			.Returns(events);

		// Act - range from 5min to 25min
		var result = await _sut.GetEventsInTimeRangeAsync(
			"agg-1", "Order",
			BaseTime.AddMinutes(5), BaseTime.AddMinutes(25),
			CancellationToken.None);

		// Assert
		result.Count.ShouldBe(2);
		result[0].Version.ShouldBe(1);
		result[1].Version.ShouldBe(2);
	}

	[Fact]
	public async Task GetEventsAtPointInTime_WithEmptyStore_ShouldReturnEmpty()
	{
		// Arrange
		A.CallTo(() => _eventStore.LoadAsync("agg-1", "Order", A<CancellationToken>._))
			.Returns(new List<StoredEvent>());

		// Act
		var result = await _sut.GetEventsAtPointInTimeAsync("agg-1", "Order", DateTimeOffset.UtcNow, CancellationToken.None);

		// Assert
		result.ShouldBeEmpty();
	}

	[Fact]
	public async Task GetEventsAtPointInTime_ExactTimestamp_ShouldInclude()
	{
		// Arrange
		var events = new List<StoredEvent>
		{
			CreateEvent(0, 10),
		};
		A.CallTo(() => _eventStore.LoadAsync("agg-1", "Order", A<CancellationToken>._))
			.Returns(events);

		// Act - exact same timestamp
		var result = await _sut.GetEventsAtPointInTimeAsync("agg-1", "Order", BaseTime.AddMinutes(10), CancellationToken.None);

		// Assert - should include the event (<=)
		result.Count.ShouldBe(1);
	}

	[Fact]
	public void Constructor_ShouldThrowOnNullEventStore()
	{
		Should.Throw<ArgumentNullException>(() => new EventStoreTimeTravelQuery(null!));
	}

	[Fact]
	public async Task GetEventsAtPointInTime_ShouldThrowOnEmptyAggregateId()
	{
		await Should.ThrowAsync<ArgumentException>(async () =>
			await _sut.GetEventsAtPointInTimeAsync("", "Order", DateTimeOffset.UtcNow, CancellationToken.None));
	}

	[Fact]
	public async Task GetEventsAtVersion_ShouldThrowOnEmptyAggregateId()
	{
		await Should.ThrowAsync<ArgumentException>(async () =>
			await _sut.GetEventsAtVersionAsync("", "Order", 0, CancellationToken.None));
	}

	[Fact]
	public async Task GetEventsInTimeRange_ShouldThrowOnEmptyAggregateType()
	{
		await Should.ThrowAsync<ArgumentException>(async () =>
			await _sut.GetEventsInTimeRangeAsync("agg-1", "", DateTimeOffset.MinValue, DateTimeOffset.MaxValue, CancellationToken.None));
	}
}
