// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA2012 // Use ValueTasks correctly

using Excalibur.EventSourcing.Queries;

namespace Excalibur.EventSourcing.Tests.Core.Queries;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class EventStoreTimeTravelQueryDepthShould
{
	private readonly IEventStore _eventStore = A.Fake<IEventStore>();
	private readonly EventStoreTimeTravelQuery _sut;

	public EventStoreTimeTravelQueryDepthShould()
	{
		_sut = new EventStoreTimeTravelQuery(_eventStore);
	}

	[Fact]
	public void ThrowWhenEventStoreIsNull()
	{
		Should.Throw<ArgumentNullException>(() => new EventStoreTimeTravelQuery(null!));
	}

	[Fact]
	public async Task GetEventsAtPointInTimeFiltersCorrectly()
	{
		// Arrange
		var baseTime = DateTimeOffset.UtcNow.AddHours(-3);
		var events = new List<StoredEvent>
		{
			new("e1", "agg-1", "Test", "Evt", "d"u8.ToArray(), null, 0, baseTime, false),
			new("e2", "agg-1", "Test", "Evt", "d"u8.ToArray(), null, 1, baseTime.AddHours(1), false),
			new("e3", "agg-1", "Test", "Evt", "d"u8.ToArray(), null, 2, baseTime.AddHours(2), false),
		};

		A.CallTo(() => _eventStore.LoadAsync("agg-1", "Test", A<CancellationToken>._))
			.Returns(new ValueTask<IReadOnlyList<StoredEvent>>(events));

		// Act - point in time between event 2 and event 3
		var result = await _sut.GetEventsAtPointInTimeAsync(
			"agg-1", "Test", baseTime.AddMinutes(90), CancellationToken.None);

		// Assert - should only include events at or before the point in time
		result.Count.ShouldBe(2);
		result[0].EventId.ShouldBe("e1");
		result[1].EventId.ShouldBe("e2");
	}

	[Fact]
	public async Task GetEventsAtPointInTimeReturnsEmptyForPastFilter()
	{
		// Arrange
		var events = new List<StoredEvent>
		{
			new("e1", "agg-1", "Test", "Evt", "d"u8.ToArray(), null, 0, DateTimeOffset.UtcNow, false),
		};

		A.CallTo(() => _eventStore.LoadAsync("agg-1", "Test", A<CancellationToken>._))
			.Returns(new ValueTask<IReadOnlyList<StoredEvent>>(events));

		// Act - point in time far in the past
		var result = await _sut.GetEventsAtPointInTimeAsync(
			"agg-1", "Test", DateTimeOffset.UtcNow.AddYears(-10), CancellationToken.None);

		// Assert
		result.ShouldBeEmpty();
	}

	[Fact]
	public async Task GetEventsAtPointInTimeReturnsAllWhenFutureFilter()
	{
		// Arrange
		var events = new List<StoredEvent>
		{
			new("e1", "agg-1", "Test", "Evt", "d"u8.ToArray(), null, 0, DateTimeOffset.UtcNow, false),
			new("e2", "agg-1", "Test", "Evt", "d"u8.ToArray(), null, 1, DateTimeOffset.UtcNow, false),
		};

		A.CallTo(() => _eventStore.LoadAsync("agg-1", "Test", A<CancellationToken>._))
			.Returns(new ValueTask<IReadOnlyList<StoredEvent>>(events));

		// Act - far future
		var result = await _sut.GetEventsAtPointInTimeAsync(
			"agg-1", "Test", DateTimeOffset.UtcNow.AddYears(10), CancellationToken.None);

		// Assert
		result.Count.ShouldBe(2);
	}

	[Fact]
	public async Task GetEventsAtPointInTimeThrowsWhenAggregateIdIsNull()
	{
		await Should.ThrowAsync<ArgumentException>(async () =>
			await _sut.GetEventsAtPointInTimeAsync(null!, "Test", DateTimeOffset.UtcNow, CancellationToken.None));
	}

	[Fact]
	public async Task GetEventsAtPointInTimeThrowsWhenAggregateTypeIsEmpty()
	{
		await Should.ThrowAsync<ArgumentException>(async () =>
			await _sut.GetEventsAtPointInTimeAsync("agg-1", "", DateTimeOffset.UtcNow, CancellationToken.None));
	}

	[Fact]
	public async Task GetEventsAtVersionFiltersCorrectly()
	{
		// Arrange
		var events = new List<StoredEvent>
		{
			new("e1", "agg-1", "Test", "Evt", "d"u8.ToArray(), null, 0, DateTimeOffset.UtcNow, false),
			new("e2", "agg-1", "Test", "Evt", "d"u8.ToArray(), null, 1, DateTimeOffset.UtcNow, false),
			new("e3", "agg-1", "Test", "Evt", "d"u8.ToArray(), null, 2, DateTimeOffset.UtcNow, false),
			new("e4", "agg-1", "Test", "Evt", "d"u8.ToArray(), null, 3, DateTimeOffset.UtcNow, false),
		};

		A.CallTo(() => _eventStore.LoadAsync("agg-1", "Test", A<CancellationToken>._))
			.Returns(new ValueTask<IReadOnlyList<StoredEvent>>(events));

		// Act
		var result = await _sut.GetEventsAtVersionAsync("agg-1", "Test", 1, CancellationToken.None);

		// Assert - should include versions 0 and 1
		result.Count.ShouldBe(2);
		result[0].Version.ShouldBe(0);
		result[1].Version.ShouldBe(1);
	}

