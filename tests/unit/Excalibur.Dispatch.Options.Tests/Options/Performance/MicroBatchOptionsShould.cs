// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Options.Performance;

namespace Excalibur.Dispatch.Tests.Options.Performance;

/// <summary>
/// Unit tests for <see cref="MicroBatchOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Options")]
[Trait("Priority", "0")]
public sealed class MicroBatchOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void Default_MaxBatchSize_Is100()
	{
		// Arrange & Act
		var options = new MicroBatchOptions();

		// Assert
		options.MaxBatchSize.ShouldBe(100);
	}

	[Fact]
	public void Default_MaxBatchDelay_Is100Milliseconds()
	{
		// Arrange & Act
		var options = new MicroBatchOptions();

		// Assert
		options.MaxBatchDelay.ShouldBe(TimeSpan.FromMilliseconds(100));
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void MaxBatchSize_CanBeSet()
	{
		// Arrange
		var options = new MicroBatchOptions();

		// Act
		options.MaxBatchSize = 500;

		// Assert
		options.MaxBatchSize.ShouldBe(500);
	}

	[Fact]
	public void MaxBatchDelay_CanBeSet()
	{
		// Arrange
		var options = new MicroBatchOptions();

		// Act
		options.MaxBatchDelay = TimeSpan.FromMilliseconds(250);

		// Assert
		options.MaxBatchDelay.ShouldBe(TimeSpan.FromMilliseconds(250));
	}

	#endregion

	#region Object Initializer Tests

	[Fact]
	public void ObjectInitializer_SetsAllProperties()
	{
		// Act
		var options = new MicroBatchOptions
		{
			MaxBatchSize = 200,
			MaxBatchDelay = TimeSpan.FromMilliseconds(50),
		};

		// Assert
		options.MaxBatchSize.ShouldBe(200);
		options.MaxBatchDelay.ShouldBe(TimeSpan.FromMilliseconds(50));
	}

	#endregion

	#region Real-World Scenario Tests

	[Fact]
	public void Options_ForHighThroughput_HasLargeBatchSize()
	{
		// Act
		var options = new MicroBatchOptions
		{
			MaxBatchSize = 1000,
			MaxBatchDelay = TimeSpan.FromMilliseconds(200),
		};

		// Assert
		options.MaxBatchSize.ShouldBeGreaterThan(100);
		options.MaxBatchDelay.ShouldBeGreaterThan(TimeSpan.FromMilliseconds(100));
	}

	[Fact]
	public void Options_ForLowLatency_HasSmallBatchAndShortDelay()
	{
		// Act
		var options = new MicroBatchOptions
		{
			MaxBatchSize = 10,
			MaxBatchDelay = TimeSpan.FromMilliseconds(10),
		};

		// Assert
		options.MaxBatchSize.ShouldBeLessThan(100);
		options.MaxBatchDelay.ShouldBeLessThan(TimeSpan.FromMilliseconds(100));
	}

	[Fact]
	public void Options_ForBulkProcessing_HasVeryLargeBatch()
	{
		// Act
		var options = new MicroBatchOptions
		{
			MaxBatchSize = 5000,
			MaxBatchDelay = TimeSpan.FromSeconds(1),
		};

		// Assert
		options.MaxBatchSize.ShouldBe(5000);
		options.MaxBatchDelay.ShouldBe(TimeSpan.FromSeconds(1));
	}

	#endregion
}
