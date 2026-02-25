// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Outbox.Health;

namespace Excalibur.Outbox.Tests.Health;

/// <summary>
/// Unit tests for <see cref="InboxHealthCheckOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Outbox")]
public sealed class InboxHealthCheckOptionsShould : UnitTestBase
{
	#region Default Values Tests

	[Fact]
	public void HaveDefaultUnhealthyInactivityTimeout()
	{
		// Arrange & Act
		var options = new InboxHealthCheckOptions();

		// Assert
		options.UnhealthyInactivityTimeout.ShouldBe(TimeSpan.FromMinutes(5));
	}

	[Fact]
	public void HaveDefaultDegradedInactivityTimeout()
	{
		// Arrange & Act
		var options = new InboxHealthCheckOptions();

		// Assert
		options.DegradedInactivityTimeout.ShouldBe(TimeSpan.FromMinutes(2));
	}

	#endregion Default Values Tests

	#region Property Setting Tests

	[Fact]
	public void AllowUnhealthyInactivityTimeoutToBeSet()
	{
		// Arrange & Act
		var options = new InboxHealthCheckOptions
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
		var options = new InboxHealthCheckOptions
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
		var options = new InboxHealthCheckOptions
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
		var options = new InboxHealthCheckOptions
		{
			DegradedInactivityTimeout = TimeSpan.Zero,
		};

		// Assert
		options.DegradedInactivityTimeout.ShouldBe(TimeSpan.Zero);
	}

	#endregion Property Setting Tests

	#region Configuration Scenario Tests

	[Fact]
	public void CreateStrictMonitoringConfiguration()
	{
		// Arrange & Act
		var options = new InboxHealthCheckOptions
		{
			UnhealthyInactivityTimeout = TimeSpan.FromMinutes(1),
			DegradedInactivityTimeout = TimeSpan.FromSeconds(30),
		};

		// Assert
		options.UnhealthyInactivityTimeout.ShouldBe(TimeSpan.FromMinutes(1));
		options.DegradedInactivityTimeout.ShouldBe(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void CreateRelaxedMonitoringConfiguration()
	{
		// Arrange & Act
		var options = new InboxHealthCheckOptions
		{
			UnhealthyInactivityTimeout = TimeSpan.FromMinutes(30),
			DegradedInactivityTimeout = TimeSpan.FromMinutes(15),
		};

		// Assert
		options.UnhealthyInactivityTimeout.ShouldBe(TimeSpan.FromMinutes(30));
		options.DegradedInactivityTimeout.ShouldBe(TimeSpan.FromMinutes(15));
	}

	[Fact]
	public void CreateLongRunningServiceConfiguration()
	{
		// Arrange & Act
		var options = new InboxHealthCheckOptions
		{
			UnhealthyInactivityTimeout = TimeSpan.FromHours(1),
			DegradedInactivityTimeout = TimeSpan.FromMinutes(30),
		};

		// Assert
		options.UnhealthyInactivityTimeout.ShouldBe(TimeSpan.FromHours(1));
		options.DegradedInactivityTimeout.ShouldBe(TimeSpan.FromMinutes(30));
	}

	[Fact]
	public void AllowDegradedTimeoutGreaterThanUnhealthyTimeout()
	{
		// Arrange & Act - While this may be semantically incorrect, the class allows it
		var options = new InboxHealthCheckOptions
		{
			UnhealthyInactivityTimeout = TimeSpan.FromMinutes(1),
			DegradedInactivityTimeout = TimeSpan.FromMinutes(10),
		};

		// Assert
		options.DegradedInactivityTimeout.ShouldBeGreaterThan(options.UnhealthyInactivityTimeout);
	}

	[Fact]
	public void AllowEqualTimeouts()
	{
		// Arrange & Act
		var options = new InboxHealthCheckOptions
		{
			UnhealthyInactivityTimeout = TimeSpan.FromMinutes(5),
			DegradedInactivityTimeout = TimeSpan.FromMinutes(5),
		};

		// Assert
		options.UnhealthyInactivityTimeout.ShouldBe(options.DegradedInactivityTimeout);
	}

	#endregion Configuration Scenario Tests

	#region TimeSpan Value Tests

	[Fact]
	public void SupportSubSecondDegradedTimeout()
	{
		// Arrange & Act
		var options = new InboxHealthCheckOptions
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
		var options = new InboxHealthCheckOptions
		{
			UnhealthyInactivityTimeout = TimeSpan.FromMilliseconds(100),
		};

		// Assert
		options.UnhealthyInactivityTimeout.ShouldBe(TimeSpan.FromMilliseconds(100));
	}

	[Fact]
	public void SupportLargeDegradedTimeout()
	{
		// Arrange & Act
		var options = new InboxHealthCheckOptions
		{
			DegradedInactivityTimeout = TimeSpan.FromDays(1),
		};

		// Assert
		options.DegradedInactivityTimeout.ShouldBe(TimeSpan.FromDays(1));
	}

	[Fact]
	public void SupportLargeUnhealthyTimeout()
	{
		// Arrange & Act
		var options = new InboxHealthCheckOptions
		{
			UnhealthyInactivityTimeout = TimeSpan.FromDays(7),
		};

		// Assert
		options.UnhealthyInactivityTimeout.ShouldBe(TimeSpan.FromDays(7));
	}

	#endregion TimeSpan Value Tests
}
