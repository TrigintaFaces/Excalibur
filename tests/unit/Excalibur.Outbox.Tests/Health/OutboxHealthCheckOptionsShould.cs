// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Outbox.Health;

namespace Excalibur.Outbox.Tests.Health;

/// <summary>
/// Unit tests for <see cref="OutboxHealthCheckOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Outbox")]
public sealed class OutboxHealthCheckOptionsShould : UnitTestBase
{
	#region Default Values Tests

	[Fact]
	public void HaveDefaultDegradedFailureRatePercent()
	{
		// Arrange & Act
		var options = new OutboxHealthCheckOptions();

		// Assert
		options.DegradedFailureRatePercent.ShouldBe(5.0);
	}

	[Fact]
	public void HaveDefaultUnhealthyFailureRatePercent()
	{
		// Arrange & Act
		var options = new OutboxHealthCheckOptions();

		// Assert
		options.UnhealthyFailureRatePercent.ShouldBe(20.0);
	}

	[Fact]
	public void HaveDefaultUnhealthyInactivityTimeout()
	{
		// Arrange & Act
		var options = new OutboxHealthCheckOptions();

		// Assert
		options.UnhealthyInactivityTimeout.ShouldBe(TimeSpan.FromMinutes(5));
	}

	[Fact]
	public void HaveDefaultDegradedInactivityTimeout()
	{
		// Arrange & Act
		var options = new OutboxHealthCheckOptions();

		// Assert
		options.DegradedInactivityTimeout.ShouldBe(TimeSpan.FromMinutes(2));
	}

	#endregion Default Values Tests

	#region Failure Rate Property Tests

	[Fact]
	public void AllowDegradedFailureRatePercentToBeSet()
	{
		// Arrange & Act
		var options = new OutboxHealthCheckOptions
		{
			DegradedFailureRatePercent = 10.0,
		};

		// Assert
		options.DegradedFailureRatePercent.ShouldBe(10.0);
	}

	[Fact]
	public void AllowUnhealthyFailureRatePercentToBeSet()
	{
		// Arrange & Act
		var options = new OutboxHealthCheckOptions
		{
			UnhealthyFailureRatePercent = 50.0,
		};

		// Assert
		options.UnhealthyFailureRatePercent.ShouldBe(50.0);
	}

	[Fact]
	public void AllowZeroDegradedFailureRatePercent()
	{
		// Arrange & Act
		var options = new OutboxHealthCheckOptions
		{
			DegradedFailureRatePercent = 0.0,
		};

		// Assert
		options.DegradedFailureRatePercent.ShouldBe(0.0);
	}

	[Fact]
	public void AllowZeroUnhealthyFailureRatePercent()
	{
		// Arrange & Act
		var options = new OutboxHealthCheckOptions
		{
			UnhealthyFailureRatePercent = 0.0,
		};

		// Assert
		options.UnhealthyFailureRatePercent.ShouldBe(0.0);
	}

	[Fact]
	public void Allow100PercentDegradedFailureRate()
	{
		// Arrange & Act
		var options = new OutboxHealthCheckOptions
		{
			DegradedFailureRatePercent = 100.0,
		};

		// Assert
		options.DegradedFailureRatePercent.ShouldBe(100.0);
	}

	[Fact]
	public void Allow100PercentUnhealthyFailureRate()
	{
		// Arrange & Act
		var options = new OutboxHealthCheckOptions
		{
			UnhealthyFailureRatePercent = 100.0,
		};

		// Assert
		options.UnhealthyFailureRatePercent.ShouldBe(100.0);
	}

	[Fact]
	public void AllowFractionalDegradedFailureRate()
	{
		// Arrange & Act
		var options = new OutboxHealthCheckOptions
		{
			DegradedFailureRatePercent = 2.5,
		};

		// Assert
		options.DegradedFailureRatePercent.ShouldBe(2.5);
	}

	[Fact]
	public void AllowFractionalUnhealthyFailureRate()
	{
		// Arrange & Act
		var options = new OutboxHealthCheckOptions
		{
			UnhealthyFailureRatePercent = 15.75,
		};

		// Assert
		options.UnhealthyFailureRatePercent.ShouldBe(15.75);
	}

	#endregion Failure Rate Property Tests

	#region Timeout Property Tests

	[Fact]
	public void AllowUnhealthyInactivityTimeoutToBeSet()
	{
		// Arrange & Act
		var options = new OutboxHealthCheckOptions
		{
			UnhealthyInactivityTimeout = TimeSpan.FromMinutes(10),
		};

		// Assert
		options.UnhealthyInactivityTimeout.ShouldBe(TimeSpan.FromMinutes(10));
	}

	[Fact]
	public void AllowDegradedInactivityTimeoutToBeSet()
	{
		// Arrange & Act
		var options = new OutboxHealthCheckOptions
		{
			DegradedInactivityTimeout = TimeSpan.FromMinutes(3),
		};

		// Assert
		options.DegradedInactivityTimeout.ShouldBe(TimeSpan.FromMinutes(3));
	}

	[Fact]
	public void AllowZeroUnhealthyInactivityTimeout()
	{
		// Arrange & Act
		var options = new OutboxHealthCheckOptions
		{
			UnhealthyInactivityTimeout = TimeSpan.Zero,
		};

		// Assert
		options.UnhealthyInactivityTimeout.ShouldBe(TimeSpan.Zero);
	}

