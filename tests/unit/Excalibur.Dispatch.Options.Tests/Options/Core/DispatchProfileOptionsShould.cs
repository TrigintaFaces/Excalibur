// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Options.Core;

namespace Excalibur.Dispatch.Tests.Options.Core;

/// <summary>
/// Unit tests for <see cref="DispatchProfileOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Options")]
[Trait("Priority", "0")]
public sealed class DispatchProfileOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void Default_ProfileName_IsEmptyString()
	{
		// Arrange & Act
		var options = new DispatchProfileOptions();

		// Assert
		options.ProfileName.ShouldBe(string.Empty);
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void ProfileName_CanBeSet()
	{
		// Arrange
		var options = new DispatchProfileOptions();

		// Act
		options.ProfileName = "production";

		// Assert
		options.ProfileName.ShouldBe("production");
	}

	[Fact]
	public void ProfileName_CanBeSetToNull()
	{
		// Arrange
		var options = new DispatchProfileOptions();

		// Act
		options.ProfileName = null!;

		// Assert
		options.ProfileName.ShouldBeNull();
	}

	#endregion

	#region Object Initializer Tests

	[Fact]
	public void ObjectInitializer_SetsAllProperties()
	{
		// Act
		var options = new DispatchProfileOptions
		{
			ProfileName = "development",
		};

		// Assert
		options.ProfileName.ShouldBe("development");
	}

	#endregion

	#region Real-World Scenario Tests

	[Fact]
	public void Options_ForProduction_SetsProductionProfile()
	{
		// Act
		var options = new DispatchProfileOptions
		{
			ProfileName = "production",
		};

		// Assert
		options.ProfileName.ShouldNotBeEmpty();
	}

	[Fact]
	public void Options_ForStaging_SetsStagingProfile()
	{
		// Act
		var options = new DispatchProfileOptions
		{
			ProfileName = "staging",
		};

		// Assert
		options.ProfileName.ShouldBe("staging");
	}

	#endregion
}
