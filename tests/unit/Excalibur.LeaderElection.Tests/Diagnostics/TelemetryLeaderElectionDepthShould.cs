// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;

using Excalibur.LeaderElection.Diagnostics;

namespace Excalibur.LeaderElection.Tests.Diagnostics;

/// <summary>
/// Depth coverage tests for <see cref="TelemetryLeaderElection"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class TelemetryLeaderElectionDepthShould : IDisposable
{
	private readonly ILeaderElection _inner;
	private readonly Meter _meter;
	private readonly ActivitySource _activitySource;

	public TelemetryLeaderElectionDepthShould()
	{
		_inner = A.Fake<ILeaderElection>();
		A.CallTo(() => _inner.CandidateId).Returns("candidate-1");
		A.CallTo(() => _inner.IsLeader).Returns(false);
		A.CallTo(() => _inner.CurrentLeaderId).Returns((string?)null);
		_meter = new Meter("Test.LeaderElection.Depth", "1.0");
		_activitySource = new ActivitySource("Test.LeaderElection.Depth", "1.0");
	}

	[Fact]
	public void ThrowWhenInnerIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new TelemetryLeaderElection(null!, _meter, _activitySource, "test"));
	}

	[Fact]
	public void ThrowWhenMeterIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new TelemetryLeaderElection(_inner, null!, _activitySource, "test"));
	}

	[Fact]
	public void ThrowWhenActivitySourceIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new TelemetryLeaderElection(_inner, _meter, null!, "test"));
	}

	[Fact]
	public void ThrowWhenProviderNameIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new TelemetryLeaderElection(_inner, _meter, _activitySource, null!));
	}

	[Fact]
	public void DelegateCandidateIdToInner()
	{
		// Arrange
		A.CallTo(() => _inner.CandidateId).Returns("my-candidate");
		var sut = new TelemetryLeaderElection(_inner, _meter, _activitySource, "test");

		// Act
		var result = sut.CandidateId;

		// Assert
		result.ShouldBe("my-candidate");
	}

	[Fact]
	public void DelegateIsLeaderToInner()
	{
		// Arrange
		A.CallTo(() => _inner.IsLeader).Returns(true);
		var sut = new TelemetryLeaderElection(_inner, _meter, _activitySource, "test");

		// Act
		var result = sut.IsLeader;

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void DelegateCurrentLeaderIdToInner()
	{
		// Arrange
		A.CallTo(() => _inner.CurrentLeaderId).Returns("leader-xyz");
		var sut = new TelemetryLeaderElection(_inner, _meter, _activitySource, "test");

		// Act
		var result = sut.CurrentLeaderId;

		// Assert
		result.ShouldBe("leader-xyz");
	}

	[Fact]
	public async Task DelegateStartAsyncToInner()
	{
		// Arrange
		var sut = new TelemetryLeaderElection(_inner, _meter, _activitySource, "test");

		// Act
		await sut.StartAsync(CancellationToken.None);

		// Assert
		A.CallTo(() => _inner.StartAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task DelegateStopAsyncToInner()
	{
		// Arrange
		var sut = new TelemetryLeaderElection(_inner, _meter, _activitySource, "test");

		// Act
		await sut.StopAsync(CancellationToken.None);

		// Assert
		A.CallTo(() => _inner.StopAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task PropagateStartAsyncException()
	{
		// Arrange
		A.CallTo(() => _inner.StartAsync(A<CancellationToken>._))
			.ThrowsAsync(new InvalidOperationException("start failed"));
		var sut = new TelemetryLeaderElection(_inner, _meter, _activitySource, "test");

		// Act & Assert
		await Should.ThrowAsync<InvalidOperationException>(() => sut.StartAsync(CancellationToken.None));
	}

	[Fact]
	public async Task PropagateStopAsyncException()
	{
		// Arrange
		A.CallTo(() => _inner.StopAsync(A<CancellationToken>._))
			.ThrowsAsync(new InvalidOperationException("stop failed"));
		var sut = new TelemetryLeaderElection(_inner, _meter, _activitySource, "test");

		// Act & Assert
		await Should.ThrowAsync<InvalidOperationException>(() => sut.StopAsync(CancellationToken.None));
	}

	[Fact]
	public async Task ForwardBecameLeaderEvent()
	{
		// Arrange
		var sut = new TelemetryLeaderElection(_inner, _meter, _activitySource, "test");
		var eventRaised = false;
		sut.BecameLeader += (_, _) => eventRaised = true;

		// Act - raise event on inner
		_inner.BecameLeader += Raise.With(new LeaderElectionEventArgs("candidate-1", "test-resource"));

		// Assert
		eventRaised.ShouldBeTrue();
		await sut.DisposeAsync();
	}

	[Fact]
	public async Task ForwardLostLeadershipEvent()
	{
		// Arrange
		var sut = new TelemetryLeaderElection(_inner, _meter, _activitySource, "test");
		var eventRaised = false;
		sut.LostLeadership += (_, _) => eventRaised = true;

		// Act - raise event on inner
		_inner.LostLeadership += Raise.With(new LeaderElectionEventArgs("candidate-1", "test-resource"));

		// Assert
		eventRaised.ShouldBeTrue();
		await sut.DisposeAsync();
	}

	[Fact]
	public async Task ForwardLeaderChangedEvent()
	{
		// Arrange
		var sut = new TelemetryLeaderElection(_inner, _meter, _activitySource, "test");
		var eventRaised = false;
		sut.LeaderChanged += (_, _) => eventRaised = true;

		// Act - raise event on inner
		_inner.LeaderChanged += Raise.With(new LeaderChangedEventArgs("old", "new", "test-resource"));

		// Assert
		eventRaised.ShouldBeTrue();
		await sut.DisposeAsync();
	}

	[Fact]
	public async Task DisposeAsyncOnlyOnce()
	{
		// Arrange
		var sut = new TelemetryLeaderElection(_inner, _meter, _activitySource, "test");

		// Act - dispose twice
		await sut.DisposeAsync();
		await sut.DisposeAsync(); // Should not throw

		// Assert - no exception means success
	}

	[Fact]
	public async Task DisposeInnerWhenAsyncDisposable()
	{
		// Arrange
		var asyncDisposableInner = A.Fake<ILeaderElection>(builder =>
			builder.Implements<IAsyncDisposable>());
		A.CallTo(() => asyncDisposableInner.CandidateId).Returns("test");

		var sut = new TelemetryLeaderElection(asyncDisposableInner, _meter, _activitySource, "test");

		// Act
		await sut.DisposeAsync();

		// Assert
#pragma warning disable CA2012
		A.CallTo(() => ((IAsyncDisposable)asyncDisposableInner).DisposeAsync()).MustHaveHappenedOnceExactly();
#pragma warning restore CA2012
	}

	[Fact]
	public async Task DisposeInnerWhenDisposable_PrefersAsyncDisposable()
	{
		// Arrange — ILeaderElection inherits IAsyncDisposable, so DisposeAsync always wins
		// even when IDisposable is also implemented (IAsyncDisposable priority pattern)
		var disposableInner = A.Fake<ILeaderElection>(builder =>
			builder.Implements<IDisposable>());
		A.CallTo(() => disposableInner.CandidateId).Returns("test");

		var sut = new TelemetryLeaderElection(disposableInner, _meter, _activitySource, "test");

		// Act
		await sut.DisposeAsync();

		// Assert — DisposeAsync takes priority over Dispose when both are available
#pragma warning disable CA2012
		A.CallTo(() => ((IAsyncDisposable)disposableInner).DisposeAsync()).MustHaveHappenedOnceExactly();
#pragma warning restore CA2012
		A.CallTo(() => ((IDisposable)disposableInner).Dispose()).MustNotHaveHappened();
	}

	public void Dispose()
	{
		(_inner as IDisposable)?.Dispose();
		_meter.Dispose();
		_activitySource.Dispose();
	}
}