	[Fact]
	public async Task GetEventsAtVersionReturnsAllWhenVersionIsHigherThanMax()
	{
		// Arrange
		var events = new List<StoredEvent>
		{
			new("e1", "agg-1", "Test", "Evt", "d"u8.ToArray(), null, 0, DateTimeOffset.UtcNow, false),
			new("e2", "agg-1", "Test", "Evt", "d"u8.ToArray(), null, 1, DateTimeOffset.UtcNow, false),
		};

		A.CallTo(() => _eventStore.LoadAsync("agg-1", "Test", A<CancellationToken>._))
			.Returns(new ValueTask<IReadOnlyList<StoredEvent>>(events));

		// Act
		var result = await _sut.GetEventsAtVersionAsync("agg-1", "Test", 100, CancellationToken.None);

		// Assert
		result.Count.ShouldBe(2);
	}

	[Fact]
	public async Task GetEventsAtVersionThrowsWhenAggregateIdIsEmpty()
	{
		await Should.ThrowAsync<ArgumentException>(async () =>
			await _sut.GetEventsAtVersionAsync("", "Test", 0, CancellationToken.None));
	}

	[Fact]
	public async Task GetEventsAtVersionThrowsWhenAggregateTypeIsNull()
	{
		await Should.ThrowAsync<ArgumentException>(async () =>
			await _sut.GetEventsAtVersionAsync("agg-1", null!, 0, CancellationToken.None));
	}

	[Fact]
	public async Task GetEventsInTimeRangeFiltersCorrectly()
	{
		// Arrange
		var baseTime = DateTimeOffset.UtcNow.AddHours(-5);
		var events = new List<StoredEvent>
		{
			new("e1", "agg-1", "Test", "Evt", "d"u8.ToArray(), null, 0, baseTime, false),
			new("e2", "agg-1", "Test", "Evt", "d"u8.ToArray(), null, 1, baseTime.AddHours(1), false),
			new("e3", "agg-1", "Test", "Evt", "d"u8.ToArray(), null, 2, baseTime.AddHours(2), false),
			new("e4", "agg-1", "Test", "Evt", "d"u8.ToArray(), null, 3, baseTime.AddHours(3), false),
			new("e5", "agg-1", "Test", "Evt", "d"u8.ToArray(), null, 4, baseTime.AddHours(4), false),
		};

		A.CallTo(() => _eventStore.LoadAsync("agg-1", "Test", A<CancellationToken>._))
			.Returns(new ValueTask<IReadOnlyList<StoredEvent>>(events));

		// Act - range covering events 2, 3, 4 (hours 1-3)
		var result = await _sut.GetEventsInTimeRangeAsync(
			"agg-1", "Test",
			baseTime.AddHours(1),
			baseTime.AddHours(3),
			CancellationToken.None);

		// Assert
		result.Count.ShouldBe(3);
		result[0].EventId.ShouldBe("e2");
		result[1].EventId.ShouldBe("e3");
		result[2].EventId.ShouldBe("e4");
	}

	[Fact]
	public async Task GetEventsInTimeRangeReturnsEmptyForNonOverlappingRange()
	{
		// Arrange
		var events = new List<StoredEvent>
		{
			new("e1", "agg-1", "Test", "Evt", "d"u8.ToArray(), null, 0, DateTimeOffset.UtcNow, false),
		};

		A.CallTo(() => _eventStore.LoadAsync("agg-1", "Test", A<CancellationToken>._))
			.Returns(new ValueTask<IReadOnlyList<StoredEvent>>(events));

		// Act - range in the distant past
		var result = await _sut.GetEventsInTimeRangeAsync(
			"agg-1", "Test",
			DateTimeOffset.UtcNow.AddYears(-10),
			DateTimeOffset.UtcNow.AddYears(-9),
			CancellationToken.None);

		// Assert
		result.ShouldBeEmpty();
	}

	[Fact]
	public async Task GetEventsInTimeRangeThrowsWhenAggregateIdIsNull()
	{
		await Should.ThrowAsync<ArgumentException>(async () =>
			await _sut.GetEventsInTimeRangeAsync(
				null!, "Test", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, CancellationToken.None));
	}

	[Fact]
	public async Task GetEventsInTimeRangeThrowsWhenAggregateTypeIsEmpty()
	{
		await Should.ThrowAsync<ArgumentException>(async () =>
			await _sut.GetEventsInTimeRangeAsync(
				"agg-1", "", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, CancellationToken.None));
	}

	[Fact]
	public async Task GetEventsAtPointInTimeReturnsEmptyWhenNoEventsExist()
	{
		// Arrange
		A.CallTo(() => _eventStore.LoadAsync("agg-1", "Test", A<CancellationToken>._))
			.Returns(new ValueTask<IReadOnlyList<StoredEvent>>(Array.Empty<StoredEvent>()));

		// Act
		var result = await _sut.GetEventsAtPointInTimeAsync(
			"agg-1", "Test", DateTimeOffset.UtcNow, CancellationToken.None);

		// Assert
		result.ShouldBeEmpty();
	}

	[Fact]
	public async Task GetEventsInTimeRangeReturnsEmptyWhenNoEventsExist()
	{
		// Arrange
		A.CallTo(() => _eventStore.LoadAsync("agg-1", "Test", A<CancellationToken>._))
			.Returns(new ValueTask<IReadOnlyList<StoredEvent>>(Array.Empty<StoredEvent>()));

		// Act
		var result = await _sut.GetEventsInTimeRangeAsync(
			"agg-1", "Test", DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow, CancellationToken.None);

		// Assert
		result.ShouldBeEmpty();
	}
}

#pragma warning restore CA2012
