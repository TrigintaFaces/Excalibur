// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.Models;

namespace Excalibur.Saga.Tests.Core.Models;

/// <summary>
/// Unit tests for <see cref="SagaInstanceFilter"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Saga")]
public sealed class SagaInstanceFilterShould
{
	#region Default Values Tests

	[Fact]
	public void HaveNullSagaIdByDefault()
	{
		// Arrange & Act
		var filter = new SagaInstanceFilter();

		// Assert
		filter.SagaId.ShouldBeNull();
	}

	[Fact]
	public void HaveNullCorrelationIdByDefault()
	{
		// Arrange & Act
		var filter = new SagaInstanceFilter();

		// Assert
		filter.CorrelationId.ShouldBeNull();
	}

	[Fact]
	public void HaveNullStatusByDefault()
	{
		// Arrange & Act
		var filter = new SagaInstanceFilter();

		// Assert
		filter.Status.ShouldBeNull();
	}

	[Fact]
	public void HaveNullCreatedAfterByDefault()
	{
		// Arrange & Act
		var filter = new SagaInstanceFilter();

		// Assert
		filter.CreatedAfter.ShouldBeNull();
	}

	[Fact]
	public void HaveNullCreatedBeforeByDefault()
	{
		// Arrange & Act
		var filter = new SagaInstanceFilter();

		// Assert
		filter.CreatedBefore.ShouldBeNull();
	}

	[Fact]
	public void HaveNullMaxResultsByDefault()
	{
		// Arrange & Act
		var filter = new SagaInstanceFilter();

		// Assert
		filter.MaxResults.ShouldBeNull();
	}

	[Fact]
	public void IncludeCompletedByDefault()
	{
		// Arrange & Act
		var filter = new SagaInstanceFilter();

		// Assert
		filter.IncludeCompleted.ShouldBeTrue();
	}

	[Fact]
	public void IncludeFailedByDefault()
	{
		// Arrange & Act
		var filter = new SagaInstanceFilter();

		// Assert
		filter.IncludeFailed.ShouldBeTrue();
	}

	[Fact]
	public void HaveNullMetadataFiltersByDefault()
	{
		// Arrange & Act
		var filter = new SagaInstanceFilter();

		// Assert
		filter.MetadataFilters.ShouldBeNull();
	}

	#endregion Default Values Tests

	#region Property Setting Tests

	[Fact]
	public void AllowSagaIdToBeSet()
	{
		// Arrange & Act
		var filter = new SagaInstanceFilter { SagaId = "saga-123" };

		// Assert
		filter.SagaId.ShouldBe("saga-123");
	}

	[Fact]
	public void AllowCorrelationIdToBeSet()
	{
		// Arrange & Act
		var filter = new SagaInstanceFilter { CorrelationId = "corr-456" };

		// Assert
		filter.CorrelationId.ShouldBe("corr-456");
	}

	[Fact]
	public void AllowStatusToBeSet()
	{
		// Arrange & Act
		var filter = new SagaInstanceFilter { Status = SagaStatus.Running };

		// Assert
		filter.Status.ShouldBe(SagaStatus.Running);
	}

	[Fact]
	public void AllowCreatedAfterToBeSet()
	{
		// Arrange
		var date = DateTime.UtcNow.AddDays(-7);

		// Act
		var filter = new SagaInstanceFilter { CreatedAfter = date };

		// Assert
		filter.CreatedAfter.ShouldBe(date);
	}

	[Fact]
	public void AllowCreatedBeforeToBeSet()
	{
		// Arrange
		var date = DateTime.UtcNow;

		// Act
		var filter = new SagaInstanceFilter { CreatedBefore = date };

		// Assert
		filter.CreatedBefore.ShouldBe(date);
	}

	[Fact]
	public void AllowMaxResultsToBeSet()
	{
		// Arrange & Act
		var filter = new SagaInstanceFilter { MaxResults = 100 };

		// Assert
		filter.MaxResults.ShouldBe(100);
	}

	[Fact]
	public void AllowIncludeCompletedToBeSet()
	{
		// Arrange & Act
		var filter = new SagaInstanceFilter { IncludeCompleted = false };

		// Assert
		filter.IncludeCompleted.ShouldBeFalse();
	}

	[Fact]
	public void AllowIncludeFailedToBeSet()
	{
		// Arrange & Act
		var filter = new SagaInstanceFilter { IncludeFailed = false };

		// Assert
		filter.IncludeFailed.ShouldBeFalse();
	}

	#endregion Property Setting Tests

	#region Comprehensive Filter Tests

	[Fact]
	public void CreateFilterForRunningSagas()
	{
		// Arrange & Act
		var filter = new SagaInstanceFilter
		{
			Status = SagaStatus.Running,
			IncludeCompleted = false,
			IncludeFailed = false,
		};

		// Assert
		filter.Status.ShouldBe(SagaStatus.Running);
		filter.IncludeCompleted.ShouldBeFalse();
		filter.IncludeFailed.ShouldBeFalse();
	}

	[Fact]
	public void CreateFilterForDateRange()
	{
		// Arrange
		var startDate = DateTime.UtcNow.AddDays(-30);
		var endDate = DateTime.UtcNow;

		// Act
		var filter = new SagaInstanceFilter
		{
			CreatedAfter = startDate,
			CreatedBefore = endDate,
		};

		// Assert
		filter.CreatedAfter.ShouldBe(startDate);
		filter.CreatedBefore.ShouldBe(endDate);
	}

	[Fact]
	public void CreateFilterWithPagination()
	{
		// Arrange & Act
		var filter = new SagaInstanceFilter
		{
			MaxResults = 50,
		};

		// Assert
		filter.MaxResults.ShouldBe(50);
	}

	[Fact]
	public void CreateFilterByCorrelationId()
	{
		// Arrange & Act
		var filter = new SagaInstanceFilter
		{
			CorrelationId = "order-12345",
		};

		// Assert
		filter.CorrelationId.ShouldBe("order-12345");
	}

	[Fact]
	public void CreateCompleteFilter()
	{
		// Arrange
		var startDate = DateTime.UtcNow.AddDays(-7);
		var endDate = DateTime.UtcNow;

		// Act
		var filter = new SagaInstanceFilter
		{
			SagaId = "OrderSaga",
			CorrelationId = "order-789",
			Status = SagaStatus.Failed,
			CreatedAfter = startDate,
			CreatedBefore = endDate,
			MaxResults = 25,
			IncludeCompleted = true,
			IncludeFailed = true,
		};

		// Assert
		filter.SagaId.ShouldBe("OrderSaga");
		filter.CorrelationId.ShouldBe("order-789");
		filter.Status.ShouldBe(SagaStatus.Failed);
		filter.CreatedAfter.ShouldBe(startDate);
		filter.CreatedBefore.ShouldBe(endDate);
		filter.MaxResults.ShouldBe(25);
		filter.IncludeCompleted.ShouldBeTrue();
		filter.IncludeFailed.ShouldBeTrue();
	}

	#endregion Comprehensive Filter Tests
}
