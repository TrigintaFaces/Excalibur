// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.Queries;

namespace Excalibur.EventSourcing.Tests.Core.Queries;

[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
public sealed class EventStoreTimeTravelQueryShould
{
	private readonly IEventStore _eventStore;
	private readonly EventStoreTimeTravelQuery _sut;

	public EventStoreTimeTravelQueryShould()
	{
		_eventStore = A.Fake<IEventStore>();
		_sut = new EventStoreTimeTravelQuery(_eventStore);
	}

	[Fact]
	public async Task GetEventsAtPointInTime_FilterByTimestamp()
	{
		// Arrange
		var baseTime = DateTimeOffset.UtcNow;
		var events = new List<StoredEvent>
		{
			CreateEvent("e1", 1, baseTime.AddMinutes(-10)),
			CreateEvent("e2", 2, baseTime.AddMinutes(-5)),
			CreateEvent("e3", 3, baseTime.AddMinutes(5)),
		};

		SetupLoad(events);

		// Act
		var result = await _sut.GetEventsAtPointInTimeAsync(
			"agg-1", "Type", baseTime, CancellationToken.None);

		// Assert
		result.Count.ShouldBe(2);
		result[0].EventId.ShouldBe("e1");
		result[1].EventId.ShouldBe("e2");
	}

	[Fact]
	public async Task GetEventsAtPointInTime_ReturnEmpty_WhenAllEventsAreAfterPoint()
	{
		// Arrange
		var baseTime = DateTimeOffset.UtcNow;
		var events = new List<StoredEvent>
		{
			CreateEvent("e1", 1, baseTime.AddMinutes(10)),
		};

		SetupLoad(events);

		// Act
		var result = await _sut.GetEventsAtPointInTimeAsync(
			"agg-1", "Type", baseTime, CancellationToken.None);

		// Assert
		result.ShouldBeEmpty();
	}

	[Fact]
	public async Task GetEventsAtVersion_FilterByVersion()
	{
		// Arrange
		var events = new List<StoredEvent>
		{
			CreateEvent("e1", 1, DateTimeOffset.UtcNow),
			CreateEvent("e2", 2, DateTimeOffset.UtcNow),
			CreateEvent("e3", 3, DateTimeOffset.UtcNow),
			CreateEvent("e4", 4, DateTimeOffset.UtcNow),
		};

		SetupLoad(events);

		// Act
		var result = await _sut.GetEventsAtVersionAsync("agg-1", "Type", 2, CancellationToken.None);

		// Assert
		result.Count.ShouldBe(2);
		result[0].Version.ShouldBe(1);
		result[1].Version.ShouldBe(2);
	}

	[Fact]
	public async Task GetEventsInTimeRange_FilterByRange()
	{
		// Arrange
		var baseTime = DateTimeOffset.UtcNow;
		var events = new List<StoredEvent>
		{
			CreateEvent("e1", 1, baseTime.AddMinutes(-20)),
			CreateEvent("e2", 2, baseTime.AddMinutes(-10)),
			CreateEvent("e3", 3, baseTime.AddMinutes(-5)),
			CreateEvent("e4", 4, baseTime.AddMinutes(5)),
		};

		SetupLoad(events);

		// Act
		var result = await _sut.GetEventsInTimeRangeAsync(
			"agg-1", "Type",
			baseTime.AddMinutes(-15),
			baseTime,
			CancellationToken.None);

		// Assert
		result.Count.ShouldBe(2);
		result[0].EventId.ShouldBe("e2");
		result[1].EventId.ShouldBe("e3");
	}

	[Fact]
	public async Task ThrowOnNullOrEmptyArgs()
	{
		var time = DateTimeOffset.UtcNow;

		await Should.ThrowAsync<ArgumentException>(
			() => _sut.GetEventsAtPointInTimeAsync(null!, "Type", time, CancellationToken.None).AsTask());
		await Should.ThrowAsync<ArgumentException>(
			() => _sut.GetEventsAtPointInTimeAsync("id", null!, time, CancellationToken.None).AsTask());
		await Should.ThrowAsync<ArgumentException>(
			() => _sut.GetEventsAtVersionAsync(null!, "Type", 1, CancellationToken.None).AsTask());
		await Should.ThrowAsync<ArgumentException>(
			() => _sut.GetEventsAtVersionAsync("id", null!, 1, CancellationToken.None).AsTask());
		await Should.ThrowAsync<ArgumentException>(
			() => _sut.GetEventsInTimeRangeAsync(null!, "Type", time, time, CancellationToken.None).AsTask());
		await Should.ThrowAsync<ArgumentException>(
			() => _sut.GetEventsInTimeRangeAsync("id", null!, time, time, CancellationToken.None).AsTask());
	}

	[Fact]
	public void ThrowOnNullEventStore()
	{
		Should.Throw<ArgumentNullException>(() => new EventStoreTimeTravelQuery(null!));
	}

	private void SetupLoad(List<StoredEvent> events)
	{
#pragma warning disable CA2012
		A.CallTo(() => _eventStore.LoadAsync("agg-1", "Type", A<CancellationToken>._))
			.Returns(new ValueTask<IReadOnlyList<StoredEvent>>(events));
#pragma warning restore CA2012
	}

	private static StoredEvent CreateEvent(string eventId, long version, DateTimeOffset timestamp) =>
		new(eventId, "agg-1", "Type", "TestEvent", Array.Empty<byte>(), null, version, timestamp, false);
}
