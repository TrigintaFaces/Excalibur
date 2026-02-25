// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Amazon.SQS.Model;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Transport.Aws;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.Sqs;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class SqsMessageEnvelopeShould : IDisposable
{
	private readonly IMessageContext _context = A.Fake<IMessageContext>();
	private readonly Dictionary<string, object> _items = new(StringComparer.Ordinal);

	public SqsMessageEnvelopeShould()
	{
		A.CallTo(() => _context.Items).Returns(_items);
		A.CallTo(() => _context.DeliveryCount).Returns(0);
	}

	[Fact]
	public void CreateWithNullSqsMessage()
	{
		// Arrange & Act
		var envelope = new SqsMessageEnvelope(null, null, _context);

		// Assert
		envelope.SqsMessage.ShouldNotBeNull();
		envelope.Message.ShouldNotBeNull();
		envelope.Context.ShouldBe(_context);
	}

	[Fact]
	public void ExposeSqsMessageProperties()
	{
		// Arrange
		var sqsMessage = new Message
		{
			MessageId = "msg-001",
			ReceiptHandle = "receipt-handle-123",
			Body = "test-body",
		};

		// Act
		var envelope = new SqsMessageEnvelope(sqsMessage, null, _context);

		// Assert
		envelope.MessageId.ShouldBe("msg-001");
		envelope.ReceiptHandle.ShouldBe("receipt-handle-123");
	}

	[Fact]
	public void SetPollerIdInContext()
	{
		// Arrange & Act
		var envelope = new SqsMessageEnvelope(null, null, _context, pollerId: 42);

		// Assert
		_items["SQS.PollerId"].ShouldBe("42");
		envelope.PollerId.ShouldBe(42);
	}

	[Fact]
	public void AddSqsAttributesToContext()
	{
		// Arrange
		var sqsMessage = new Message
		{
			MessageId = "msg-1",
			ReceiptHandle = "rh-1",
			Attributes = new Dictionary<string, string>
			{
				["ApproximateReceiveCount"] = "3",
			},
		};

		// Act
		var envelope = new SqsMessageEnvelope(sqsMessage, null, _context);

		// Assert
		_items["SQS.ApproximateReceiveCount"].ShouldBe("3");
		_items["SQS.ReceiptHandle"].ShouldBe("rh-1");
		_items["SQS.MessageId"].ShouldBe("msg-1");
	}

	[Fact]
	public void ReturnApproximateReceiveCountFromContext()
	{
		// Arrange
		var sqsMessage = new Message
		{
			Attributes = new Dictionary<string, string>
			{
				["ApproximateReceiveCount"] = "5",
			},
		};
		var envelope = new SqsMessageEnvelope(sqsMessage, null, _context);

		// Act & Assert
		envelope.ApproximateReceiveCount.ShouldBe(5);
	}

	[Fact]
	public void SetApproximateReceiveCount()
	{
		// Arrange
		var envelope = new SqsMessageEnvelope(null, null, _context);

		// Act
		envelope.ApproximateReceiveCount = 7;

		// Assert
		_items["SQS.ApproximateReceiveCount"].ShouldBe("7");
	}

	[Fact]
	public async Task AcknowledgeWhenCallbackProvided()
	{
		// Arrange
		CancellationToken capturedToken = CancellationToken.None;
		using var cts = new CancellationTokenSource();
		var envelope = new SqsMessageEnvelope(
			null, null, _context,
			onAcknowledge: token =>
			{
				capturedToken = token;
				return Task.CompletedTask;
			});

		// Act
		await envelope.AcknowledgeAsync(cts.Token);

		// Assert
		capturedToken.ShouldBe(cts.Token);
		capturedToken.CanBeCanceled.ShouldBeTrue();
	}

	[Fact]
	public async Task RejectWhenCallbackProvided()
	{
		// Arrange
		string? rejectedReason = null;
		CancellationToken capturedToken = CancellationToken.None;
		using var cts = new CancellationTokenSource();
		var envelope = new SqsMessageEnvelope(
			null, null, _context,
			onReject: (reason, token) =>
			{
				rejectedReason = reason;
				capturedToken = token;
				return Task.CompletedTask;
			});

		// Act
		await envelope.RejectAsync(cts.Token, "bad message");

		// Assert
		rejectedReason.ShouldBe("bad message");
		capturedToken.ShouldBe(cts.Token);
	}

	[Fact]
	public void SupportDispose()
	{
		// Arrange
		var envelope = new SqsMessageEnvelope(null, null, _context);

		// Act & Assert - should not throw
		envelope.Dispose();
		envelope.Dispose(); // Double dispose should be safe
	}

	[Fact]
	public async Task SupportAsyncDispose()
	{
		// Arrange
		var envelope = new SqsMessageEnvelope(null, null, _context);

		// Act & Assert - should not throw
		await envelope.DisposeAsync();
		await envelope.DisposeAsync(); // Double dispose should be safe
	}

	[Fact]
	public void ThrowWhenContextIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(
			() => new SqsMessageEnvelope(null, null, null!));
	}

	public void Dispose()
	{
		// No cleanup needed
	}
}
