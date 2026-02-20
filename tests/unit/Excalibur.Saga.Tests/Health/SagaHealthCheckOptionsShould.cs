// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.Health;

namespace Excalibur.Saga.Tests.Health;

/// <summary>
/// Unit tests for <see cref="SagaHealthCheckOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Saga")]
public sealed class SagaHealthCheckOptionsShould
{
	#region Default Values Tests

	[Fact]
	public void HaveDefaultStuckThreshold()
	{
		// Arrange & Act
		var options = new SagaHealthCheckOptions();

		// Assert
		options.StuckThreshold.ShouldBe(TimeSpan.FromHours(1));
	}

	[Fact]
	public void HaveDefaultUnhealthyStuckThreshold()
	{
		// Arrange & Act
		var options = new SagaHealthCheckOptions();

		// Assert
		options.UnhealthyStuckThreshold.ShouldBe(10);
	}

	[Fact]
	public void HaveDefaultDegradedFailedThreshold()
	{
		// Arrange & Act
		var options = new SagaHealthCheckOptions();

		// Assert
		options.DegradedFailedThreshold.ShouldBe(5);
	}

	[Fact]
	public void HaveDefaultStuckLimit()
	{
		// Arrange & Act
		var options = new SagaHealthCheckOptions();

		// Assert
		options.StuckLimit.ShouldBe(100);
	}

	[Fact]
	public void HaveDefaultFailedLimit()
	{
		// Arrange & Act
		var options = new SagaHealthCheckOptions();

		// Assert
		options.FailedLimit.ShouldBe(100);
	}

	#endregion Default Values Tests

	#region Property Setting Tests

	[Fact]
	public void AllowStuckThresholdToBeSet()
	{
		// Arrange & Act
		var options = new SagaHealthCheckOptions { StuckThreshold = TimeSpan.FromMinutes(30) };

		// Assert
		options.StuckThreshold.ShouldBe(TimeSpan.FromMinutes(30));
	}

	[Fact]
	public void AllowUnhealthyStuckThresholdToBeSet()
	{
		// Arrange & Act
		var options = new SagaHealthCheckOptions { UnhealthyStuckThreshold = 5 };

		// Assert
		options.UnhealthyStuckThreshold.ShouldBe(5);
	}

	[Fact]
	public void AllowDegradedFailedThresholdToBeSet()
	{
		// Arrange & Act
		var options = new SagaHealthCheckOptions { DegradedFailedThreshold = 3 };

		// Assert
		options.DegradedFailedThreshold.ShouldBe(3);
	}

	[Fact]
	public void AllowStuckLimitToBeSet()
	{
		// Arrange & Act
		var options = new SagaHealthCheckOptions { StuckLimit = 50 };

		// Assert
		options.StuckLimit.ShouldBe(50);
	}

	[Fact]
	public void AllowFailedLimitToBeSet()
	{
		// Arrange & Act
		var options = new SagaHealthCheckOptions { FailedLimit = 50 };

		// Assert
		options.FailedLimit.ShouldBe(50);
	}

	#endregion Property Setting Tests

	#region Configuration Scenario Tests

	[Fact]
	public void CreateStrictHealthCheckConfiguration()
	{
		// Arrange & Act
		var options = new SagaHealthCheckOptions
		{
			StuckThreshold = TimeSpan.FromMinutes(15),
			UnhealthyStuckThreshold = 3,
			DegradedFailedThreshold = 1,
		};

		// Assert
		options.StuckThreshold.ShouldBe(TimeSpan.FromMinutes(15));
		options.UnhealthyStuckThreshold.ShouldBe(3);
		options.DegradedFailedThreshold.ShouldBe(1);
	}

	[Fact]
	public void CreateRelaxedHealthCheckConfiguration()
	{
		// Arrange & Act
		var options = new SagaHealthCheckOptions
		{
			StuckThreshold = TimeSpan.FromHours(4),
			UnhealthyStuckThreshold = 50,
			DegradedFailedThreshold = 20,
		};

		// Assert
		options.StuckThreshold.ShouldBe(TimeSpan.FromHours(4));
		options.UnhealthyStuckThreshold.ShouldBe(50);
		options.DegradedFailedThreshold.ShouldBe(20);
	}

	[Fact]
	public void CreateLimitedQueryConfiguration()
	{
		// Arrange & Act
		var options = new SagaHealthCheckOptions
		{
			StuckLimit = 25,
			FailedLimit = 25,
		};

		// Assert
		options.StuckLimit.ShouldBe(25);
		options.FailedLimit.ShouldBe(25);
	}

	[Fact]
	public void CreateLargeScaleConfiguration()
	{
		// Arrange & Act
		var options = new SagaHealthCheckOptions
		{
			StuckLimit = 500,
			FailedLimit = 500,
			UnhealthyStuckThreshold = 100,
			DegradedFailedThreshold = 50,
		};

		// Assert
		options.StuckLimit.ShouldBe(500);
		options.FailedLimit.ShouldBe(500);
		options.UnhealthyStuckThreshold.ShouldBe(100);
		options.DegradedFailedThreshold.ShouldBe(50);
	}

	#endregion Configuration Scenario Tests
}
