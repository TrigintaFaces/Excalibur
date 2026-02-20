// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;

using Excalibur.Dispatch.Abstractions;
using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.Diagnostics;

namespace Excalibur.EventSourcing.Tests.Core.Diagnostics;

[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
public sealed class TelemetryEventStoreShould : IDisposable
{
	private readonly IEventStore _inner;
	private readonly Meter _meter;
	private readonly ActivitySource _activitySource;
	private readonly TelemetryEventStore _sut;

	public TelemetryEventStoreShould()
	{
		_inner = A.Fake<IEventStore>();
		_meter = new Meter("test.eventsourcing.telemetry");
		_activitySource = new ActivitySource("test.eventsourcing");
		_sut = new TelemetryEventStore(_inner, _meter, _activitySource, "test-provider");
	}

	public void Dispose()
	{
		_meter.Dispose();
		_activitySource.Dispose();
	}

	[Fact]
	public async Task LoadAsync_DelegateToInnerAndRecordMetrics()
	{
		// Arrange
		var expected = new List<StoredEvent> { CreateStoredEvent() };
#pragma warning disable CA2012
		A.CallTo(() => _inner.LoadAsync("agg-1", "Order", A<CancellationToken>._))
			.Returns(new ValueTask<IReadOnlyList<StoredEvent>>(expected));
#pragma warning restore CA2012

		// Act
		var result = await _sut.LoadAsync("agg-1", "Order", CancellationToken.None);

		// Assert
		result.ShouldBeSameAs(expected);
		A.CallTo(() => _inner.LoadAsync("agg-1", "Order", A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task LoadAsync_WithVersion_DelegateToInner()
	{
		// Arrange
		var expected = new List<StoredEvent> { CreateStoredEvent() };
#pragma warning disable CA2012
		A.CallTo(() => _inner.LoadAsync("agg-1", "Order", 5L, A<CancellationToken>._))
			.Returns(new ValueTask<IReadOnlyList<StoredEvent>>(expected));
#pragma warning restore CA2012

		// Act
		var result = await _sut.LoadAsync("agg-1", "Order", 5L, CancellationToken.None);

		// Assert
		result.ShouldBeSameAs(expected);
	}

	[Fact]
	public async Task LoadAsync_PropagateExceptionFromInner()
	{
		// Arrange
#pragma warning disable CA2012
		A.CallTo(() => _inner.LoadAsync("agg-1", "Order", A<CancellationToken>._))
			.Returns(new ValueTask<IReadOnlyList<StoredEvent>>(
				Task.FromException<IReadOnlyList<StoredEvent>>(new InvalidOperationException("store error"))));
#pragma warning restore CA2012

		// Act & Assert
		await Should.ThrowAsync<InvalidOperationException>(
			() => _sut.LoadAsync("agg-1", "Order", CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task AppendAsync_DelegateToInner()
	{
		// Arrange
		var events = Array.Empty<IDomainEvent>();
		var expected = AppendResult.CreateSuccess(1, 0);
#pragma warning disable CA2012
		A.CallTo(() => _inner.AppendAsync("agg-1", "Order", events, 0L, A<CancellationToken>._))
			.Returns(new ValueTask<AppendResult>(expected));
#pragma warning restore CA2012

		// Act
		var result = await _sut.AppendAsync("agg-1", "Order", events, 0L, CancellationToken.None);

		// Assert
		result.ShouldBe(expected);
	}

	[Fact]
	public async Task AppendAsync_PropagateExceptionFromInner()
	{
		// Arrange
		var events = Array.Empty<IDomainEvent>();
#pragma warning disable CA2012
		A.CallTo(() => _inner.AppendAsync("agg-1", "Order", events, 0L, A<CancellationToken>._))
			.Returns(new ValueTask<AppendResult>(
				Task.FromException<AppendResult>(new InvalidOperationException("concurrency"))));
#pragma warning restore CA2012

		// Act & Assert
		await Should.ThrowAsync<InvalidOperationException>(
			() => _sut.AppendAsync("agg-1", "Order", events, 0L, CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task GetUndispatchedEventsAsync_DelegateToInner()
	{
		// Arrange
		var expected = new List<StoredEvent> { CreateStoredEvent() };
#pragma warning disable CA2012
		A.CallTo(() => _inner.GetUndispatchedEventsAsync(100, A<CancellationToken>._))
			.Returns(new ValueTask<IReadOnlyList<StoredEvent>>(expected));
#pragma warning restore CA2012

		// Act
		var result = await _sut.GetUndispatchedEventsAsync(100, CancellationToken.None);

		// Assert
		result.ShouldBeSameAs(expected);
	}

	[Fact]
	public async Task GetUndispatchedEventsAsync_PropagateException()
	{
		// Arrange
#pragma warning disable CA2012
		A.CallTo(() => _inner.GetUndispatchedEventsAsync(100, A<CancellationToken>._))
			.Returns(new ValueTask<IReadOnlyList<StoredEvent>>(
				Task.FromException<IReadOnlyList<StoredEvent>>(new TimeoutException())));
#pragma warning restore CA2012

		// Act & Assert
		await Should.ThrowAsync<TimeoutException>(
			() => _sut.GetUndispatchedEventsAsync(100, CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task MarkEventAsDispatchedAsync_DelegateToInner()
	{
		// Act
		await _sut.MarkEventAsDispatchedAsync("event-1", CancellationToken.None);

		// Assert
		A.CallTo(() => _inner.MarkEventAsDispatchedAsync("event-1", A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task MarkEventAsDispatchedAsync_PropagateException()
	{
		// Arrange
#pragma warning disable CA2012
		A.CallTo(() => _inner.MarkEventAsDispatchedAsync("event-1", A<CancellationToken>._))
			.Returns(new ValueTask(Task.FromException(new InvalidOperationException("mark failed"))));
#pragma warning restore CA2012

		// Act & Assert
		await Should.ThrowAsync<InvalidOperationException>(
			() => _sut.MarkEventAsDispatchedAsync("event-1", CancellationToken.None).AsTask());
	}

	[Fact]
	public void ThrowOnNullConstructorArgs()
	{
		Should.Throw<ArgumentNullException>(() =>
			new TelemetryEventStore(_inner, null!, _activitySource, "p"));
		Should.Throw<ArgumentNullException>(() =>
			new TelemetryEventStore(_inner, _meter, null!, "p"));
		Should.Throw<ArgumentNullException>(() =>
			new TelemetryEventStore(_inner, _meter, _activitySource, null!));
	}

	private static StoredEvent CreateStoredEvent() =>
		new(
			Guid.NewGuid().ToString(),
			"agg-1",
			"Order",
			"OrderCreated",
			Array.Empty<byte>(),
			null,
			1,
			DateTimeOffset.UtcNow,
			false);
}