	[Fact]
	public void AllowZeroDegradedInactivityTimeout()
	{
		// Arrange & Act
		var options = new OutboxHealthCheckOptions
		{
			DegradedInactivityTimeout = TimeSpan.Zero,
		};

		// Assert
		options.DegradedInactivityTimeout.ShouldBe(TimeSpan.Zero);
	}

	#endregion Timeout Property Tests

	#region Configuration Scenario Tests

	[Fact]
	public void CreateStrictMonitoringConfiguration()
	{
		// Arrange & Act
		var options = new OutboxHealthCheckOptions
		{
			DegradedFailureRatePercent = 1.0,
			UnhealthyFailureRatePercent = 5.0,
			DegradedInactivityTimeout = TimeSpan.FromSeconds(30),
			UnhealthyInactivityTimeout = TimeSpan.FromMinutes(1),
		};

		// Assert
		options.DegradedFailureRatePercent.ShouldBe(1.0);
		options.UnhealthyFailureRatePercent.ShouldBe(5.0);
		options.DegradedInactivityTimeout.ShouldBe(TimeSpan.FromSeconds(30));
		options.UnhealthyInactivityTimeout.ShouldBe(TimeSpan.FromMinutes(1));
	}

	[Fact]
	public void CreateRelaxedMonitoringConfiguration()
	{
		// Arrange & Act
		var options = new OutboxHealthCheckOptions
		{
			DegradedFailureRatePercent = 25.0,
			UnhealthyFailureRatePercent = 75.0,
			DegradedInactivityTimeout = TimeSpan.FromMinutes(15),
			UnhealthyInactivityTimeout = TimeSpan.FromMinutes(30),
		};

		// Assert
		options.DegradedFailureRatePercent.ShouldBe(25.0);
		options.UnhealthyFailureRatePercent.ShouldBe(75.0);
		options.DegradedInactivityTimeout.ShouldBe(TimeSpan.FromMinutes(15));
		options.UnhealthyInactivityTimeout.ShouldBe(TimeSpan.FromMinutes(30));
	}

	[Fact]
	public void CreateHighAvailabilityConfiguration()
	{
		// Arrange & Act
		var options = new OutboxHealthCheckOptions
		{
			DegradedFailureRatePercent = 0.1,
			UnhealthyFailureRatePercent = 1.0,
			DegradedInactivityTimeout = TimeSpan.FromSeconds(10),
			UnhealthyInactivityTimeout = TimeSpan.FromSeconds(30),
		};

		// Assert
		options.DegradedFailureRatePercent.ShouldBe(0.1);
		options.UnhealthyFailureRatePercent.ShouldBe(1.0);
		options.DegradedInactivityTimeout.ShouldBe(TimeSpan.FromSeconds(10));
		options.UnhealthyInactivityTimeout.ShouldBe(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void AllowDegradedRateGreaterThanUnhealthyRate()
	{
		// Arrange & Act - While semantically incorrect, the class allows it
		var options = new OutboxHealthCheckOptions
		{
			DegradedFailureRatePercent = 50.0,
			UnhealthyFailureRatePercent = 10.0,
		};

		// Assert
		options.DegradedFailureRatePercent.ShouldBeGreaterThan(options.UnhealthyFailureRatePercent);
	}

	[Fact]
	public void AllowEqualFailureRates()
	{
		// Arrange & Act
		var options = new OutboxHealthCheckOptions
		{
			DegradedFailureRatePercent = 15.0,
			UnhealthyFailureRatePercent = 15.0,
		};

		// Assert
		options.DegradedFailureRatePercent.ShouldBe(options.UnhealthyFailureRatePercent);
	}

	#endregion Configuration Scenario Tests

	#region Edge Case Tests

	[Fact]
	public void AllowVerySmallDegradedFailureRate()
	{
		// Arrange & Act
		var options = new OutboxHealthCheckOptions
		{
			DegradedFailureRatePercent = 0.001,
		};

		// Assert
		options.DegradedFailureRatePercent.ShouldBe(0.001);
	}

	[Fact]
	public void AllowVerySmallUnhealthyFailureRate()
	{
		// Arrange & Act
		var options = new OutboxHealthCheckOptions
		{
			UnhealthyFailureRatePercent = 0.001,
		};

		// Assert
		options.UnhealthyFailureRatePercent.ShouldBe(0.001);
	}

	[Fact]
	public void SupportSubSecondDegradedTimeout()
	{
		// Arrange & Act
		var options = new OutboxHealthCheckOptions
		{
			DegradedInactivityTimeout = TimeSpan.FromMilliseconds(500),
		};

		// Assert
		options.DegradedInactivityTimeout.ShouldBe(TimeSpan.FromMilliseconds(500));
	}

	[Fact]
	public void SupportSubSecondUnhealthyTimeout()
	{
		// Arrange & Act
		var options = new OutboxHealthCheckOptions
		{
			UnhealthyInactivityTimeout = TimeSpan.FromMilliseconds(100),
		};

		// Assert
		options.UnhealthyInactivityTimeout.ShouldBe(TimeSpan.FromMilliseconds(100));
	}

	[Fact]
	public void SupportLargeTimeoutValues()
	{
		// Arrange & Act
		var options = new OutboxHealthCheckOptions
		{
			DegradedInactivityTimeout = TimeSpan.FromDays(1),
			UnhealthyInactivityTimeout = TimeSpan.FromDays(7),
		};

		// Assert
		options.DegradedInactivityTimeout.ShouldBe(TimeSpan.FromDays(1));
		options.UnhealthyInactivityTimeout.ShouldBe(TimeSpan.FromDays(7));
	}

	#endregion Edge Case Tests
}
