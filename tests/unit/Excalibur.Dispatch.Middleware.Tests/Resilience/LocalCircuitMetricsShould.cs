// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Resilience.Polly;

namespace Excalibur.Dispatch.Middleware.Tests.Resilience;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Platform")]
public sealed class LocalCircuitMetricsShould
{
	[Fact]
	public void HaveDefaultValues()
	{
		// Arrange & Act
		var sut = new LocalCircuitMetrics();

		// Assert
		sut.SuccessCount.ShouldBe(0);
		sut.FailureCount.ShouldBe(0);
		sut.ConsecutiveFailures.ShouldBe(0);
		sut.ConsecutiveSuccesses.ShouldBe(0);
		sut.LastFailureReason.ShouldBe(string.Empty);
	}

	[Fact]
	public void SetAndGetSuccessCount()
	{
		var sut = new LocalCircuitMetrics { SuccessCount = 42 };
		sut.SuccessCount.ShouldBe(42);
	}

	[Fact]
	public void SetAndGetFailureCount()
	{
		var sut = new LocalCircuitMetrics { FailureCount = 7 };
		sut.FailureCount.ShouldBe(7);
	}

	[Fact]
	public void SetAndGetConsecutiveFailures()
	{
		var sut = new LocalCircuitMetrics { ConsecutiveFailures = 3 };
		sut.ConsecutiveFailures.ShouldBe(3);
	}

	[Fact]
	public void SetAndGetConsecutiveSuccesses()
	{
		var sut = new LocalCircuitMetrics { ConsecutiveSuccesses = 10 };
		sut.ConsecutiveSuccesses.ShouldBe(10);
	}

	[Fact]
	public void SetAndGetLastSuccess()
	{
		var now = DateTimeOffset.UtcNow;
		var sut = new LocalCircuitMetrics { LastSuccess = now };
		sut.LastSuccess.ShouldBe(now);
	}

	[Fact]
	public void SetAndGetLastFailure()
	{
		var now = DateTimeOffset.UtcNow;
		var sut = new LocalCircuitMetrics { LastFailure = now };
		sut.LastFailure.ShouldBe(now);
	}

	[Fact]
	public void SetAndGetLastFailureReason()
	{
		var sut = new LocalCircuitMetrics { LastFailureReason = "Timeout" };
		sut.LastFailureReason.ShouldBe("Timeout");
	}
}
