// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Resilience.Polly;

namespace Excalibur.Dispatch.Middleware.Tests.Resilience;

/// <summary>
/// Unit tests for <see cref="BulkheadMetrics"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Resilience")]
public sealed class BulkheadMetricsShould : UnitTestBase
{
	[Fact]
	public void DefaultValues_AreCorrect()
	{
		// Act
		var metrics = new BulkheadMetrics();

		// Assert
		metrics.Name.ShouldBe(string.Empty);
		metrics.MaxConcurrency.ShouldBe(0);
		metrics.MaxQueueLength.ShouldBe(0);
		metrics.ActiveExecutions.ShouldBe(0);
		metrics.QueueLength.ShouldBe(0);
		metrics.TotalExecutions.ShouldBe(0);
		metrics.RejectedExecutions.ShouldBe(0);
		metrics.QueuedExecutions.ShouldBe(0);
		metrics.AvailableCapacity.ShouldBe(0);
	}

	[Fact]
	public void Name_CanBeSet()
	{
		// Arrange & Act
		var metrics = new BulkheadMetrics { Name = "test-bulkhead" };

		// Assert
		metrics.Name.ShouldBe("test-bulkhead");
	}

	[Fact]
	public void MaxConcurrency_CanBeSet()
	{
		// Arrange & Act
		var metrics = new BulkheadMetrics { MaxConcurrency = 10 };

		// Assert
		metrics.MaxConcurrency.ShouldBe(10);
	}

	[Fact]
	public void MaxQueueLength_CanBeSet()
	{
		// Arrange & Act
		var metrics = new BulkheadMetrics { MaxQueueLength = 50 };

		// Assert
		metrics.MaxQueueLength.ShouldBe(50);
	}

	[Fact]
	public void ActiveExecutions_CanBeSet()
	{
		// Arrange & Act
		var metrics = new BulkheadMetrics { ActiveExecutions = 5 };

		// Assert
		metrics.ActiveExecutions.ShouldBe(5);
	}

	[Fact]
	public void QueueLength_CanBeSet()
	{
		// Arrange & Act
		var metrics = new BulkheadMetrics { QueueLength = 3 };

		// Assert
		metrics.QueueLength.ShouldBe(3);
	}

	[Fact]
	public void TotalExecutions_CanBeSet()
	{
		// Arrange & Act
		var metrics = new BulkheadMetrics { TotalExecutions = 1000 };

		// Assert
		metrics.TotalExecutions.ShouldBe(1000);
	}

	[Fact]
	public void RejectedExecutions_CanBeSet()
	{
		// Arrange & Act
		var metrics = new BulkheadMetrics { RejectedExecutions = 25 };

		// Assert
		metrics.RejectedExecutions.ShouldBe(25);
	}

	[Fact]
	public void QueuedExecutions_CanBeSet()
	{
		// Arrange & Act
		var metrics = new BulkheadMetrics { QueuedExecutions = 100 };

		// Assert
		metrics.QueuedExecutions.ShouldBe(100);
	}

	[Fact]
	public void AvailableCapacity_CanBeSet()
	{
		// Arrange & Act
		var metrics = new BulkheadMetrics { AvailableCapacity = 7 };

		// Assert
		metrics.AvailableCapacity.ShouldBe(7);
	}

	[Fact]
	public void UtilizationPercentage_CalculatedCorrectly_WhenHalfFull()
	{
		// Arrange & Act
		var metrics = new BulkheadMetrics
		{
			MaxConcurrency = 10,
			ActiveExecutions = 5
		};

		// Assert
		metrics.UtilizationPercentage.ShouldBe(50.0);
	}

	[Fact]
	public void UtilizationPercentage_CalculatedCorrectly_WhenFull()
	{
		// Arrange & Act
		var metrics = new BulkheadMetrics
		{
			MaxConcurrency = 10,
			ActiveExecutions = 10
		};

		// Assert
		metrics.UtilizationPercentage.ShouldBe(100.0);
	}

	[Fact]
	public void UtilizationPercentage_CalculatedCorrectly_WhenEmpty()
	{
		// Arrange & Act
		var metrics = new BulkheadMetrics
		{
			MaxConcurrency = 10,
			ActiveExecutions = 0
		};

		// Assert
		metrics.UtilizationPercentage.ShouldBe(0.0);
	}

	[Fact]
	public void UtilizationPercentage_ReturnsZero_WhenMaxConcurrencyIsZero()
	{
		// Arrange & Act
		var metrics = new BulkheadMetrics
		{
			MaxConcurrency = 0,
			ActiveExecutions = 5
		};

		// Assert - Avoid division by zero
		metrics.UtilizationPercentage.ShouldBe(0.0);
	}

	[Fact]
	public void AllProperties_CanBeSetTogether()
	{
		// Arrange & Act
		var metrics = new BulkheadMetrics
		{
			Name = "test-bulkhead",
			MaxConcurrency = 10,
			MaxQueueLength = 50,
			ActiveExecutions = 5,
			QueueLength = 3,
			TotalExecutions = 1000,
			RejectedExecutions = 25,
			QueuedExecutions = 100,
			AvailableCapacity = 5
		};

		// Assert
		metrics.Name.ShouldBe("test-bulkhead");
		metrics.MaxConcurrency.ShouldBe(10);
		metrics.MaxQueueLength.ShouldBe(50);
		metrics.ActiveExecutions.ShouldBe(5);
		metrics.QueueLength.ShouldBe(3);
		metrics.TotalExecutions.ShouldBe(1000);
		metrics.RejectedExecutions.ShouldBe(25);
		metrics.QueuedExecutions.ShouldBe(100);
		metrics.AvailableCapacity.ShouldBe(5);
		metrics.UtilizationPercentage.ShouldBe(50.0);
	}
}
