// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Threading.Channels;

using Excalibur.Dispatch.Channels;
using Excalibur.Dispatch.Options.Channels;

using FakeItEasy;

namespace Excalibur.Dispatch.Tests.Options.Channels;

/// <summary>
/// Unit tests for <see cref="BoundedDispatchChannelOptions"/>.
/// </summary>
/// <remarks>
/// Tests the bounded dispatch channel options class.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Options")]
[Trait("Priority", "0")]
public sealed class BoundedDispatchChannelOptionsShould
{
	#region Constructor Tests

	[Fact]
	public void Constructor_Default_SetsModeToBindded()
	{
		// Arrange & Act
		var options = new BoundedDispatchChannelOptions();

		// Assert
		options.Mode.ShouldBe(ChannelMode.Bounded);
	}

	[Fact]
	public void Constructor_Default_SetsCapacityTo100()
	{
		// Arrange & Act
		var options = new BoundedDispatchChannelOptions();

		// Assert
		options.Capacity.ShouldBe(100);
	}

	[Fact]
	public void Constructor_WithCapacity_SetsModeToBindded()
	{
		// Arrange & Act
		var options = new BoundedDispatchChannelOptions(500);

		// Assert
		options.Mode.ShouldBe(ChannelMode.Bounded);
	}

	[Fact]
	public void Constructor_WithCapacity_SetsCapacity()
	{
		// Arrange & Act
		var options = new BoundedDispatchChannelOptions(500);

		// Assert
		options.Capacity.ShouldBe(500);
	}

	[Fact]
	public void Constructor_WithZeroCapacity_SetsCapacityToZero()
	{
		// Arrange & Act
		var options = new BoundedDispatchChannelOptions(0);

		// Assert
		options.Capacity.ShouldBe(0);
	}

	[Fact]
	public void Constructor_WithLargeCapacity_SetsCapacity()
	{
		// Arrange & Act
		var options = new BoundedDispatchChannelOptions(int.MaxValue);

		// Assert
		options.Capacity.ShouldBe(int.MaxValue);
	}

	#endregion

	#region Default Values Tests

	[Fact]
	public void Default_WriterWaitStrategyIsNull()
	{
		// Arrange & Act
		var options = new BoundedDispatchChannelOptions();

		// Assert
		options.WriterWaitStrategy.ShouldBeNull();
	}

	[Fact]
	public void Default_ReaderWaitStrategyIsNull()
	{
		// Arrange & Act
		var options = new BoundedDispatchChannelOptions();

		// Assert
		options.ReaderWaitStrategy.ShouldBeNull();
	}

	[Fact]
	public void Default_AggressiveSpinningIsFalse()
	{
		// Arrange & Act
		var options = new BoundedDispatchChannelOptions();

		// Assert
		options.AggressiveSpinning.ShouldBeFalse();
	}

	[Fact]
	public void Default_SpinCountIs100()
	{
		// Arrange & Act
		var options = new BoundedDispatchChannelOptions();

		// Assert
		options.SpinCount.ShouldBe(100);
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void WriterWaitStrategy_CanBeSet()
	{
		// Arrange
		var options = new BoundedDispatchChannelOptions();
		var strategy = A.Fake<IWaitStrategy>();

		// Act
		options.WriterWaitStrategy = strategy;

		// Assert
		options.WriterWaitStrategy.ShouldBe(strategy);
	}

	[Fact]
	public void ReaderWaitStrategy_CanBeSet()
	{
		// Arrange
		var options = new BoundedDispatchChannelOptions();
		var strategy = A.Fake<IWaitStrategy>();

		// Act
		options.ReaderWaitStrategy = strategy;

		// Assert
		options.ReaderWaitStrategy.ShouldBe(strategy);
	}

	[Fact]
	public void AggressiveSpinning_CanBeSetToTrue()
	{
		// Arrange
		var options = new BoundedDispatchChannelOptions();

		// Act
		options.AggressiveSpinning = true;

		// Assert
		options.AggressiveSpinning.ShouldBeTrue();
	}

	[Fact]
	public void SpinCount_CanBeSet()
	{
		// Arrange
		var options = new BoundedDispatchChannelOptions();

		// Act
		options.SpinCount = 50;

		// Assert
		options.SpinCount.ShouldBe(50);
	}

	[Fact]
	public void SpinCount_CanBeSetToZero()
	{
		// Arrange
		var options = new BoundedDispatchChannelOptions();

		// Act
		options.SpinCount = 0;

		// Assert
		options.SpinCount.ShouldBe(0);
	}

	#endregion

	#region Inheritance Tests

	[Fact]
	public void InheritsFromDispatchChannelOptions()
	{
		// Arrange & Act
		var options = new BoundedDispatchChannelOptions();

		// Assert
		_ = options.ShouldBeAssignableTo<DispatchChannelOptions>();
	}

	[Fact]
	public void InheritedProperties_CanBeAccessed()
	{
		// Arrange
		var options = new BoundedDispatchChannelOptions();

		// Act
		options.SingleReader = true;
		options.SingleWriter = true;
		options.AllowSynchronousContinuations = false;
		options.FullMode = BoundedChannelFullMode.DropNewest;

		// Assert
		options.SingleReader.ShouldBeTrue();
		options.SingleWriter.ShouldBeTrue();
		options.AllowSynchronousContinuations.ShouldBeFalse();
		options.FullMode.ShouldBe(BoundedChannelFullMode.DropNewest);
	}

	[Fact]
	public void InheritedWaitStrategy_CanBeSet()
	{
		// Arrange
		var options = new BoundedDispatchChannelOptions();
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
		var writerStrategy = A.Fake<IWaitStrategy>();
		var readerStrategy = A.Fake<IWaitStrategy>();

		// Act
		var options = new BoundedDispatchChannelOptions(200)
		{
			WriterWaitStrategy = writerStrategy,
			ReaderWaitStrategy = readerStrategy,
			AggressiveSpinning = true,
			SpinCount = 75,
			SingleReader = true,
			SingleWriter = true,
		};

		// Assert
		options.Mode.ShouldBe(ChannelMode.Bounded);
		options.Capacity.ShouldBe(200);
		options.WriterWaitStrategy.ShouldBe(writerStrategy);
		options.ReaderWaitStrategy.ShouldBe(readerStrategy);
		options.AggressiveSpinning.ShouldBeTrue();
		options.SpinCount.ShouldBe(75);
		options.SingleReader.ShouldBeTrue();
		options.SingleWriter.ShouldBeTrue();
	}

	#endregion
}
