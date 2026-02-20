// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Options.Middleware;

namespace Excalibur.Dispatch.Tests.Options.Middleware;

/// <summary>
/// Unit tests for <see cref="UnifiedBatchingOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Options")]
[Trait("Priority", "0")]
public sealed class UnifiedBatchingOptionsShould
{
	// Test message type
	private sealed record TestMessage : IDispatchMessage
	{
		public string MessageId { get; init; } = Guid.NewGuid().ToString();
		public string? CorrelationId { get; init; }
		public string? CausationId { get; init; }
		public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
	}

	#region Default Value Tests

	[Fact]
	public void Default_MaxBatchSize_IsThirtyTwo()
	{
		// Arrange & Act
		var options = new UnifiedBatchingOptions();

		// Assert
		options.MaxBatchSize.ShouldBe(32);
	}

	[Fact]
	public void Default_MaxBatchDelay_IsTwoHundredFiftyMilliseconds()
	{
		// Arrange & Act
		var options = new UnifiedBatchingOptions();

		// Assert
		options.MaxBatchDelay.ShouldBe(TimeSpan.FromMilliseconds(250));
	}

	[Fact]
	public void Default_MaxParallelism_IsProcessorCount()
	{
		// Arrange & Act
		var options = new UnifiedBatchingOptions();

		// Assert
		options.MaxParallelism.ShouldBe(Environment.ProcessorCount);
	}

	[Fact]
	public void Default_ProcessAsOptimizedBulk_IsTrue()
	{
		// Arrange & Act
		var options = new UnifiedBatchingOptions();

		// Assert
		options.ProcessAsOptimizedBulk.ShouldBeTrue();
	}

	[Fact]
	public void Default_NonBatchableMessageTypes_IsNotNull()
	{
		// Arrange & Act
		var options = new UnifiedBatchingOptions();

		// Assert
		_ = options.NonBatchableMessageTypes.ShouldNotBeNull();
	}

	[Fact]
	public void Default_NonBatchableMessageTypes_IsEmpty()
	{
		// Arrange & Act
		var options = new UnifiedBatchingOptions();

		// Assert
		options.NonBatchableMessageTypes.ShouldBeEmpty();
	}

	[Fact]
	public void Default_BatchFilter_IsNull()
	{
		// Arrange & Act
		var options = new UnifiedBatchingOptions();

		// Assert
		options.BatchFilter.ShouldBeNull();
	}

	[Fact]
	public void Default_BatchKeySelector_IsNull()
	{
		// Arrange & Act
		var options = new UnifiedBatchingOptions();

		// Assert
		options.BatchKeySelector.ShouldBeNull();
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void MaxBatchSize_CanBeSet()
	{
		// Arrange
		var options = new UnifiedBatchingOptions();

		// Act
		options.MaxBatchSize = 64;

		// Assert
		options.MaxBatchSize.ShouldBe(64);
	}

	[Fact]
	public void MaxBatchDelay_CanBeSet()
	{
		// Arrange
		var options = new UnifiedBatchingOptions();

		// Act
		options.MaxBatchDelay = TimeSpan.FromMilliseconds(500);

		// Assert
		options.MaxBatchDelay.ShouldBe(TimeSpan.FromMilliseconds(500));
	}

	[Fact]
	public void MaxParallelism_CanBeSet()
	{
		// Arrange
		var options = new UnifiedBatchingOptions();

		// Act
		options.MaxParallelism = 16;

		// Assert
		options.MaxParallelism.ShouldBe(16);
	}

	[Fact]
	public void ProcessAsOptimizedBulk_CanBeSet()
	{
		// Arrange
		var options = new UnifiedBatchingOptions();

		// Act
		options.ProcessAsOptimizedBulk = false;

		// Assert
		options.ProcessAsOptimizedBulk.ShouldBeFalse();
	}

	[Fact]
	public void NonBatchableMessageTypes_CanAddTypes()
	{
		// Arrange
		var options = new UnifiedBatchingOptions();

		// Act
		_ = options.NonBatchableMessageTypes.Add(typeof(TestMessage));

		// Assert
		options.NonBatchableMessageTypes.Count.ShouldBe(1);
		options.NonBatchableMessageTypes.ShouldContain(typeof(TestMessage));
	}

	[Fact]
	public void BatchFilter_CanBeSet()
	{
		// Arrange
		var options = new UnifiedBatchingOptions();
		Func<IDispatchMessage, bool> filter = msg => true;

		// Act
		options.BatchFilter = filter;

		// Assert
		options.BatchFilter.ShouldBeSameAs(filter);
	}

	[Fact]
	public void BatchKeySelector_CanBeSet()
	{
		// Arrange
		var options = new UnifiedBatchingOptions();
		Func<IDispatchMessage, string> selector = msg => "custom-batch-key";

		// Act
		options.BatchKeySelector = selector;

		// Assert
		options.BatchKeySelector.ShouldBeSameAs(selector);
	}

	#endregion

	#region Object Initializer Tests

	[Fact]
	public void ObjectInitializer_SetsScalarProperties()
	{
		// Act
		var options = new UnifiedBatchingOptions
		{
			MaxBatchSize = 128,
			MaxBatchDelay = TimeSpan.FromSeconds(1),
			MaxParallelism = 8,
			ProcessAsOptimizedBulk = false,
		};

		// Assert
		options.MaxBatchSize.ShouldBe(128);
		options.MaxBatchDelay.ShouldBe(TimeSpan.FromSeconds(1));
		options.MaxParallelism.ShouldBe(8);
		options.ProcessAsOptimizedBulk.ShouldBeFalse();
	}

	#endregion

	#region Real-World Scenario Tests

	[Fact]
	public void Options_ForHighThroughput_HasLargeBatchSize()
	{
		// Act
		var options = new UnifiedBatchingOptions
		{
			MaxBatchSize = 256,
			MaxParallelism = Environment.ProcessorCount * 2,
		};

		// Assert
		options.MaxBatchSize.ShouldBeGreaterThan(32);
		options.MaxParallelism.ShouldBeGreaterThan(Environment.ProcessorCount);
	}

	[Fact]
	public void Options_ForLowLatency_HasShortDelay()
	{
		// Act
		var options = new UnifiedBatchingOptions
		{
			MaxBatchDelay = TimeSpan.FromMilliseconds(50),
			MaxBatchSize = 10,
		};

		// Assert
		options.MaxBatchDelay.ShouldBeLessThan(TimeSpan.FromMilliseconds(250));
		options.MaxBatchSize.ShouldBeLessThan(32);
	}

	[Fact]
	public void Options_ForCustomGrouping_HasBatchKeySelector()
	{
		// Arrange
		var options = new UnifiedBatchingOptions
		{
			BatchKeySelector = msg => msg.GetType().Name,
		};

		// Act
		var key = options.BatchKeySelector(new TestMessage());

		// Assert
		_ = options.BatchKeySelector.ShouldNotBeNull();
		key.ShouldBe("TestMessage");
	}

	#endregion
}
