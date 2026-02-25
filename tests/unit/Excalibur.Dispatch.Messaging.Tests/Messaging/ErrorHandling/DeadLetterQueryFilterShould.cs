// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.ErrorHandling;

namespace Excalibur.Dispatch.Tests.Messaging.ErrorHandling;

/// <summary>
/// Unit tests for <see cref="DeadLetterQueryFilter"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class DeadLetterQueryFilterShould
{
	[Fact]
	public void HaveDefaultMessageTypeOfNull()
	{
		// Arrange & Act
		var filter = new DeadLetterQueryFilter();

		// Assert
		filter.MessageType.ShouldBeNull();
	}

	[Fact]
	public void HaveDefaultReasonOfNull()
	{
		// Arrange & Act
		var filter = new DeadLetterQueryFilter();

		// Assert
		filter.Reason.ShouldBeNull();
	}

	[Fact]
	public void HaveDefaultFromDateOfNull()
	{
		// Arrange & Act
		var filter = new DeadLetterQueryFilter();

		// Assert
		filter.FromDate.ShouldBeNull();
	}

	[Fact]
	public void HaveDefaultToDateOfNull()
	{
		// Arrange & Act
		var filter = new DeadLetterQueryFilter();

		// Assert
		filter.ToDate.ShouldBeNull();
	}

	[Fact]
	public void HaveDefaultIsReplayedOfNull()
	{
		// Arrange & Act
		var filter = new DeadLetterQueryFilter();

		// Assert
		filter.IsReplayed.ShouldBeNull();
	}

	[Fact]
	public void HaveDefaultSourceQueueOfNull()
	{
		// Arrange & Act
		var filter = new DeadLetterQueryFilter();

		// Assert
		filter.SourceQueue.ShouldBeNull();
	}

	[Fact]
	public void HaveDefaultCorrelationIdOfNull()
	{
		// Arrange & Act
		var filter = new DeadLetterQueryFilter();

		// Assert
		filter.CorrelationId.ShouldBeNull();
	}

	[Fact]
	public void HaveDefaultMinAttemptsOfNull()
	{
		// Arrange & Act
		var filter = new DeadLetterQueryFilter();

		// Assert
		filter.MinAttempts.ShouldBeNull();
	}

	[Fact]
	public void HaveDefaultSkipOfZero()
	{
		// Arrange & Act
		var filter = new DeadLetterQueryFilter();

		// Assert
		filter.Skip.ShouldBe(0);
	}

	[Fact]
	public void AllowSettingMessageType()
	{
		// Arrange
		var filter = new DeadLetterQueryFilter();

		// Act
		filter.MessageType = "OrderCreated";

		// Assert
		filter.MessageType.ShouldBe("OrderCreated");
	}

	[Theory]
	[InlineData(DeadLetterReason.MaxRetriesExceeded)]
	[InlineData(DeadLetterReason.ValidationFailed)]
	[InlineData(DeadLetterReason.HandlerNotFound)]
	[InlineData(DeadLetterReason.Unknown)]
	public void AllowSettingReason(DeadLetterReason reason)
	{
		// Arrange
		var filter = new DeadLetterQueryFilter();

		// Act
		filter.Reason = reason;

		// Assert
		filter.Reason.ShouldBe(reason);
	}

	[Fact]
	public void AllowSettingFromDate()
	{
		// Arrange
		var filter = new DeadLetterQueryFilter();
		var fromDate = DateTimeOffset.UtcNow.AddDays(-7);

		// Act
		filter.FromDate = fromDate;

		// Assert
		filter.FromDate.ShouldBe(fromDate);
	}

	[Fact]
	public void AllowSettingToDate()
	{
		// Arrange
		var filter = new DeadLetterQueryFilter();
		var toDate = DateTimeOffset.UtcNow;

		// Act
		filter.ToDate = toDate;

		// Assert
		filter.ToDate.ShouldBe(toDate);
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public void AllowSettingIsReplayed(bool isReplayed)
	{
		// Arrange
		var filter = new DeadLetterQueryFilter();

		// Act
		filter.IsReplayed = isReplayed;

		// Assert
		filter.IsReplayed.ShouldBe(isReplayed);
	}

	[Fact]
	public void AllowSettingSourceQueue()
	{
		// Arrange
		var filter = new DeadLetterQueryFilter();

		// Act
		filter.SourceQueue = "orders-queue";

		// Assert
		filter.SourceQueue.ShouldBe("orders-queue");
	}

	[Fact]
	public void AllowSettingCorrelationId()
	{
		// Arrange
		var filter = new DeadLetterQueryFilter();

		// Act
		filter.CorrelationId = "correlation-123";

		// Assert
		filter.CorrelationId.ShouldBe("correlation-123");
	}

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(3)]
	[InlineData(5)]
	[InlineData(10)]
	public void AllowSettingMinAttempts(int minAttempts)
	{
		// Arrange
		var filter = new DeadLetterQueryFilter();

		// Act
		filter.MinAttempts = minAttempts;

		// Assert
		filter.MinAttempts.ShouldBe(minAttempts);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(10)]
	[InlineData(100)]
	[InlineData(1000)]
	public void AllowSettingSkip(int skip)
	{
		// Arrange
		var filter = new DeadLetterQueryFilter();

		// Act
		filter.Skip = skip;

		// Assert
		filter.Skip.ShouldBe(skip);
	}

	[Fact]
	public void SupportObjectInitializer()
	{
		// Arrange
		var fromDate = DateTimeOffset.UtcNow.AddDays(-7);
		var toDate = DateTimeOffset.UtcNow;

		// Act
		var filter = new DeadLetterQueryFilter
		{
			MessageType = "PaymentFailed",
			Reason = DeadLetterReason.MaxRetriesExceeded,
			FromDate = fromDate,
			ToDate = toDate,
			IsReplayed = false,
			SourceQueue = "payments-queue",
			CorrelationId = "corr-456",
			MinAttempts = 3,
			Skip = 20,
		};

		// Assert
		filter.MessageType.ShouldBe("PaymentFailed");
		filter.Reason.ShouldBe(DeadLetterReason.MaxRetriesExceeded);
		filter.FromDate.ShouldBe(fromDate);
		filter.ToDate.ShouldBe(toDate);
		filter.IsReplayed.ShouldBe(false);
		filter.SourceQueue.ShouldBe("payments-queue");
		filter.CorrelationId.ShouldBe("corr-456");
		filter.MinAttempts.ShouldBe(3);
		filter.Skip.ShouldBe(20);
	}

	// Factory method tests

	[Theory]
	[InlineData(DeadLetterReason.MaxRetriesExceeded)]
	[InlineData(DeadLetterReason.ValidationFailed)]
	[InlineData(DeadLetterReason.HandlerNotFound)]
	[InlineData(DeadLetterReason.CircuitBreakerOpen)]
	[InlineData(DeadLetterReason.PoisonMessage)]
	public void CreateFilterByReason(DeadLetterReason reason)
	{
		// Act
		var filter = DeadLetterQueryFilter.ByReason(reason);

		// Assert
		filter.Reason.ShouldBe(reason);
		filter.MessageType.ShouldBeNull();
		filter.FromDate.ShouldBeNull();
		filter.ToDate.ShouldBeNull();
		filter.IsReplayed.ShouldBeNull();
	}

	[Theory]
	[InlineData("OrderCreated")]
	[InlineData("PaymentProcessed")]
	[InlineData("UserRegistered")]
	[InlineData("")]
	public void CreateFilterByMessageType(string messageType)
	{
		// Act
		var filter = DeadLetterQueryFilter.ByMessageType(messageType);

		// Assert
		filter.MessageType.ShouldBe(messageType);
		filter.Reason.ShouldBeNull();
		filter.FromDate.ShouldBeNull();
		filter.ToDate.ShouldBeNull();
		filter.IsReplayed.ShouldBeNull();
	}

	[Fact]
	public void CreateFilterByDateRange()
	{
		// Arrange
		var from = DateTimeOffset.UtcNow.AddDays(-30);
		var to = DateTimeOffset.UtcNow;

		// Act
		var filter = DeadLetterQueryFilter.ByDateRange(from, to);

		// Assert
		filter.FromDate.ShouldBe(from);
		filter.ToDate.ShouldBe(to);
		filter.MessageType.ShouldBeNull();
		filter.Reason.ShouldBeNull();
		filter.IsReplayed.ShouldBeNull();
	}

	[Fact]
	public void CreateFilterByDateRangeWithSameFromAndTo()
	{
		// Arrange
		var date = DateTimeOffset.UtcNow;

		// Act
		var filter = DeadLetterQueryFilter.ByDateRange(date, date);

		// Assert
		filter.FromDate.ShouldBe(date);
		filter.ToDate.ShouldBe(date);
	}

	[Fact]
	public void CreatePendingOnlyFilter()
	{
		// Act
		var filter = DeadLetterQueryFilter.PendingOnly();

		// Assert
		filter.IsReplayed.ShouldBe(false);
		filter.MessageType.ShouldBeNull();
		filter.Reason.ShouldBeNull();
		filter.FromDate.ShouldBeNull();
		filter.ToDate.ShouldBeNull();
	}

	[Fact]
	public void SimulateTypicalQueryForFailedMessages()
	{
		// Arrange & Act - Query for messages that failed due to max retries in the last week
		var filter = new DeadLetterQueryFilter
		{
			Reason = DeadLetterReason.MaxRetriesExceeded,
			FromDate = DateTimeOffset.UtcNow.AddDays(-7),
			IsReplayed = false,
			MinAttempts = 3,
		};

		// Assert
		filter.Reason.ShouldBe(DeadLetterReason.MaxRetriesExceeded);
		filter.IsReplayed.ShouldBe(false);
		filter.MinAttempts.ShouldBe(3);
	}

	[Fact]
	public void SimulateTypicalPaginatedQuery()
	{
		// Arrange & Act - Query with pagination
		var filter = new DeadLetterQueryFilter
		{
			MessageType = "OrderCreated",
			Skip = 50,
		};

		// Assert
		filter.MessageType.ShouldBe("OrderCreated");
		filter.Skip.ShouldBe(50);
	}

	[Fact]
	public void SimulateTypicalCorrelationQuery()
	{
		// Arrange & Act - Query all failures for a specific correlation ID
		var filter = new DeadLetterQueryFilter
		{
			CorrelationId = "order-flow-12345",
		};

		// Assert
		filter.CorrelationId.ShouldBe("order-flow-12345");
	}

	[Fact]
	public void AllowCombiningFactoryMethodWithAdditionalFilters()
	{
		// Arrange
		var filter = DeadLetterQueryFilter.ByReason(DeadLetterReason.ValidationFailed);

		// Act - Further narrow down the filter
		filter.MessageType = "UserRegistration";
		filter.MinAttempts = 1;

		// Assert
		filter.Reason.ShouldBe(DeadLetterReason.ValidationFailed);
		filter.MessageType.ShouldBe("UserRegistration");
		filter.MinAttempts.ShouldBe(1);
	}
}
