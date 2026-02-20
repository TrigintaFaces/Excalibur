// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Options.Core;

namespace Excalibur.Dispatch.Tests.Options.Core;

/// <summary>
/// Unit tests for <see cref="MessageBusHealthCheckOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Options")]
[Trait("Priority", "0")]
public sealed class MessageBusHealthCheckOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void Default_Enabled_IsFalse()
	{
		// Arrange & Act
		var options = new MessageBusHealthCheckOptions();

		// Assert
		options.Enabled.ShouldBeFalse();
	}

	[Fact]
	public void Default_Timeout_Is15Seconds()
	{
		// Arrange & Act
		var options = new MessageBusHealthCheckOptions();

		// Assert
		options.Timeout.ShouldBe(TimeSpan.FromSeconds(15));
	}

	[Fact]
	public void Default_Interval_Is30Seconds()
	{
		// Arrange & Act
		var options = new MessageBusHealthCheckOptions();

		// Assert
		options.Interval.ShouldBe(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void Default_FailureThreshold_Is3()
	{
		// Arrange & Act
		var options = new MessageBusHealthCheckOptions();

		// Assert
		options.FailureThreshold.ShouldBe(3);
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void Enabled_CanBeSet()
	{
		// Arrange
		var options = new MessageBusHealthCheckOptions();

		// Act
		options.Enabled = true;

		// Assert
		options.Enabled.ShouldBeTrue();
	}

	[Fact]
	public void Timeout_CanBeSet()
	{
		// Arrange
		var options = new MessageBusHealthCheckOptions();

		// Act
		options.Timeout = TimeSpan.FromSeconds(30);

		// Assert
		options.Timeout.ShouldBe(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void Interval_CanBeSet()
	{
		// Arrange
		var options = new MessageBusHealthCheckOptions();

		// Act
		options.Interval = TimeSpan.FromMinutes(1);

		// Assert
		options.Interval.ShouldBe(TimeSpan.FromMinutes(1));
	}

	[Fact]
	public void FailureThreshold_CanBeSet()
	{
		// Arrange
		var options = new MessageBusHealthCheckOptions();

		// Act
		options.FailureThreshold = 5;

		// Assert
		options.FailureThreshold.ShouldBe(5);
	}

	#endregion

	#region Object Initializer Tests

	[Fact]
	public void ObjectInitializer_SetsAllProperties()
	{
		// Act
		var options = new MessageBusHealthCheckOptions
		{
			Enabled = true,
			Timeout = TimeSpan.FromSeconds(10),
			Interval = TimeSpan.FromSeconds(60),
			FailureThreshold = 2,
		};

		// Assert
		options.Enabled.ShouldBeTrue();
		options.Timeout.ShouldBe(TimeSpan.FromSeconds(10));
		options.Interval.ShouldBe(TimeSpan.FromSeconds(60));
		options.FailureThreshold.ShouldBe(2);
	}

	#endregion

	#region Real-World Scenario Tests

	[Fact]
	public void Options_ForProduction_EnablesHealthChecks()
	{
		// Act
		var options = new MessageBusHealthCheckOptions
		{
			Enabled = true,
			Timeout = TimeSpan.FromSeconds(5),
			Interval = TimeSpan.FromSeconds(15),
			FailureThreshold = 3,
		};

		// Assert
		options.Enabled.ShouldBeTrue();
		options.Timeout.ShouldBeLessThan(options.Interval);
	}

	[Fact]
	public void Options_ForHighAvailability_HasLowThreshold()
	{
		// Act
		var options = new MessageBusHealthCheckOptions
		{
			Enabled = true,
			FailureThreshold = 1,
			Interval = TimeSpan.FromSeconds(10),
		};

		// Assert
		options.FailureThreshold.ShouldBe(1);
		options.Interval.ShouldBeLessThan(TimeSpan.FromSeconds(30));
	}

	#endregion
}
