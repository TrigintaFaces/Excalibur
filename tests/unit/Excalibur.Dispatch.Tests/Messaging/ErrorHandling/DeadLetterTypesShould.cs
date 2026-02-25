// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.ErrorHandling;

namespace Excalibur.Dispatch.Tests.Messaging.ErrorHandling;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class DeadLetterTypesShould
{
	[Fact]
	public void DeadLetterEntry_SetAllProperties()
	{
		// Arrange
		var now = DateTimeOffset.UtcNow;
		var metadata = new Dictionary<string, string> { ["key"] = "value" };

		// Act
		var entry = new DeadLetterEntry
		{
			Id = Guid.NewGuid(),
			MessageType = "OrderCreated",
			Payload = [1, 2, 3],
			Reason = DeadLetterReason.MaxRetriesExceeded,
			ExceptionMessage = "timeout",
			ExceptionStackTrace = "at Foo.Bar()",
			EnqueuedAt = now,
			OriginalAttempts = 5,
			Metadata = metadata,
			CorrelationId = "corr-001",
			CausationId = "cause-001",
			SourceQueue = "orders-queue",
			IsReplayed = false,
			ReplayedAt = null,
		};

		// Assert
		entry.Id.ShouldNotBe(Guid.Empty);
		entry.MessageType.ShouldBe("OrderCreated");
		entry.Payload.ShouldBe(new byte[] { 1, 2, 3 });
		entry.Reason.ShouldBe(DeadLetterReason.MaxRetriesExceeded);
		entry.ExceptionMessage.ShouldBe("timeout");
		entry.ExceptionStackTrace.ShouldBe("at Foo.Bar()");
		entry.EnqueuedAt.ShouldBe(now);
		entry.OriginalAttempts.ShouldBe(5);
		entry.Metadata.ShouldNotBeNull();
		entry.Metadata!["key"].ShouldBe("value");
		entry.CorrelationId.ShouldBe("corr-001");
		entry.CausationId.ShouldBe("cause-001");
		entry.SourceQueue.ShouldBe("orders-queue");
		entry.IsReplayed.ShouldBeFalse();
		entry.ReplayedAt.ShouldBeNull();
	}

	[Fact]
	public void DeadLetterEntry_ReplayedEntry_SetReplayTimestamp()
	{
		// Arrange
		var replayedAt = DateTimeOffset.UtcNow;

		// Act
		var entry = new DeadLetterEntry
		{
			MessageType = "Test",
			Payload = [],
			IsReplayed = true,
			ReplayedAt = replayedAt,
		};

		// Assert
		entry.IsReplayed.ShouldBeTrue();
		entry.ReplayedAt.ShouldBe(replayedAt);
	}

	[Fact]
	public void DeadLetterQueryFilter_DefaultValues()
	{
		// Act
		var filter = new DeadLetterQueryFilter();

		// Assert
		filter.MessageType.ShouldBeNull();
		filter.Reason.ShouldBeNull();
		filter.FromDate.ShouldBeNull();
		filter.ToDate.ShouldBeNull();
		filter.IsReplayed.ShouldBeNull();
		filter.SourceQueue.ShouldBeNull();
		filter.CorrelationId.ShouldBeNull();
		filter.MinAttempts.ShouldBeNull();
		filter.Skip.ShouldBe(0);
	}

	[Fact]
	public void DeadLetterQueryFilter_ByReason_SetReason()
	{
		// Act
		var filter = DeadLetterQueryFilter.ByReason(DeadLetterReason.CircuitBreakerOpen);

		// Assert
		filter.Reason.ShouldBe(DeadLetterReason.CircuitBreakerOpen);
		filter.MessageType.ShouldBeNull();
	}

	[Fact]
	public void DeadLetterQueryFilter_ByMessageType_SetMessageType()
	{
		// Act
		var filter = DeadLetterQueryFilter.ByMessageType("OrderCreated");

		// Assert
		filter.MessageType.ShouldBe("OrderCreated");
		filter.Reason.ShouldBeNull();
	}

	[Fact]
	public void DeadLetterQueryFilter_ByDateRange_SetDates()
	{
		// Arrange
		var from = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
		var to = new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero);

		// Act
		var filter = DeadLetterQueryFilter.ByDateRange(from, to);

		// Assert
		filter.FromDate.ShouldBe(from);
		filter.ToDate.ShouldBe(to);
	}

	[Fact]
	public void DeadLetterQueryFilter_PendingOnly_SetNotReplayed()
	{
		// Act
		var filter = DeadLetterQueryFilter.PendingOnly();

		// Assert
		filter.IsReplayed.ShouldBe(false);
	}

	[Fact]
	public void DeadLetterQueryFilter_AllPropertiesSettable()
	{
		// Act
		var filter = new DeadLetterQueryFilter
		{
			MessageType = "Test",
			Reason = DeadLetterReason.HandlerNotFound,
			FromDate = DateTimeOffset.MinValue,
			ToDate = DateTimeOffset.MaxValue,
			IsReplayed = true,
			SourceQueue = "test-queue",
			CorrelationId = "corr-123",
			MinAttempts = 3,
			Skip = 10,
		};

		// Assert
		filter.MessageType.ShouldBe("Test");
		filter.Reason.ShouldBe(DeadLetterReason.HandlerNotFound);
		filter.IsReplayed.ShouldBe(true);
		filter.SourceQueue.ShouldBe("test-queue");
		filter.CorrelationId.ShouldBe("corr-123");
		filter.MinAttempts.ShouldBe(3);
		filter.Skip.ShouldBe(10);
	}

	[Fact]
	public void DeadLetterReason_HaveExpectedValues()
	{
		// Assert — verify all 11 enum values exist
		DeadLetterReason.MaxRetriesExceeded.ShouldBe((DeadLetterReason)0);
		DeadLetterReason.CircuitBreakerOpen.ShouldBe((DeadLetterReason)1);
		DeadLetterReason.DeserializationFailed.ShouldBe((DeadLetterReason)2);
		DeadLetterReason.HandlerNotFound.ShouldBe((DeadLetterReason)3);
		DeadLetterReason.ValidationFailed.ShouldBe((DeadLetterReason)4);
		DeadLetterReason.ManualRejection.ShouldBe((DeadLetterReason)5);
		DeadLetterReason.MessageExpired.ShouldBe((DeadLetterReason)6);
		DeadLetterReason.AuthorizationFailed.ShouldBe((DeadLetterReason)7);
		DeadLetterReason.UnhandledException.ShouldBe((DeadLetterReason)8);
		DeadLetterReason.PoisonMessage.ShouldBe((DeadLetterReason)9);
		DeadLetterReason.Unknown.ShouldBe((DeadLetterReason)99);
	}

	[Fact]
	public void DeadLetterReason_DefaultToMaxRetriesExceeded()
	{
		// Arrange
		DeadLetterReason reason = default;

		// Assert
		reason.ShouldBe(DeadLetterReason.MaxRetriesExceeded);
	}
}
