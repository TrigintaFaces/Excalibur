// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Pooling.Configuration;
using Excalibur.Dispatch.Options.Pooling;

namespace Excalibur.Dispatch.Tests.Options.Pooling;

/// <summary>
/// Unit tests for <see cref="MessagePoolOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Options")]
[Trait("Priority", "0")]
public sealed class MessagePoolOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void Default_Enabled_IsTrue()
	{
		// Arrange & Act
		var options = new MessagePoolOptions();

		// Assert
		options.Enabled.ShouldBeTrue();
	}

	[Fact]
	public void Default_MaxPoolSizePerType_IsProcessorCountTimesEight()
	{
		// Arrange & Act
		var options = new MessagePoolOptions();

		// Assert
		options.MaxPoolSizePerType.ShouldBe(Environment.ProcessorCount * 8);
	}

	[Fact]
	public void Default_AggressivePooling_IsTrue()
	{
		// Arrange & Act
		var options = new MessagePoolOptions();

		// Assert
		options.AggressivePooling.ShouldBeTrue();
	}

	[Fact]
	public void Default_TypeConfigurations_IsEmpty()
	{
		// Arrange & Act
		var options = new MessagePoolOptions();

		// Assert
		_ = options.TypeConfigurations.ShouldNotBeNull();
		options.TypeConfigurations.ShouldBeEmpty();
	}

	[Fact]
	public void Default_DefaultResetStrategy_IsAuto()
	{
		// Arrange & Act
		var options = new MessagePoolOptions();

		// Assert
		options.DefaultResetStrategy.ShouldBe(ResetStrategy.Auto);
	}

	[Fact]
	public void Default_TrimBehavior_IsAdaptive()
	{
		// Arrange & Act
		var options = new MessagePoolOptions();

		// Assert
		options.TrimBehavior.ShouldBe(TrimBehavior.Adaptive);
	}

	[Fact]
	public void Default_MaxTrackedTypes_IsOneHundred()
	{
		// Arrange & Act
		var options = new MessagePoolOptions();

		// Assert
		options.MaxTrackedTypes.ShouldBe(100);
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void Enabled_CanBeSet()
	{
		// Arrange
		var options = new MessagePoolOptions();

		// Act
		options.Enabled = false;

		// Assert
		options.Enabled.ShouldBeFalse();
	}

	[Fact]
	public void MaxPoolSizePerType_CanBeSet()
	{
		// Arrange
		var options = new MessagePoolOptions();

		// Act
		options.MaxPoolSizePerType = 200;

		// Assert
		options.MaxPoolSizePerType.ShouldBe(200);
	}

	[Fact]
	public void AggressivePooling_CanBeSet()
	{
		// Arrange
		var options = new MessagePoolOptions();

		// Act
		options.AggressivePooling = false;

		// Assert
		options.AggressivePooling.ShouldBeFalse();
	}

	[Fact]
	public void TypeConfigurations_CanAddEntries()
	{
		// Arrange
		var options = new MessagePoolOptions();
		var typeOptions = new TypePoolOptions();

		// Act
		options.TypeConfigurations["OrderCommand"] = typeOptions;

		// Assert
		options.TypeConfigurations.Count.ShouldBe(1);
		options.TypeConfigurations["OrderCommand"].ShouldBe(typeOptions);
	}

	[Fact]
	public void DefaultResetStrategy_CanBeSet()
	{
		// Arrange
		var options = new MessagePoolOptions();

		// Act
		options.DefaultResetStrategy = ResetStrategy.Interface;

		// Assert
		options.DefaultResetStrategy.ShouldBe(ResetStrategy.Interface);
	}

	[Fact]
	public void TrimBehavior_CanBeSet()
	{
		// Arrange
		var options = new MessagePoolOptions();

		// Act
		options.TrimBehavior = TrimBehavior.Aggressive;

		// Assert
		options.TrimBehavior.ShouldBe(TrimBehavior.Aggressive);
	}

	[Fact]
	public void MaxTrackedTypes_CanBeSet()
	{
		// Arrange
		var options = new MessagePoolOptions();

		// Act
		options.MaxTrackedTypes = 500;

		// Assert
		options.MaxTrackedTypes.ShouldBe(500);
	}

	#endregion

	#region Object Initializer Tests

	[Fact]
	public void ObjectInitializer_SetsAllProperties()
	{
		// Act
		var options = new MessagePoolOptions
		{
			Enabled = false,
			MaxPoolSizePerType = 50,
			AggressivePooling = false,
			DefaultResetStrategy = ResetStrategy.SourceGenerated,
			TrimBehavior = TrimBehavior.Fixed,
			MaxTrackedTypes = 200,
		};

		// Assert
		options.Enabled.ShouldBeFalse();
		options.MaxPoolSizePerType.ShouldBe(50);
		options.AggressivePooling.ShouldBeFalse();
		options.DefaultResetStrategy.ShouldBe(ResetStrategy.SourceGenerated);
		options.TrimBehavior.ShouldBe(TrimBehavior.Fixed);
		options.MaxTrackedTypes.ShouldBe(200);
	}

	#endregion

	#region Real-World Scenario Tests

	[Fact]
	public void Options_ForHighThroughput_HasLargePoolSize()
	{
		// Act
		var options = new MessagePoolOptions
		{
			MaxPoolSizePerType = 500,
			AggressivePooling = true,
			MaxTrackedTypes = 200,
		};

		// Assert
		options.MaxPoolSizePerType.ShouldBeGreaterThan(100);
		options.AggressivePooling.ShouldBeTrue();
	}

	[Fact]
	public void Options_WithTypeSpecificConfig_HasCustomConfigurations()
	{
		// Act
		var options = new MessagePoolOptions();
		options.TypeConfigurations["OrderCommand"] = new TypePoolOptions();
		options.TypeConfigurations["QueryCommand"] = new TypePoolOptions();

		// Assert
		options.TypeConfigurations.Count.ShouldBe(2);
		options.TypeConfigurations.ShouldContainKey("OrderCommand");
		options.TypeConfigurations.ShouldContainKey("QueryCommand");
	}

	#endregion
}
