// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;

using Excalibur.Domain.Model;
using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.Diagnostics;

namespace Excalibur.EventSourcing.Tests.Core.Diagnostics;

[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
public sealed class TelemetrySnapshotStoreShould : IDisposable
{
	private readonly ISnapshotStore _inner;
	private readonly Meter _meter;
	private readonly ActivitySource _activitySource;
	private readonly TelemetrySnapshotStore _sut;

	public TelemetrySnapshotStoreShould()
	{
		_inner = A.Fake<ISnapshotStore>();
		_meter = new Meter("test.eventsourcing.snapshot.telemetry");
		_activitySource = new ActivitySource("test.eventsourcing.snapshot");
		_sut = new TelemetrySnapshotStore(_inner, _meter, _activitySource, "test-provider");
	}

	public void Dispose()
	{
		_meter.Dispose();
		_activitySource.Dispose();
	}

	[Fact]
	public async Task GetLatestSnapshotAsync_DelegateToInnerAndReturnResult()
	{
		// Arrange
		var snapshot = A.Fake<ISnapshot>();
#pragma warning disable CA2012
		A.CallTo(() => _inner.GetLatestSnapshotAsync("agg-1", "Order", A<CancellationToken>._))
			.Returns(new ValueTask<ISnapshot?>(snapshot));
#pragma warning restore CA2012

		// Act
		var result = await _sut.GetLatestSnapshotAsync("agg-1", "Order", CancellationToken.None);

		// Assert
		result.ShouldBeSameAs(snapshot);
	}

	[Fact]
	public async Task GetLatestSnapshotAsync_ReturnNull_WhenInnerReturnsNull()
	{
		// Arrange
#pragma warning disable CA2012
		A.CallTo(() => _inner.GetLatestSnapshotAsync("agg-1", "Order", A<CancellationToken>._))
			.Returns(new ValueTask<ISnapshot?>((ISnapshot?)null));
#pragma warning restore CA2012

		// Act
		var result = await _sut.GetLatestSnapshotAsync("agg-1", "Order", CancellationToken.None);

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public async Task GetLatestSnapshotAsync_PropagateException()
	{
		// Arrange
#pragma warning disable CA2012
		A.CallTo(() => _inner.GetLatestSnapshotAsync("agg-1", "Order", A<CancellationToken>._))
			.Returns(new ValueTask<ISnapshot?>(
				Task.FromException<ISnapshot?>(new TimeoutException("timeout"))));
#pragma warning restore CA2012

		// Act & Assert
		await Should.ThrowAsync<TimeoutException>(
			() => _sut.GetLatestSnapshotAsync("agg-1", "Order", CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task SaveSnapshotAsync_DelegateToInner()
	{
		// Arrange
		var snapshot = CreateSnapshot("agg-1");

		// Act
		await _sut.SaveSnapshotAsync(snapshot, CancellationToken.None);

		// Assert
		A.CallTo(() => _inner.SaveSnapshotAsync(snapshot, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task SaveSnapshotAsync_ThrowOnNull()
	{
		await Should.ThrowAsync<ArgumentNullException>(
			() => _sut.SaveSnapshotAsync(null!, CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task SaveSnapshotAsync_PropagateException()
	{
		// Arrange
		var snapshot = CreateSnapshot("agg-1");
#pragma warning disable CA2012
		A.CallTo(() => _inner.SaveSnapshotAsync(snapshot, A<CancellationToken>._))
			.Returns(new ValueTask(Task.FromException(new InvalidOperationException("save failed"))));
#pragma warning restore CA2012

		// Act & Assert
		await Should.ThrowAsync<InvalidOperationException>(
			() => _sut.SaveSnapshotAsync(snapshot, CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task DeleteSnapshotsAsync_DelegateToInner()
	{
		// Act
		await _sut.DeleteSnapshotsAsync("agg-1", "Order", CancellationToken.None);

		// Assert
		A.CallTo(() => _inner.DeleteSnapshotsAsync("agg-1", "Order", A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task DeleteSnapshotsAsync_PropagateException()
	{
		// Arrange
#pragma warning disable CA2012
		A.CallTo(() => _inner.DeleteSnapshotsAsync("agg-1", "Order", A<CancellationToken>._))
			.Returns(new ValueTask(Task.FromException(new InvalidOperationException("delete failed"))));
#pragma warning restore CA2012

		// Act & Assert
		await Should.ThrowAsync<InvalidOperationException>(
			() => _sut.DeleteSnapshotsAsync("agg-1", "Order", CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task DeleteSnapshotsOlderThanAsync_DelegateToInner()
	{
		// Act
		await _sut.DeleteSnapshotsOlderThanAsync("agg-1", "Order", 5L, CancellationToken.None);

		// Assert
		A.CallTo(() => _inner.DeleteSnapshotsOlderThanAsync("agg-1", "Order", 5L, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task DeleteSnapshotsOlderThanAsync_PropagateException()
	{
		// Arrange
#pragma warning disable CA2012
		A.CallTo(() => _inner.DeleteSnapshotsOlderThanAsync("agg-1", "Order", 5L, A<CancellationToken>._))
			.Returns(new ValueTask(Task.FromException(new InvalidOperationException("delete failed"))));
#pragma warning restore CA2012

		// Act & Assert
		await Should.ThrowAsync<InvalidOperationException>(
			() => _sut.DeleteSnapshotsOlderThanAsync("agg-1", "Order", 5L, CancellationToken.None).AsTask());
	}

	[Fact]
	public void ThrowOnNullConstructorArgs()
	{
		Should.Throw<ArgumentNullException>(() =>
			new TelemetrySnapshotStore(_inner, null!, _activitySource, "p"));
		Should.Throw<ArgumentNullException>(() =>
			new TelemetrySnapshotStore(_inner, _meter, null!, "p"));
		Should.Throw<ArgumentNullException>(() =>
			new TelemetrySnapshotStore(_inner, _meter, _activitySource, null!));
	}

	private static ISnapshot CreateSnapshot(string aggregateId)
	{
		var snapshot = A.Fake<ISnapshot>();
		A.CallTo(() => snapshot.AggregateId).Returns(aggregateId);
		A.CallTo(() => snapshot.AggregateType).Returns("Order");
		A.CallTo(() => snapshot.Version).Returns(1);
		A.CallTo(() => snapshot.Data).Returns(Array.Empty<byte>());
		return snapshot;
	}
}
