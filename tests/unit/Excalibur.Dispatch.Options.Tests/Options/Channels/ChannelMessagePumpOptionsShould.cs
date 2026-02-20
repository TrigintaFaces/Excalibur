// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Threading.Channels;

using Excalibur.Dispatch.Options.Channels;

namespace Excalibur.Dispatch.Tests.Options.Channels;

/// <summary>
/// Unit tests for <see cref="ChannelMessagePumpOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Options")]
[Trait("Priority", "0")]
public sealed class ChannelMessagePumpOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void Default_Capacity_IsOneHundred()
	{
		// Arrange & Act
		var options = new ChannelMessagePumpOptions();

		// Assert
		options.Capacity.ShouldBe(100);
	}

	[Fact]
	public void Default_FullMode_IsWait()
	{
		// Arrange & Act
		var options = new ChannelMessagePumpOptions();

		// Assert
		options.FullMode.ShouldBe(BoundedChannelFullMode.Wait);
	}

	[Fact]
	public void Default_AllowSynchronousContinuations_IsFalse()
	{
		// Arrange & Act
		var options = new ChannelMessagePumpOptions();

		// Assert
		options.AllowSynchronousContinuations.ShouldBeFalse();
	}

	[Fact]
	public void Default_ConcurrentConsumers_IsOne()
	{
		// Arrange & Act
		var options = new ChannelMessagePumpOptions();

		// Assert
		options.ConcurrentConsumers.ShouldBe(1);
	}

	[Fact]
	public void Default_SingleReader_IsFalse()
	{
		// Arrange & Act
		var options = new ChannelMessagePumpOptions();

		// Assert
		options.SingleReader.ShouldBeFalse();
	}

	[Fact]
	public void Default_SingleWriter_IsFalse()
	{
		// Arrange & Act
		var options = new ChannelMessagePumpOptions();

		// Assert
		options.SingleWriter.ShouldBeFalse();
	}

	[Fact]
	public void Default_BatchSize_IsTen()
	{
		// Arrange & Act
		var options = new ChannelMessagePumpOptions();

		// Assert
		options.BatchSize.ShouldBe(10);
	}

	[Fact]
	public void Default_BatchTimeoutMs_IsOneThousand()
	{
		// Arrange & Act
		var options = new ChannelMessagePumpOptions();

		// Assert
		options.BatchTimeoutMs.ShouldBe(1000);
	}

	[Fact]
	public void Default_EnableMetrics_IsTrue()
	{
		// Arrange & Act
		var options = new ChannelMessagePumpOptions();

		// Assert
		options.EnableMetrics.ShouldBeTrue();
	}

	[Fact]
	public void Default_PrefetchCount_IsTwenty()
	{
		// Arrange & Act
		var options = new ChannelMessagePumpOptions();

		// Assert
		options.PrefetchCount.ShouldBe(20);
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void Capacity_CanBeSet()
	{
		// Arrange
		var options = new ChannelMessagePumpOptions();

		// Act
		options.Capacity = 500;

		// Assert
		options.Capacity.ShouldBe(500);
	}

	[Fact]
	public void FullMode_CanBeSet()
	{
		// Arrange
		var options = new ChannelMessagePumpOptions();

		// Act
		options.FullMode = BoundedChannelFullMode.DropOldest;

		// Assert
		options.FullMode.ShouldBe(BoundedChannelFullMode.DropOldest);
	}

	[Fact]
	public void AllowSynchronousContinuations_CanBeSet()
	{
		// Arrange
		var options = new ChannelMessagePumpOptions();

		// Act
		options.AllowSynchronousContinuations = true;

		// Assert
		options.AllowSynchronousContinuations.ShouldBeTrue();
	}

	[Fact]
	public void ConcurrentConsumers_CanBeSet()
	{
		// Arrange
		var options = new ChannelMessagePumpOptions();

		// Act
		options.ConcurrentConsumers = 4;

		// Assert
		options.ConcurrentConsumers.ShouldBe(4);
	}

	[Fact]
	public void SingleReader_CanBeSet()
	{
		// Arrange
		var options = new ChannelMessagePumpOptions();

		// Act
		options.SingleReader = true;

		// Assert
		options.SingleReader.ShouldBeTrue();
	}

	[Fact]
	public void SingleWriter_CanBeSet()
	{
		// Arrange
		var options = new ChannelMessagePumpOptions();

		// Act
		options.SingleWriter = true;

		// Assert
		options.SingleWriter.ShouldBeTrue();
	}

	[Fact]
	public void BatchSize_CanBeSet()
	{
		// Arrange
		var options = new ChannelMessagePumpOptions();

		// Act
		options.BatchSize = 50;

		// Assert
		options.BatchSize.ShouldBe(50);
	}

	[Fact]
	public void BatchTimeoutMs_CanBeSet()
	{
		// Arrange
		var options = new ChannelMessagePumpOptions();

		// Act
		options.BatchTimeoutMs = 5000;

		// Assert
		options.BatchTimeoutMs.ShouldBe(5000);
	}

	[Fact]
	public void EnableMetrics_CanBeSet()
	{
		// Arrange
		var options = new ChannelMessagePumpOptions();

		// Act
		options.EnableMetrics = false;

		// Assert
		options.EnableMetrics.ShouldBeFalse();
	}

	[Fact]
	public void PrefetchCount_CanBeSet()
	{
		// Arrange
		var options = new ChannelMessagePumpOptions();

		// Act
		options.PrefetchCount = 100;

		// Assert
		options.PrefetchCount.ShouldBe(100);
	}

	#endregion

	#region Object Initializer Tests

	[Fact]
	public void ObjectInitializer_SetsAllProperties()
	{
		// Act
		var options = new ChannelMessagePumpOptions
		{
			Capacity = 200,
			FullMode = BoundedChannelFullMode.DropNewest,
			AllowSynchronousContinuations = true,
			ConcurrentConsumers = 8,
			SingleReader = true,
			SingleWriter = true,
			BatchSize = 25,
			BatchTimeoutMs = 2000,
			EnableMetrics = false,
			PrefetchCount = 50,
		};

		// Assert
		options.Capacity.ShouldBe(200);
		options.FullMode.ShouldBe(BoundedChannelFullMode.DropNewest);
		options.AllowSynchronousContinuations.ShouldBeTrue();
		options.ConcurrentConsumers.ShouldBe(8);
		options.SingleReader.ShouldBeTrue();
		options.SingleWriter.ShouldBeTrue();
		options.BatchSize.ShouldBe(25);
		options.BatchTimeoutMs.ShouldBe(2000);
		options.EnableMetrics.ShouldBeFalse();
		options.PrefetchCount.ShouldBe(50);
	}

	#endregion

	#region Real-World Scenario Tests

	[Fact]
	public void Options_ForHighThroughput_HasLargeCapacity()
	{
		// Act
		var options = new ChannelMessagePumpOptions
		{
			Capacity = 1000,
			ConcurrentConsumers = Environment.ProcessorCount,
			BatchSize = 100,
			PrefetchCount = 200,
		};

		// Assert
		options.Capacity.ShouldBeGreaterThan(500);
		options.ConcurrentConsumers.ShouldBeGreaterThan(1);
	}

	[Fact]
	public void Options_ForSingleConsumer_OptimizedForSingleReader()
	{
		// Act
		var options = new ChannelMessagePumpOptions
		{
			ConcurrentConsumers = 1,
			SingleReader = true,
			SingleWriter = true,
		};

		// Assert
		options.SingleReader.ShouldBeTrue();
		options.SingleWriter.ShouldBeTrue();
	}

	#endregion
}
