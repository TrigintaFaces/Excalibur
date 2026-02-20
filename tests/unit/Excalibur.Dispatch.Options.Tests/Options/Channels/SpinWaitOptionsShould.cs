// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Options.Channels;

namespace Excalibur.Dispatch.Tests.Options.Channels;

/// <summary>
/// Unit tests for <see cref="SpinWaitOptions"/>.
/// </summary>
/// <remarks>
/// Tests the spin wait options class.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Options")]
[Trait("Priority", "0")]
public sealed class SpinWaitOptionsShould
{
	#region Default Values Tests

	[Fact]
	public void Default_SpinCountIsTen()
	{
		// Arrange & Act
		var options = new SpinWaitOptions();

		// Assert
		options.SpinCount.ShouldBe(10);
	}

	[Fact]
	public void Default_DelayMillisecondsIsOne()
	{
		// Arrange & Act
		var options = new SpinWaitOptions();

		// Assert
		options.DelayMilliseconds.ShouldBe(1);
	}

	[Fact]
	public void Default_AggressiveSpinIsFalse()
	{
		// Arrange & Act
		var options = new SpinWaitOptions();

		// Assert
		options.AggressiveSpin.ShouldBeFalse();
	}

	[Fact]
	public void Default_SpinIterationsIsOneHundred()
	{
		// Arrange & Act
		var options = new SpinWaitOptions();

		// Assert
		options.SpinIterations.ShouldBe(100);
	}

	[Fact]
	public void Default_MaxSpinCyclesIsTen()
	{
		// Arrange & Act
		var options = new SpinWaitOptions();

		// Assert
		options.MaxSpinCycles.ShouldBe(10);
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void SpinCount_CanBeSet()
	{
		// Arrange
		var options = new SpinWaitOptions();

		// Act
		options.SpinCount = 50;

		// Assert
		options.SpinCount.ShouldBe(50);
	}

	[Fact]
	public void SpinCount_CanBeSetToZero()
	{
		// Arrange
		var options = new SpinWaitOptions();

		// Act
		options.SpinCount = 0;

		// Assert
		options.SpinCount.ShouldBe(0);
	}

	[Fact]
	public void DelayMilliseconds_CanBeSet()
	{
		// Arrange
		var options = new SpinWaitOptions();

		// Act
		options.DelayMilliseconds = 100;

		// Assert
		options.DelayMilliseconds.ShouldBe(100);
	}

	[Fact]
	public void DelayMilliseconds_CanBeSetToZero()
	{
		// Arrange
		var options = new SpinWaitOptions();

		// Act
		options.DelayMilliseconds = 0;

		// Assert
		options.DelayMilliseconds.ShouldBe(0);
	}

	[Fact]
	public void AggressiveSpin_CanBeSetToTrue()
	{
		// Arrange
		var options = new SpinWaitOptions();

		// Act
		options.AggressiveSpin = true;

		// Assert
		options.AggressiveSpin.ShouldBeTrue();
	}

	[Fact]
	public void SpinIterations_CanBeSet()
	{
		// Arrange
		var options = new SpinWaitOptions();

		// Act
		options.SpinIterations = 500;

		// Assert
		options.SpinIterations.ShouldBe(500);
	}

	[Fact]
	public void MaxSpinCycles_CanBeSet()
	{
		// Arrange
		var options = new SpinWaitOptions();

		// Act
		options.MaxSpinCycles = 25;

		// Assert
		options.MaxSpinCycles.ShouldBe(25);
	}

	#endregion

	#region Object Initializer Tests

	[Fact]
	public void ObjectInitializer_SetsAllProperties()
	{
		// Act
		var options = new SpinWaitOptions
		{
			SpinCount = 20,
			DelayMilliseconds = 5,
			AggressiveSpin = true,
			SpinIterations = 200,
			MaxSpinCycles = 15,
		};

		// Assert
		options.SpinCount.ShouldBe(20);
		options.DelayMilliseconds.ShouldBe(5);
		options.AggressiveSpin.ShouldBeTrue();
		options.SpinIterations.ShouldBe(200);
		options.MaxSpinCycles.ShouldBe(15);
	}

	#endregion

	#region Edge Case Tests

	[Fact]
	public void SpinCount_CanBeNegative()
	{
		// Arrange
		var options = new SpinWaitOptions();

		// Act
		options.SpinCount = -1;

		// Assert
		options.SpinCount.ShouldBe(-1);
	}

	[Fact]
	public void DelayMilliseconds_CanBeNegative()
	{
		// Arrange
		var options = new SpinWaitOptions();

		// Act
		options.DelayMilliseconds = -1;

		// Assert
		options.DelayMilliseconds.ShouldBe(-1);
	}

	[Fact]
	public void SpinIterations_CanBeSetToMaxValue()
	{
		// Arrange
		var options = new SpinWaitOptions();

		// Act
		options.SpinIterations = int.MaxValue;

		// Assert
		options.SpinIterations.ShouldBe(int.MaxValue);
	}

	[Fact]
	public void MaxSpinCycles_CanBeSetToMaxValue()
	{
		// Arrange
		var options = new SpinWaitOptions();

		// Act
		options.MaxSpinCycles = int.MaxValue;

		// Assert
		options.MaxSpinCycles.ShouldBe(int.MaxValue);
	}

	#endregion
}
