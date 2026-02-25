// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Azure;

namespace Excalibur.Dispatch.Transport.Tests.AzureServiceBus.StorageQueues;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class DeadLetterMessageEnvelopeShould
{
	[Fact]
	public void CreateWithRequiredProperties()
	{
		// Arrange
		var now = DateTimeOffset.UtcNow;

		// Act
		var envelope = new DeadLetterMessageEnvelope
		{
			OriginalMessageId = "msg-001",
			OriginalMessage = "{\"data\":\"value\"}",
			DeadLetterReason = "MaxDeliveryAttempts exceeded",
			DeadLetterTimestamp = now,
			OriginalDequeueCount = 5,
		};

		// Assert
		envelope.OriginalMessageId.ShouldBe("msg-001");
		envelope.OriginalMessage.ShouldBe("{\"data\":\"value\"}");
		envelope.DeadLetterReason.ShouldBe("MaxDeliveryAttempts exceeded");
		envelope.DeadLetterTimestamp.ShouldBe(now);
		envelope.OriginalDequeueCount.ShouldBe(5);
		envelope.ExceptionDetails.ShouldBeNull();
		envelope.CorrelationId.ShouldBeNull();
		envelope.MessageType.ShouldBeNull();
		envelope.Properties.ShouldBeEmpty();
	}

	[Fact]
	public void CreateWithAllProperties()
	{
		// Arrange & Act
		var envelope = new DeadLetterMessageEnvelope
		{
			OriginalMessageId = "msg-002",
			OriginalMessage = "payload",
			DeadLetterReason = "Processing error",
			DeadLetterTimestamp = DateTimeOffset.UtcNow,
			OriginalDequeueCount = 3,
			ExceptionDetails = "NullReferenceException at ...",
			CorrelationId = "corr-789",
			MessageType = "OrderCreated",
			Properties = new Dictionary<string, string?> { ["source"] = "api" },
		};

		// Assert
		envelope.ExceptionDetails.ShouldBe("NullReferenceException at ...");
		envelope.CorrelationId.ShouldBe("corr-789");
		envelope.MessageType.ShouldBe("OrderCreated");
		envelope.Properties["source"].ShouldBe("api");
	}
}
