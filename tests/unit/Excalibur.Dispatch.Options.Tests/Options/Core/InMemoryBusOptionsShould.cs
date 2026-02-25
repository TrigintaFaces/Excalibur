// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Options.Core;

namespace Excalibur.Dispatch.Tests.Options.Core;

/// <summary>
/// Unit tests for <see cref="InMemoryBusOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Options")]
[Trait("Priority", "0")]
public sealed class InMemoryBusOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void Default_MaxQueueLength_IsOneThousand()
	{
		// Arrange & Act
		var options = new InMemoryBusOptions();

		// Assert
		options.MaxQueueLength.ShouldBe(1000);
	}

	[Fact]
	public void Default_PreserveOrder_IsTrue()
	{
		// Arrange & Act
		var options = new InMemoryBusOptions();

		// Assert
		options.PreserveOrder.ShouldBeTrue();
	}

	[Fact]
	public void Default_ProcessingDelay_IsZero()
	{
		// Arrange & Act
		var options = new InMemoryBusOptions();

		// Assert
		options.ProcessingDelay.ShouldBe(TimeSpan.Zero);
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void MaxQueueLength_CanBeSet()
	{
		// Arrange
		var options = new InMemoryBusOptions();

		// Act
		options.MaxQueueLength = 5000;

		// Assert
		options.MaxQueueLength.ShouldBe(5000);
	}

	[Fact]
	public void PreserveOrder_CanBeSet()
	{
		// Arrange
		var options = new InMemoryBusOptions();

		// Act
		options.PreserveOrder = false;

		// Assert
		options.PreserveOrder.ShouldBeFalse();
	}

	[Fact]
	public void ProcessingDelay_CanBeSet()
	{
		// Arrange
		var options = new InMemoryBusOptions();

		// Act
		options.ProcessingDelay = TimeSpan.FromMilliseconds(100);

		// Assert
		options.ProcessingDelay.ShouldBe(TimeSpan.FromMilliseconds(100));
	}

	#endregion

	#region Object Initializer Tests

	[Fact]
	public void ObjectInitializer_SetsAllProperties()
	{
		// Act
		var options = new InMemoryBusOptions
		{
			MaxQueueLength = 2000,
			PreserveOrder = false,
			ProcessingDelay = TimeSpan.FromMilliseconds(50),
		};

		// Assert
		options.MaxQueueLength.ShouldBe(2000);
		options.PreserveOrder.ShouldBeFalse();
		options.ProcessingDelay.ShouldBe(TimeSpan.FromMilliseconds(50));
	}

	#endregion

	#region Real-World Scenario Tests

	[Fact]
	public void Options_ForHighThroughput_HasLargeQueueLength()
	{
		// Act
		var options = new InMemoryBusOptions
		{
			MaxQueueLength = 10000,
			PreserveOrder = true,
		};

		// Assert
		options.MaxQueueLength.ShouldBeGreaterThan(1000);
	}

	[Fact]
	public void Options_ForTesting_HasProcessingDelay()
	{
		// Act
		var options = new InMemoryBusOptions
		{
			ProcessingDelay = TimeSpan.FromMilliseconds(100),
		};

		// Assert
		options.ProcessingDelay.ShouldBeGreaterThan(TimeSpan.Zero);
	}

	[Fact]
	public void Options_ForUnorderedProcessing_DisablesPreserveOrder()
	{
		// Act
		var options = new InMemoryBusOptions
		{
			PreserveOrder = false,
			MaxQueueLength = 5000,
		};

		// Assert
		options.PreserveOrder.ShouldBeFalse();
	}

	#endregion
}
