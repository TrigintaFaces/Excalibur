// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;

using Excalibur.Dispatch.LeaderElection;
using Excalibur.LeaderElection.Diagnostics;

namespace Excalibur.LeaderElection.Tests.Diagnostics;

/// <summary>
/// Extended unit tests for <see cref="TelemetryLeaderElection"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
[Trait("Feature", "LeaderElection")]
public sealed class TelemetryLeaderElectionExtendedShould : IAsyncDisposable
{
	private readonly Meter _meter = new("TestMeter.LE");
	private readonly ActivitySource _activitySource = new("TestActivity.LE");

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
		// Arrange
		var inner = A.Fake<ILeaderElection>();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new TelemetryLeaderElection(inner, null!, _activitySource, "test"));
	}

	[Fact]
	public void ThrowWhenActivitySourceIsNull()
	{
		// Arrange
		var inner = A.Fake<ILeaderElection>();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new TelemetryLeaderElection(inner, _meter, null!, "test"));
	}

	[Fact]
	public void ThrowWhenProviderNameIsNull()
	{
		// Arrange
		var inner = A.Fake<ILeaderElection>();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new TelemetryLeaderElection(inner, _meter, _activitySource, null!));
	}

	[Fact]
	public void DelegateCandidateIdToInner()
	{
		// Arrange
		var inner = A.Fake<ILeaderElection>();
		A.CallTo(() => inner.CandidateId).Returns("candidate-1");
		var sut = new TelemetryLeaderElection(inner, _meter, _activitySource, "test-provider");

		// Act & Assert
		sut.CandidateId.ShouldBe("candidate-1");
	}

	[Fact]
	public void DelegateIsLeaderToInner()
	{
		// Arrange
		var inner = A.Fake<ILeaderElection>();
		A.CallTo(() => inner.IsLeader).Returns(true);
		var sut = new TelemetryLeaderElection(inner, _meter, _activitySource, "test-provider");

		// Act & Assert
		sut.IsLeader.ShouldBeTrue();
	}

	[Fact]
	public void DelegateCurrentLeaderIdToInner()
	{
		// Arrange
		var inner = A.Fake<ILeaderElection>();
		A.CallTo(() => inner.CurrentLeaderId).Returns("leader-1");
		var sut = new TelemetryLeaderElection(inner, _meter, _activitySource, "test-provider");

		// Act & Assert
		sut.CurrentLeaderId.ShouldBe("leader-1");
	}

	[Fact]
	public async Task DelegateStartAsyncToInner()
	{
		// Arrange
		var inner = A.Fake<ILeaderElection>();
		var sut = CreateSut(inner);

		// Act
		await sut.StartAsync(CancellationToken.None);

		// Assert
		A.CallTo(() => inner.StartAsync(A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task DelegateStopAsyncToInner()
	{
		// Arrange
		var inner = A.Fake<ILeaderElection>();
		var sut = CreateSut(inner);

		// Act
		await sut.StopAsync(CancellationToken.None);

		// Assert
		A.CallTo(() => inner.StopAsync(A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task PropagateStartAsyncExceptions()
	{
		// Arrange
		var inner = A.Fake<ILeaderElection>();
		A.CallTo(() => inner.StartAsync(A<CancellationToken>._))
			.ThrowsAsync(new InvalidOperationException("Start failed"));
		var sut = CreateSut(inner);

		// Act & Assert
		await Should.ThrowAsync<InvalidOperationException>(() =>
			sut.StartAsync(CancellationToken.None));
	}

	[Fact]
	public async Task PropagateStopAsyncExceptions()
	{
		// Arrange
		var inner = A.Fake<ILeaderElection>();
		A.CallTo(() => inner.StopAsync(A<CancellationToken>._))
			.ThrowsAsync(new InvalidOperationException("Stop failed"));
		var sut = CreateSut(inner);

		// Act & Assert
		await Should.ThrowAsync<InvalidOperationException>(() =>
			sut.StopAsync(CancellationToken.None));
	}

	[Fact]
	public async Task DisposeInnerViaAsyncDisposable()
	{
		// ILeaderElection : IAsyncDisposable, so DisposeAsync is always the disposal path
		// Arrange
		var inner = A.Fake<ILeaderElection>();
		var sut = new TelemetryLeaderElection(inner, _meter, _activitySource, "test");

		// Act
		await sut.DisposeAsync();

		// Assert
		A.CallTo(() => inner.DisposeAsync())
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task NotDisposeInnerTwice()
	{
		// Arrange
		var inner = A.Fake<ILeaderElection>();
		var sut = new TelemetryLeaderElection(inner, _meter, _activitySource, "test");

		// Act
		await sut.DisposeAsync();
		await sut.DisposeAsync();

		// Assert
		A.CallTo(() => inner.DisposeAsync())
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void ForwardBecameLeaderEvents()
	{
		// Arrange
		var inner = A.Fake<ILeaderElection>();
		var sut = CreateSut(inner);
		var raised = false;
		sut.BecameLeader += (_, _) => raised = true;

		// Act
		inner.BecameLeader += Raise.FreeForm<EventHandler<LeaderElectionEventArgs>>
			.With(inner, new LeaderElectionEventArgs("c1", "r1"));

		// Assert
		raised.ShouldBeTrue();
	}

	[Fact]
	public void ForwardLostLeadershipEvents()
	{
		// Arrange
		var inner = A.Fake<ILeaderElection>();
		var sut = CreateSut(inner);
		var raised = false;
		sut.LostLeadership += (_, _) => raised = true;

		// Act
		inner.LostLeadership += Raise.FreeForm<EventHandler<LeaderElectionEventArgs>>
			.With(inner, new LeaderElectionEventArgs("c1", "r1"));

		// Assert
		raised.ShouldBeTrue();
	}

	[Fact]
	public void ForwardLeaderChangedEvents()
	{
		// Arrange
		var inner = A.Fake<ILeaderElection>();
		var sut = CreateSut(inner);
		var raised = false;
		sut.LeaderChanged += (_, _) => raised = true;

		// Act
		inner.LeaderChanged += Raise.FreeForm<EventHandler<LeaderChangedEventArgs>>
			.With(inner, new LeaderChangedEventArgs("old", "new", "r1"));

		// Assert
		raised.ShouldBeTrue();
	}

	private TelemetryLeaderElection CreateSut(ILeaderElection inner)
	{
		A.CallTo(() => inner.CandidateId).Returns("test-candidate");
		return new TelemetryLeaderElection(inner, _meter, _activitySource, "test-provider");
	}

	public async ValueTask DisposeAsync()
	{
		_meter.Dispose();
		_activitySource.Dispose();
		await Task.CompletedTask.ConfigureAwait(false);
	}
}
