// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;

using Excalibur.LeaderElection.Diagnostics;

namespace Excalibur.LeaderElection.Tests.Diagnostics;

/// <summary>
/// Unit tests for <see cref="TelemetryLeaderElectionFactory"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
[Trait("Feature", "LeaderElection")]
public sealed class TelemetryLeaderElectionFactoryShould : IDisposable
{
	private readonly ILeaderElectionFactory _innerFactory;
	private readonly Meter _meter;
	private readonly ActivitySource _activitySource;
	private readonly TelemetryLeaderElectionFactory _sut;

	public TelemetryLeaderElectionFactoryShould()
	{
		_innerFactory = A.Fake<ILeaderElectionFactory>();
		_meter = new Meter("TelemetryFactory.Test." + Guid.NewGuid().ToString("N")[..8]);
		_activitySource = new ActivitySource("TelemetryFactory.Test");
		_sut = new TelemetryLeaderElectionFactory(_innerFactory, _meter, _activitySource, "test-provider");
	}

	public void Dispose()
	{
		_sut.Dispose();
		_meter.Dispose();
		_activitySource.Dispose();
	}

	// --- Constructor null guards ---

	[Fact]
	public void ThrowWhenInnerIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new TelemetryLeaderElectionFactory(null!, _meter, _activitySource, "test"));
	}

	[Fact]
	public void ThrowWhenMeterIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new TelemetryLeaderElectionFactory(_innerFactory, null!, _activitySource, "test"));
	}

	[Fact]
	public void ThrowWhenActivitySourceIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new TelemetryLeaderElectionFactory(_innerFactory, _meter, null!, "test"));
	}

	[Fact]
	public void ThrowWhenProviderNameIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			new TelemetryLeaderElectionFactory(_innerFactory, _meter, _activitySource, null!));
	}

	[Fact]
	public void ThrowWhenProviderNameIsEmpty()
	{
		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			new TelemetryLeaderElectionFactory(_innerFactory, _meter, _activitySource, ""));
	}

	[Fact]
	public void ThrowWhenProviderNameIsWhitespace()
	{
		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			new TelemetryLeaderElectionFactory(_innerFactory, _meter, _activitySource, "   "));
	}

	// --- CreateElection ---

	[Fact]
	public void CreateElectionDelegatingToInner()
	{
		// Arrange
		var innerElection = A.Fake<ILeaderElection>();
		A.CallTo(() => innerElection.CandidateId).Returns("candidate-1");
		A.CallTo(() => _innerFactory.CreateElection("resource-1", "candidate-1")).Returns(innerElection);

		// Act
		var result = _sut.CreateElection("resource-1", "candidate-1");

		// Assert
		result.ShouldNotBeNull();
		result.ShouldBeOfType<TelemetryLeaderElection>();
		A.CallTo(() => _innerFactory.CreateElection("resource-1", "candidate-1")).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void CreateElectionWithNullCandidateId()
	{
		// Arrange
		var innerElection = A.Fake<ILeaderElection>();
		A.CallTo(() => innerElection.CandidateId).Returns("auto-id");
		A.CallTo(() => _innerFactory.CreateElection("resource-1", null)).Returns(innerElection);

		// Act
		var result = _sut.CreateElection("resource-1", null);

		// Assert
		result.ShouldNotBeNull();
		result.ShouldBeOfType<TelemetryLeaderElection>();
		A.CallTo(() => _innerFactory.CreateElection("resource-1", null)).MustHaveHappenedOnceExactly();
	}

	// --- CreateHealthBasedElection ---

	[Fact]
	public void CreateHealthBasedElectionDelegatingWithoutWrapping()
	{
		// Arrange
		var innerHealthBased = A.Fake<IHealthBasedLeaderElection>();
		A.CallTo(() => _innerFactory.CreateHealthBasedElection("resource-2", "candidate-2")).Returns(innerHealthBased);

		// Act
		var result = _sut.CreateHealthBasedElection("resource-2", "candidate-2");

		// Assert — should return the inner instance directly, NOT wrapped
		result.ShouldBeSameAs(innerHealthBased);
		A.CallTo(() => _innerFactory.CreateHealthBasedElection("resource-2", "candidate-2")).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void CreateHealthBasedElectionWithNullCandidateId()
	{
		// Arrange
		var innerHealthBased = A.Fake<IHealthBasedLeaderElection>();
		A.CallTo(() => _innerFactory.CreateHealthBasedElection("resource-2", null)).Returns(innerHealthBased);

		// Act
		var result = _sut.CreateHealthBasedElection("resource-2", null);

		// Assert
		result.ShouldBeSameAs(innerHealthBased);
	}

	// --- Dispose ---

	[Fact]
	public void DisposeIdempotently()
	{
		// Arrange
		using var meter = new Meter("DisposeTest." + Guid.NewGuid().ToString("N")[..8]);
		using var source = new ActivitySource("DisposeTest");
		var factory = new TelemetryLeaderElectionFactory(_innerFactory, meter, source, "test");

		// Act — dispose twice
		factory.Dispose();
		factory.Dispose();

		// Assert — no exception thrown (volatile _disposed guard works)
	}
}
