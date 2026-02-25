// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Options.Core;

namespace Excalibur.Dispatch.Tests.Options.Core;

/// <summary>
/// Unit tests for <see cref="HealthCheckOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Options")]
[Trait("Priority", "0")]
public sealed class HealthCheckOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void Default_Enabled_IsFalse()
	{
		// Arrange & Act
		var options = new HealthCheckOptions();

		// Assert
		options.Enabled.ShouldBeFalse();
	}

	[Fact]
	public void Default_Timeout_IsTenSeconds()
	{
		// Arrange & Act
		var options = new HealthCheckOptions();

		// Assert
		options.Timeout.ShouldBe(TimeSpan.FromSeconds(10));
	}

	[Fact]
	public void Default_Interval_IsThirtySeconds()
	{
		// Arrange & Act
		var options = new HealthCheckOptions();

		// Assert
		options.Interval.ShouldBe(TimeSpan.FromSeconds(30));
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void Enabled_CanBeSet()
	{
		// Arrange
		var options = new HealthCheckOptions();

		// Act
		options.Enabled = true;

		// Assert
		options.Enabled.ShouldBeTrue();
	}

	[Fact]
	public void Timeout_CanBeSet()
	{
		// Arrange
		var options = new HealthCheckOptions();

		// Act
		options.Timeout = TimeSpan.FromSeconds(30);

		// Assert
		options.Timeout.ShouldBe(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void Interval_CanBeSet()
	{
		// Arrange
		var options = new HealthCheckOptions();

		// Act
		options.Interval = TimeSpan.FromMinutes(5);

		// Assert
		options.Interval.ShouldBe(TimeSpan.FromMinutes(5));
	}

	#endregion

	#region Object Initializer Tests

	[Fact]
	public void ObjectInitializer_SetsAllProperties()
	{
		// Act
		var options = new HealthCheckOptions
		{
			Enabled = true,
			Timeout = TimeSpan.FromSeconds(5),
			Interval = TimeSpan.FromMinutes(1),
		};

		// Assert
		options.Enabled.ShouldBeTrue();
		options.Timeout.ShouldBe(TimeSpan.FromSeconds(5));
		options.Interval.ShouldBe(TimeSpan.FromMinutes(1));
	}

	#endregion
}
