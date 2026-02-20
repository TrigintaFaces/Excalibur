// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.ErrorHandling;

namespace Excalibur.Dispatch.Tests.Messaging.ErrorHandling;

/// <summary>
/// Unit tests for <see cref="DeadLetterEntry"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class DeadLetterEntryShould
{
	[Fact]
	public void StoreIdProperty()
	{
		// Arrange
		var id = Guid.NewGuid();

		// Act
		var entry = CreateTestEntry(id: id);

		// Assert
		entry.Id.ShouldBe(id);
	}

	[Fact]
	public void StoreMessageTypeProperty()
	{
		// Arrange & Act
		var entry = CreateTestEntry(messageType: "OrderCreated");

		// Assert
		entry.MessageType.ShouldBe("OrderCreated");
	}

	[Fact]
	public void StorePayloadProperty()
	{
		// Arrange
		var payload = new byte[] { 1, 2, 3, 4, 5 };

		// Act
		var entry = CreateTestEntry(payload: payload);

		// Assert
		entry.Payload.ShouldBe(payload);
	}

	[Fact]
	public void StoreReasonProperty()
	{
		// Arrange & Act
		var entry = CreateTestEntry(reason: DeadLetterReason.MaxRetriesExceeded);

		// Assert
		entry.Reason.ShouldBe(DeadLetterReason.MaxRetriesExceeded);
	}

	[Fact]
	public void StoreExceptionMessageProperty()
	{
		// Arrange & Act
		var entry = CreateTestEntry(exceptionMessage: "NullReferenceException occurred");

		// Assert
		entry.ExceptionMessage.ShouldBe("NullReferenceException occurred");
	}

	[Fact]
	public void StoreExceptionStackTraceProperty()
	{
		// Arrange
		var stackTrace = "at MyClass.MyMethod()\n   at Program.Main()";

		// Act
		var entry = CreateTestEntry(exceptionStackTrace: stackTrace);

		// Assert
		entry.ExceptionStackTrace.ShouldBe(stackTrace);
	}

	[Fact]
	public void StoreEnqueuedAtProperty()
	{
		// Arrange
		var enqueuedAt = DateTimeOffset.UtcNow;

		// Act
		var entry = CreateTestEntry(enqueuedAt: enqueuedAt);

		// Assert
		entry.EnqueuedAt.ShouldBe(enqueuedAt);
	}

	[Fact]
	public void StoreOriginalAttemptsProperty()
	{
		// Arrange & Act
		var entry = CreateTestEntry(originalAttempts: 5);

		// Assert
		entry.OriginalAttempts.ShouldBe(5);
	}

	[Fact]
	public void StoreMetadataProperty()
	{
		// Arrange
		var metadata = new Dictionary<string, string>
		{
			["key1"] = "value1",
			["key2"] = "value2",
		};

		// Act
		var entry = CreateTestEntry(metadata: metadata);

		// Assert
		entry.Metadata.ShouldNotBeNull();
		entry.Metadata["key1"].ShouldBe("value1");
		entry.Metadata["key2"].ShouldBe("value2");
	}

	[Fact]
	public void StoreCorrelationIdProperty()
	{
		// Arrange & Act
		var entry = CreateTestEntry(correlationId: "correlation-123");

		// Assert
		entry.CorrelationId.ShouldBe("correlation-123");
	}

	[Fact]
	public void StoreCausationIdProperty()
	{
		// Arrange & Act
		var entry = CreateTestEntry(causationId: "causation-456");

		// Assert
		entry.CausationId.ShouldBe("causation-456");
	}

	[Fact]
	public void StoreSourceQueueProperty()
	{
		// Arrange & Act
		var entry = CreateTestEntry(sourceQueue: "orders-queue");

		// Assert
		entry.SourceQueue.ShouldBe("orders-queue");
	}

	[Fact]
	public void StoreIsReplayedProperty()
	{
		// Arrange & Act
		var entry = CreateTestEntry(isReplayed: true);

		// Assert
		entry.IsReplayed.ShouldBeTrue();
	}

	[Fact]
	public void StoreReplayedAtProperty()
	{
		// Arrange
		var replayedAt = DateTimeOffset.UtcNow.AddMinutes(-5);

		// Act
		var entry = CreateTestEntry(replayedAt: replayedAt);

		// Assert
		entry.ReplayedAt.ShouldBe(replayedAt);
	}

	[Fact]
	public void HaveDefaultIsReplayedOfFalse()
	{
		// Arrange & Act
		var entry = CreateTestEntry();

		// Assert
		entry.IsReplayed.ShouldBeFalse();
	}

	[Fact]
	public void HaveDefaultReplayedAtOfNull()
	{
		// Arrange & Act
		var entry = CreateTestEntry();

		// Assert
		entry.ReplayedAt.ShouldBeNull();
	}

	[Fact]
	public void AllowNullExceptionMessage()
	{
		// Arrange & Act
		var entry = CreateTestEntry(exceptionMessage: null);

		// Assert
		entry.ExceptionMessage.ShouldBeNull();
	}

	[Fact]
	public void AllowNullExceptionStackTrace()
	{
		// Arrange & Act
		var entry = CreateTestEntry(exceptionStackTrace: null);

		// Assert
		entry.ExceptionStackTrace.ShouldBeNull();
	}

	[Fact]
	public void AllowNullMetadata()
	{
		// Arrange & Act
		var entry = CreateTestEntry(metadata: null);

		// Assert
		entry.Metadata.ShouldBeNull();
	}

	[Fact]
	public void AllowNullCorrelationId()
	{
		// Arrange & Act
		var entry = CreateTestEntry(correlationId: null);

		// Assert
		entry.CorrelationId.ShouldBeNull();
	}

	[Fact]
	public void AllowNullCausationId()
	{
		// Arrange & Act
		var entry = CreateTestEntry(causationId: null);

		// Assert
		entry.CausationId.ShouldBeNull();
	}

	[Fact]
	public void AllowNullSourceQueue()
	{
		// Arrange & Act
		var entry = CreateTestEntry(sourceQueue: null);

		// Assert
		entry.SourceQueue.ShouldBeNull();
	}

	[Fact]
	public void SupportObjectInitializer()
	{
		// Arrange & Act
		var entry = new DeadLetterEntry
		{
			Id = Guid.NewGuid(),
			MessageType = "TestMessage",
			Payload = [1, 2, 3],
			Reason = DeadLetterReason.HandlerNotFound,
			ExceptionMessage = "Test exception",
			ExceptionStackTrace = "Stack trace here",
			EnqueuedAt = DateTimeOffset.UtcNow,
			OriginalAttempts = 3,
			Metadata = new Dictionary<string, string> { ["key"] = "value" },
			CorrelationId = "corr-1",
			CausationId = "caus-1",
			SourceQueue = "source-queue",
			IsReplayed = true,
			ReplayedAt = DateTimeOffset.UtcNow,
		};

		// Assert
		entry.MessageType.ShouldBe("TestMessage");
		entry.Reason.ShouldBe(DeadLetterReason.HandlerNotFound);
		entry.OriginalAttempts.ShouldBe(3);
		entry.IsReplayed.ShouldBeTrue();
	}

	[Fact]
	public void AcceptEmptyPayload()
	{
		// Arrange & Act
		var entry = CreateTestEntry(payload: []);

		// Assert
		entry.Payload.ShouldBeEmpty();
	}

	[Fact]
	public void AcceptLargePayload()
	{
		// Arrange
		var largePayload = new byte[100000];
		Random.Shared.NextBytes(largePayload);

		// Act
		var entry = CreateTestEntry(payload: largePayload);

		// Assert
		entry.Payload.Length.ShouldBe(100000);
	}

	[Theory]
	[InlineData(DeadLetterReason.Unknown)]
	[InlineData(DeadLetterReason.MaxRetriesExceeded)]
	[InlineData(DeadLetterReason.PoisonMessage)]
	[InlineData(DeadLetterReason.HandlerNotFound)]
	[InlineData(DeadLetterReason.ValidationFailed)]
	[InlineData(DeadLetterReason.CircuitBreakerOpen)]
	[InlineData(DeadLetterReason.ManualRejection)]
	[InlineData(DeadLetterReason.DeserializationFailed)]
	[InlineData(DeadLetterReason.MessageExpired)]
	[InlineData(DeadLetterReason.AuthorizationFailed)]
	[InlineData(DeadLetterReason.UnhandledException)]
	public void AcceptAllDeadLetterReasons(DeadLetterReason reason)
	{
		// Arrange & Act
		var entry = CreateTestEntry(reason: reason);

		// Assert
		entry.Reason.ShouldBe(reason);
	}

	[Fact]
	public void SimulateTypicalMaxRetriesEntry()
	{
		// Arrange & Act
		var entry = new DeadLetterEntry
		{
			Id = Guid.NewGuid(),
			MessageType = "OrderCreated",
			Payload = System.Text.Encoding.UTF8.GetBytes("""{"orderId": 123}"""),
			Reason = DeadLetterReason.MaxRetriesExceeded,
			ExceptionMessage = "TimeoutException: The operation has timed out.",
			ExceptionStackTrace = "at OrderHandler.HandleAsync()",
			EnqueuedAt = DateTimeOffset.UtcNow,
			OriginalAttempts = 5,
			CorrelationId = "order-flow-abc",
			SourceQueue = "orders-processing",
			IsReplayed = false,
		};

		// Assert
		entry.Reason.ShouldBe(DeadLetterReason.MaxRetriesExceeded);
		entry.OriginalAttempts.ShouldBe(5);
		entry.IsReplayed.ShouldBeFalse();
		entry.ExceptionMessage.ShouldContain("TimeoutException");
	}

	[Fact]
	public void SimulateReplayedEntry()
	{
		// Arrange
		var originalEnqueue = DateTimeOffset.UtcNow.AddHours(-2);
		var replayTime = DateTimeOffset.UtcNow.AddMinutes(-10);

		// Act
		var entry = new DeadLetterEntry
		{
			Id = Guid.NewGuid(),
			MessageType = "PaymentProcessed",
			Payload = [0x01, 0x02],
			Reason = DeadLetterReason.MessageExpired,
			EnqueuedAt = originalEnqueue,
			OriginalAttempts = 3,
			IsReplayed = true,
			ReplayedAt = replayTime,
		};

		// Assert
		entry.IsReplayed.ShouldBeTrue();
		entry.ReplayedAt.ShouldBe(replayTime);
		entry.EnqueuedAt.ShouldBe(originalEnqueue);
	}

	private static DeadLetterEntry CreateTestEntry(
		Guid? id = null,
		string messageType = "TestMessage",
		byte[]? payload = null,
		DeadLetterReason reason = DeadLetterReason.Unknown,
		string? exceptionMessage = null,
		string? exceptionStackTrace = null,
		DateTimeOffset? enqueuedAt = null,
		int originalAttempts = 0,
		IDictionary<string, string>? metadata = null,
		string? correlationId = null,
		string? causationId = null,
		string? sourceQueue = null,
		bool isReplayed = false,
		DateTimeOffset? replayedAt = null)
	{
		return new DeadLetterEntry
		{
			Id = id ?? Guid.NewGuid(),
			MessageType = messageType,
			Payload = payload ?? [1, 2, 3],
			Reason = reason,
			ExceptionMessage = exceptionMessage,
			ExceptionStackTrace = exceptionStackTrace,
			EnqueuedAt = enqueuedAt ?? DateTimeOffset.UtcNow,
			OriginalAttempts = originalAttempts,
			Metadata = metadata,
			CorrelationId = correlationId,
			CausationId = causationId,
			SourceQueue = sourceQueue,
			IsReplayed = isReplayed,
			ReplayedAt = replayedAt,
		};
	}
}
