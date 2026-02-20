// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;

using Excalibur.LeaderElection.Diagnostics;

namespace Excalibur.LeaderElection.Tests.Diagnostics;

/// <summary>
/// Depth coverage tests for <see cref="TelemetryLeaderElectionFactory"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class TelemetryLeaderElectionFactoryDepthShould : IDisposable
{
	private readonly ILeaderElectionFactory _innerFactory;
	private readonly Meter _meter;
	private readonly ActivitySource _activitySource;

	public TelemetryLeaderElectionFactoryDepthShould()
	{
		_innerFactory = A.Fake<ILeaderElectionFactory>();
		_meter = new Meter("Test.LEFactory.Depth", "1.0");
		_activitySource = new ActivitySource("Test.LEFactory.Depth", "1.0");
	}

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

	[Fact]
	public void WrapCreatedElectionWithTelemetry()
	{
		// Arrange
		var innerElection = A.Fake<ILeaderElection>();
		A.CallTo(() => innerElection.CandidateId).Returns("candidate-1");
		A.CallTo(() => _innerFactory.CreateElection("resource-1", "candidate-1")).Returns(innerElection);

		var sut = new TelemetryLeaderElectionFactory(_innerFactory, _meter, _activitySource, "test");

		// Act
		var result = sut.CreateElection("resource-1", "candidate-1");

		// Assert
		result.ShouldBeOfType<TelemetryLeaderElection>();
		A.CallTo(() => _innerFactory.CreateElection("resource-1", "candidate-1")).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void DelegateCreateHealthBasedElectionToInner()
	{
		// Arrange
		var healthBased = A.Fake<IHealthBasedLeaderElection>();
		A.CallTo(() => _innerFactory.CreateHealthBasedElection("resource-1", "candidate-1")).Returns(healthBased);

		var sut = new TelemetryLeaderElectionFactory(_innerFactory, _meter, _activitySource, "test");

		// Act
		var result = sut.CreateHealthBasedElection("resource-1", "candidate-1");

		// Assert
		result.ShouldBe(healthBased);
		A.CallTo(() => _innerFactory.CreateHealthBasedElection("resource-1", "candidate-1")).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void DisposeOnlyOnce()
	{
		// Arrange
		var sut = new TelemetryLeaderElectionFactory(_innerFactory, _meter, _activitySource, "test");

		// Act - dispose twice
		sut.Dispose();
		sut.Dispose(); // Should not throw

		// Assert - no exception means success
	}

	public void Dispose()
	{
		_meter.Dispose();
		_activitySource.Dispose();
	}
}
