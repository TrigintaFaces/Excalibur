// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Options.Core;

namespace Excalibur.Dispatch.Tests.Options.Core;

/// <summary>
/// Unit tests for <see cref="MetricsOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Options")]
[Trait("Priority", "0")]
public sealed class MetricsOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void Default_Enabled_IsFalse()
	{
		// Arrange & Act
		var options = new MetricsOptions();

		// Assert
		options.Enabled.ShouldBeFalse();
	}

	[Fact]
	public void Default_ExportInterval_IsThirtySeconds()
	{
		// Arrange & Act
		var options = new MetricsOptions();

		// Assert
		options.ExportInterval.ShouldBe(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void Default_CustomTags_IsEmpty()
	{
		// Arrange & Act
		var options = new MetricsOptions();

		// Assert
		_ = options.CustomTags.ShouldNotBeNull();
		options.CustomTags.ShouldBeEmpty();
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void Enabled_CanBeSet()
	{
		// Arrange
		var options = new MetricsOptions();

		// Act
		options.Enabled = true;

		// Assert
		options.Enabled.ShouldBeTrue();
	}

	[Fact]
	public void ExportInterval_CanBeSet()
	{
		// Arrange
		var options = new MetricsOptions();

		// Act
		options.ExportInterval = TimeSpan.FromMinutes(5);

		// Assert
		options.ExportInterval.ShouldBe(TimeSpan.FromMinutes(5));
	}

	#endregion

	#region CustomTags Tests

	[Fact]
	public void CustomTags_CanAddEntry()
	{
		// Arrange
		var options = new MetricsOptions();

		// Act
		options.CustomTags.Add("environment", "production");

		// Assert
		options.CustomTags.Count.ShouldBe(1);
		options.CustomTags["environment"].ShouldBe("production");
	}

	[Fact]
	public void CustomTags_CanAddMultipleEntries()
	{
		// Arrange
		var options = new MetricsOptions();

		// Act
		options.CustomTags.Add("environment", "production");
		options.CustomTags.Add("service", "dispatch");
		options.CustomTags.Add("region", "us-east-1");

		// Assert
		options.CustomTags.Count.ShouldBe(3);
	}

	[Fact]
	public void CustomTags_UsesOrdinalComparison()
	{
		// Arrange
		var options = new MetricsOptions();

		// Act
		options.CustomTags.Add("Environment", "prod");
		options.CustomTags.Add("ENVIRONMENT", "staging");

		// Assert - Should be treated as different keys (case-sensitive)
		options.CustomTags.Count.ShouldBe(2);
	}

	#endregion

	#region Object Initializer Tests

	[Fact]
	public void ObjectInitializer_SetsAllProperties()
	{
		// Act
		var options = new MetricsOptions
		{
			Enabled = true,
			ExportInterval = TimeSpan.FromMinutes(1),
		};

		// Assert
		options.Enabled.ShouldBeTrue();
		options.ExportInterval.ShouldBe(TimeSpan.FromMinutes(1));
	}

	#endregion

	#region Edge Cases

	[Fact]
	public void ExportInterval_CanBeZero()
	{
		// Arrange
		var options = new MetricsOptions();

		// Act
		options.ExportInterval = TimeSpan.Zero;

		// Assert
		options.ExportInterval.ShouldBe(TimeSpan.Zero);
	}

	#endregion
}
