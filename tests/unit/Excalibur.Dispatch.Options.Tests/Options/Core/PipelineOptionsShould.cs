// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Options.Core;

namespace Excalibur.Dispatch.Tests.Options.Core;

/// <summary>
/// Unit tests for <see cref="PipelineOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Options")]
[Trait("Priority", "0")]
public sealed class PipelineOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void Default_MaxConcurrency_IsProcessorCountTimesTwo()
	{
		// Arrange & Act
		var options = new PipelineOptions();

		// Assert
		options.MaxConcurrency.ShouldBe(Environment.ProcessorCount * 2);
	}

	[Fact]
	public void Default_DefaultTimeout_IsThirtySeconds()
	{
		// Arrange & Act
		var options = new PipelineOptions();

		// Assert
		options.DefaultTimeout.ShouldBe(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void Default_EnableParallelProcessing_IsTrue()
	{
		// Arrange & Act
		var options = new PipelineOptions();

		// Assert
		options.EnableParallelProcessing.ShouldBeTrue();
	}

	[Fact]
	public void Default_StopOnFirstError_IsFalse()
	{
		// Arrange & Act
		var options = new PipelineOptions();

		// Assert
		options.StopOnFirstError.ShouldBeFalse();
	}

	[Fact]
	public void Default_BufferSize_IsOneThousand()
	{
		// Arrange & Act
		var options = new PipelineOptions();

		// Assert
		options.BufferSize.ShouldBe(1000);
	}

	[Fact]
	public void Default_ApplicableMessageKinds_IsAllKinds()
	{
		// Arrange & Act
		var options = new PipelineOptions();

		// Assert
		options.ApplicableMessageKinds.ShouldBe(MessageKinds.Action | MessageKinds.Event | MessageKinds.Document);
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void MaxConcurrency_CanBeSet()
	{
		// Arrange
		var options = new PipelineOptions();

		// Act
		options.MaxConcurrency = 50;

		// Assert
		options.MaxConcurrency.ShouldBe(50);
	}

	[Fact]
	public void DefaultTimeout_CanBeSet()
	{
		// Arrange
		var options = new PipelineOptions();

		// Act
		options.DefaultTimeout = TimeSpan.FromMinutes(5);

		// Assert
		options.DefaultTimeout.ShouldBe(TimeSpan.FromMinutes(5));
	}

	[Fact]
	public void EnableParallelProcessing_CanBeSet()
	{
		// Arrange
		var options = new PipelineOptions();

		// Act
		options.EnableParallelProcessing = false;

		// Assert
		options.EnableParallelProcessing.ShouldBeFalse();
	}

	[Fact]
	public void StopOnFirstError_CanBeSet()
	{
		// Arrange
		var options = new PipelineOptions();

		// Act
		options.StopOnFirstError = true;

		// Assert
		options.StopOnFirstError.ShouldBeTrue();
	}

	[Fact]
	public void BufferSize_CanBeSet()
	{
		// Arrange
		var options = new PipelineOptions();

		// Act
		options.BufferSize = 5000;

		// Assert
		options.BufferSize.ShouldBe(5000);
	}

	[Fact]
	public void ApplicableMessageKinds_CanBeSet()
	{
		// Arrange
		var options = new PipelineOptions();

		// Act
		options.ApplicableMessageKinds = MessageKinds.Event;

		// Assert
		options.ApplicableMessageKinds.ShouldBe(MessageKinds.Event);
	}

	#endregion

	#region Object Initializer Tests

	[Fact]
	public void ObjectInitializer_SetsAllProperties()
	{
		// Act
		var options = new PipelineOptions
		{
			MaxConcurrency = 100,
			DefaultTimeout = TimeSpan.FromMinutes(2),
			EnableParallelProcessing = false,
			StopOnFirstError = true,
			BufferSize = 2000,
			ApplicableMessageKinds = MessageKinds.Action | MessageKinds.Event,
		};

		// Assert
		options.MaxConcurrency.ShouldBe(100);
		options.DefaultTimeout.ShouldBe(TimeSpan.FromMinutes(2));
		options.EnableParallelProcessing.ShouldBeFalse();
		options.StopOnFirstError.ShouldBeTrue();
		options.BufferSize.ShouldBe(2000);
		options.ApplicableMessageKinds.ShouldBe(MessageKinds.Action | MessageKinds.Event);
	}

	#endregion

	#region Real-World Scenario Tests

	[Fact]
	public void Options_ForHighThroughput_HasHighConcurrency()
	{
		// Act
		var options = new PipelineOptions
		{
			MaxConcurrency = 100,
			EnableParallelProcessing = true,
			BufferSize = 10000,
		};

		// Assert
		options.MaxConcurrency.ShouldBeGreaterThan(50);
		options.BufferSize.ShouldBeGreaterThan(1000);
	}

	[Fact]
	public void Options_ForSequentialProcessing_HasParallelDisabled()
	{
		// Act
		var options = new PipelineOptions
		{
			MaxConcurrency = 1,
			EnableParallelProcessing = false,
			StopOnFirstError = true,
		};

		// Assert
		options.MaxConcurrency.ShouldBe(1);
		options.EnableParallelProcessing.ShouldBeFalse();
		options.StopOnFirstError.ShouldBeTrue();
	}

	[Fact]
	public void Options_ForEventOnlyProcessing_HasEventMessageKind()
	{
		// Act
		var options = new PipelineOptions
		{
			ApplicableMessageKinds = MessageKinds.Event,
		};

		// Assert
		options.ApplicableMessageKinds.ShouldBe(MessageKinds.Event);
		(options.ApplicableMessageKinds & MessageKinds.Action).ShouldBe(MessageKinds.None);
	}

	#endregion
}
