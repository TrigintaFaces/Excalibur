// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Threading.Channels;

using Excalibur.Dispatch.Channels;
using Excalibur.Dispatch.Options.Channels;

using FakeItEasy;

namespace Excalibur.Dispatch.Tests.Options.Channels;

/// <summary>
/// Unit tests for <see cref="DispatchChannelOptions"/>.
/// </summary>
/// <remarks>
/// Tests the dispatch channel options class.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Options")]
[Trait("Priority", "0")]
public sealed class DispatchChannelOptionsShould
{
	#region Default Values Tests

	[Fact]
	public void Default_ModeIsUnbounded()
	{
		// Arrange & Act
		var options = new DispatchChannelOptions();

		// Assert
		options.Mode.ShouldBe(ChannelMode.Unbounded);
	}

	[Fact]
	public void Default_CapacityIsNull()
	{
		// Arrange & Act
		var options = new DispatchChannelOptions();

		// Assert
		options.Capacity.ShouldBeNull();
	}

	[Fact]
	public void Default_FullModeIsWait()
	{
		// Arrange & Act
		var options = new DispatchChannelOptions();

		// Assert
		options.FullMode.ShouldBe(BoundedChannelFullMode.Wait);
	}

	[Fact]
	public void Default_SingleReaderIsFalse()
	{
		// Arrange & Act
		var options = new DispatchChannelOptions();

		// Assert
		options.SingleReader.ShouldBeFalse();
	}

	[Fact]
	public void Default_SingleWriterIsFalse()
	{
		// Arrange & Act
		var options = new DispatchChannelOptions();

		// Assert
		options.SingleWriter.ShouldBeFalse();
	}

	[Fact]
	public void Default_AllowSynchronousContinuationsIsTrue()
	{
		// Arrange & Act
		var options = new DispatchChannelOptions();

		// Assert
		options.AllowSynchronousContinuations.ShouldBeTrue();
	}

	[Fact]
	public void Default_WaitStrategyIsNull()
	{
		// Arrange & Act
		var options = new DispatchChannelOptions();

		// Assert
		options.WaitStrategy.ShouldBeNull();
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void Mode_CanBeSetToBounded()
	{
		// Arrange
		var options = new DispatchChannelOptions();

		// Act
		options.Mode = ChannelMode.Bounded;

		// Assert
		options.Mode.ShouldBe(ChannelMode.Bounded);
	}

	[Fact]
	public void Mode_CanBeSetToUnbounded()
	{
		// Arrange
		var options = new DispatchChannelOptions { Mode = ChannelMode.Bounded };

		// Act
		options.Mode = ChannelMode.Unbounded;

		// Assert
		options.Mode.ShouldBe(ChannelMode.Unbounded);
	}

	[Fact]
	public void Capacity_CanBeSet()
	{
		// Arrange
		var options = new DispatchChannelOptions();

		// Act
		options.Capacity = 100;

		// Assert
		options.Capacity.ShouldBe(100);
	}

	[Fact]
	public void Capacity_CanBeSetToZero()
	{
		// Arrange
		var options = new DispatchChannelOptions();

		// Act
		options.Capacity = 0;

		// Assert
		options.Capacity.ShouldBe(0);
	}

	[Fact]
	public void Capacity_CanBeSetToLargeValue()
	{
		// Arrange
		var options = new DispatchChannelOptions();

		// Act
		options.Capacity = int.MaxValue;

		// Assert
		options.Capacity.ShouldBe(int.MaxValue);
	}

	[Fact]
	public void FullMode_CanBeSetToDropNewest()
	{
		// Arrange
		var options = new DispatchChannelOptions();

		// Act
		options.FullMode = BoundedChannelFullMode.DropNewest;

		// Assert
		options.FullMode.ShouldBe(BoundedChannelFullMode.DropNewest);
	}

	[Fact]
	public void FullMode_CanBeSetToDropOldest()
	{
		// Arrange
		var options = new DispatchChannelOptions();

		// Act
		options.FullMode = BoundedChannelFullMode.DropOldest;

		// Assert
		options.FullMode.ShouldBe(BoundedChannelFullMode.DropOldest);
	}

	[Fact]
	public void FullMode_CanBeSetToDropWrite()
	{
		// Arrange
		var options = new DispatchChannelOptions();

		// Act
		options.FullMode = BoundedChannelFullMode.DropWrite;

		// Assert
		options.FullMode.ShouldBe(BoundedChannelFullMode.DropWrite);
	}

	[Fact]
	public void SingleReader_CanBeSetToTrue()
	{
		// Arrange
		var options = new DispatchChannelOptions();

		// Act
		options.SingleReader = true;

		// Assert
		options.SingleReader.ShouldBeTrue();
	}

	[Fact]
	public void SingleWriter_CanBeSetToTrue()
	{
		// Arrange
		var options = new DispatchChannelOptions();

		// Act
		options.SingleWriter = true;

		// Assert
		options.SingleWriter.ShouldBeTrue();
	}

	[Fact]
	public void AllowSynchronousContinuations_CanBeSetToFalse()
	{
		// Arrange
		var options = new DispatchChannelOptions();

		// Act
		options.AllowSynchronousContinuations = false;

		// Assert
		options.AllowSynchronousContinuations.ShouldBeFalse();
	}

	[Fact]
	public void WaitStrategy_CanBeSet()
	{
		// Arrange
		var options = new DispatchChannelOptions();
		var strategy = A.Fake<IWaitStrategy>();

		// Act
		options.WaitStrategy = strategy;

		// Assert
		options.WaitStrategy.ShouldBe(strategy);
	}

	#endregion

	#region Object Initializer Tests

	[Fact]
	public void ObjectInitializer_SetsAllProperties()
	{
		// Arrange
		var strategy = A.Fake<IWaitStrategy>();

		// Act
		var options = new DispatchChannelOptions
		{
			Mode = ChannelMode.Bounded,
			Capacity = 500,
			FullMode = BoundedChannelFullMode.DropOldest,
			SingleReader = true,
			SingleWriter = true,
			AllowSynchronousContinuations = false,
			WaitStrategy = strategy,
		};

		// Assert
		options.Mode.ShouldBe(ChannelMode.Bounded);
		options.Capacity.ShouldBe(500);
		options.FullMode.ShouldBe(BoundedChannelFullMode.DropOldest);
		options.SingleReader.ShouldBeTrue();
		options.SingleWriter.ShouldBeTrue();
		options.AllowSynchronousContinuations.ShouldBeFalse();
		options.WaitStrategy.ShouldBe(strategy);
	}

	#endregion
}
