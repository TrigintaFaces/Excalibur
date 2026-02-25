// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Pooling.Configuration;
using Excalibur.Dispatch.Options.Pooling;

namespace Excalibur.Dispatch.Tests.Options.Pooling;

/// <summary>
/// Unit tests for <see cref="TypePoolOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Options")]
[Trait("Priority", "0")]
public sealed class TypePoolOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void Default_MaxPoolSize_IsZero()
	{
		// Arrange & Act
		var options = new TypePoolOptions();

		// Assert - 0 means use default
		options.MaxPoolSize.ShouldBe(0);
	}

	[Fact]
	public void Default_Enabled_IsTrue()
	{
		// Arrange & Act
		var options = new TypePoolOptions();

		// Assert
		options.Enabled.ShouldBeTrue();
	}

	[Fact]
	public void Default_ResetStrategy_IsAuto()
	{
		// Arrange & Act
		var options = new TypePoolOptions();

		// Assert
		options.ResetStrategy.ShouldBe(ResetStrategy.Auto);
	}

	[Fact]
	public void Default_PreWarm_IsFalse()
	{
		// Arrange & Act
		var options = new TypePoolOptions();

		// Assert
		options.PreWarm.ShouldBeFalse();
	}

	[Fact]
	public void Default_PreWarmCount_IsZero()
	{
		// Arrange & Act
		var options = new TypePoolOptions();

		// Assert
		options.PreWarmCount.ShouldBe(0);
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void MaxPoolSize_CanBeSet()
	{
		// Arrange
		var options = new TypePoolOptions();

		// Act
		options.MaxPoolSize = 500;

		// Assert
		options.MaxPoolSize.ShouldBe(500);
	}

	[Fact]
	public void Enabled_CanBeSet()
	{
		// Arrange
		var options = new TypePoolOptions();

		// Act
		options.Enabled = false;

		// Assert
		options.Enabled.ShouldBeFalse();
	}

	[Fact]
	public void ResetStrategy_CanBeSet()
	{
		// Arrange
		var options = new TypePoolOptions();

		// Act
		options.ResetStrategy = ResetStrategy.Interface;

		// Assert
		options.ResetStrategy.ShouldBe(ResetStrategy.Interface);
	}

	[Fact]
	public void PreWarm_CanBeSet()
	{
		// Arrange
		var options = new TypePoolOptions();

		// Act
		options.PreWarm = true;

		// Assert
		options.PreWarm.ShouldBeTrue();
	}

	[Fact]
	public void PreWarmCount_CanBeSet()
	{
		// Arrange
		var options = new TypePoolOptions();

		// Act
		options.PreWarmCount = 50;

		// Assert
		options.PreWarmCount.ShouldBe(50);
	}

	#endregion

	#region Object Initializer Tests

	[Fact]
	public void ObjectInitializer_SetsAllProperties()
	{
		// Act
		var options = new TypePoolOptions
		{
			MaxPoolSize = 100,
			Enabled = false,
			ResetStrategy = ResetStrategy.SourceGenerated,
			PreWarm = true,
			PreWarmCount = 25,
		};

		// Assert
		options.MaxPoolSize.ShouldBe(100);
		options.Enabled.ShouldBeFalse();
		options.ResetStrategy.ShouldBe(ResetStrategy.SourceGenerated);
		options.PreWarm.ShouldBeTrue();
		options.PreWarmCount.ShouldBe(25);
	}

	#endregion

	#region Real-World Scenario Tests

	[Fact]
	public void Options_ForFrequentMessage_HasPreWarming()
	{
		// Act
		var options = new TypePoolOptions
		{
			MaxPoolSize = 200,
			PreWarm = true,
			PreWarmCount = 50,
		};

		// Assert
		options.PreWarm.ShouldBeTrue();
		options.PreWarmCount.ShouldBeGreaterThan(0);
	}

	[Fact]
	public void Options_ForStatelessMessage_DisablesReset()
	{
		// Act
		var options = new TypePoolOptions
		{
			ResetStrategy = ResetStrategy.None,
		};

		// Assert
		options.ResetStrategy.ShouldBe(ResetStrategy.None);
	}

	[Fact]
	public void Options_ForDisabledPooling_HasEnabledFalse()
	{
		// Act
		var options = new TypePoolOptions
		{
			Enabled = false,
		};

		// Assert
		options.Enabled.ShouldBeFalse();
	}

	#endregion
}
