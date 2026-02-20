// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Options.CloudEvents;

namespace Excalibur.Dispatch.Tests.Options.CloudEvents;

/// <summary>
/// Unit tests for <see cref="CloudEventBatchOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Options")]
[Trait("Priority", "0")]
public sealed class CloudEventBatchOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void Default_MaxEvents_Is100()
	{
		// Arrange & Act
		var options = new CloudEventBatchOptions();

		// Assert
		options.MaxEvents.ShouldBe(100);
	}

	[Fact]
	public void Default_MaxBatchSizeBytes_Is1MB()
	{
		// Arrange & Act
		var options = new CloudEventBatchOptions();

		// Assert
		options.MaxBatchSizeBytes.ShouldBe(1024 * 1024);
	}

	[Fact]
	public void Default_InitialCapacity_Is10()
	{
		// Arrange & Act
		var options = new CloudEventBatchOptions();

		// Assert
		options.InitialCapacity.ShouldBe(10);
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void MaxEvents_CanBeSet()
	{
		// Arrange
		var options = new CloudEventBatchOptions();

		// Act
		options.MaxEvents = 500;

		// Assert
		options.MaxEvents.ShouldBe(500);
	}

	[Fact]
	public void MaxBatchSizeBytes_CanBeSet()
	{
		// Arrange
		var options = new CloudEventBatchOptions();

		// Act
		options.MaxBatchSizeBytes = 2 * 1024 * 1024;

		// Assert
		options.MaxBatchSizeBytes.ShouldBe(2 * 1024 * 1024);
	}

	[Fact]
	public void InitialCapacity_CanBeSet()
	{
		// Arrange
		var options = new CloudEventBatchOptions();

		// Act
		options.InitialCapacity = 50;

		// Assert
		options.InitialCapacity.ShouldBe(50);
	}

	#endregion

	#region Object Initializer Tests

	[Fact]
	public void ObjectInitializer_SetsAllProperties()
	{
		// Act
		var options = new CloudEventBatchOptions
		{
			MaxEvents = 200,
			MaxBatchSizeBytes = 512 * 1024,
			InitialCapacity = 25,
		};

		// Assert
		options.MaxEvents.ShouldBe(200);
		options.MaxBatchSizeBytes.ShouldBe(512 * 1024);
		options.InitialCapacity.ShouldBe(25);
	}

	#endregion

	#region Real-World Scenario Tests

	[Fact]
	public void Options_ForHighThroughput_HasLargeBatches()
	{
		// Act
		var options = new CloudEventBatchOptions
		{
			MaxEvents = 1000,
			MaxBatchSizeBytes = 5 * 1024 * 1024,
			InitialCapacity = 100,
		};

		// Assert
		options.MaxEvents.ShouldBeGreaterThan(100);
		options.MaxBatchSizeBytes.ShouldBeGreaterThan(1024 * 1024);
	}

	[Fact]
	public void Options_ForLowLatency_HasSmallBatches()
	{
		// Act
		var options = new CloudEventBatchOptions
		{
			MaxEvents = 10,
			MaxBatchSizeBytes = 64 * 1024,
			InitialCapacity = 5,
		};

		// Assert
		options.MaxEvents.ShouldBeLessThan(100);
		options.MaxBatchSizeBytes.ShouldBeLessThan(1024 * 1024);
	}

	#endregion
}
