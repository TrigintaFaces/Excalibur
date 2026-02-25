// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Options.Validation;

namespace Excalibur.Dispatch.Tests.Options.Validation;

/// <summary>
/// Unit tests for <see cref="VersioningOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Options")]
[Trait("Priority", "0")]
public sealed class VersioningOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void Default_Enabled_IsTrue()
	{
		// Arrange & Act
		var options = new VersioningOptions();

		// Assert
		options.Enabled.ShouldBeTrue();
	}

	[Fact]
	public void Default_RequireContractVersion_IsTrue()
	{
		// Arrange & Act
		var options = new VersioningOptions();

		// Assert
		options.RequireContractVersion.ShouldBeTrue();
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void Enabled_CanBeSet()
	{
		// Arrange
		var options = new VersioningOptions();

		// Act
		options.Enabled = false;

		// Assert
		options.Enabled.ShouldBeFalse();
	}

	[Fact]
	public void RequireContractVersion_CanBeSet()
	{
		// Arrange
		var options = new VersioningOptions();

		// Act
		options.RequireContractVersion = false;

		// Assert
		options.RequireContractVersion.ShouldBeFalse();
	}

	#endregion

	#region Object Initializer Tests

	[Fact]
	public void ObjectInitializer_SetsAllProperties()
	{
		// Act
		var options = new VersioningOptions
		{
			Enabled = true,
			RequireContractVersion = false,
		};

		// Assert
		options.Enabled.ShouldBeTrue();
		options.RequireContractVersion.ShouldBeFalse();
	}

	#endregion

	#region Real-World Scenario Tests

	[Fact]
	public void Options_ForStrictVersioning_EnablesAllChecks()
	{
		// Act
		var options = new VersioningOptions
		{
			Enabled = true,
			RequireContractVersion = true,
		};

		// Assert
		options.Enabled.ShouldBeTrue();
		options.RequireContractVersion.ShouldBeTrue();
	}

	[Fact]
	public void Options_ForLenientVersioning_DisablesRequirement()
	{
		// Act
		var options = new VersioningOptions
		{
			Enabled = true,
			RequireContractVersion = false,
		};

		// Assert
		options.Enabled.ShouldBeTrue();
		options.RequireContractVersion.ShouldBeFalse();
	}

	[Fact]
	public void Options_ForDisabledVersioning_TurnsOffAllChecks()
	{
		// Act
		var options = new VersioningOptions
		{
			Enabled = false,
			RequireContractVersion = false,
		};

		// Assert
		options.Enabled.ShouldBeFalse();
	}

	#endregion
}
