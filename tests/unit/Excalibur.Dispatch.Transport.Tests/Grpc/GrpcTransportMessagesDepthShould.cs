// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Grpc;

namespace Excalibur.Dispatch.Transport.Tests.Grpc;

/// <summary>
/// Depth coverage tests for gRPC transport message types covering
/// internal/sealed type checks, error response fields, batch scenarios,
/// and acknowledge action patterns.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class GrpcTransportMessagesDepthShould
{
	[Fact]
	public void GrpcTransportRequest_BeInternalAndSealed()
	{
		typeof(GrpcTransportRequest).IsNotPublic.ShouldBeTrue();
		typeof(GrpcTransportRequest).IsSealed.ShouldBeTrue();
	}

	[Fact]
	public void GrpcTransportResponse_BeInternalAndSealed()
	{
		typeof(GrpcTransportResponse).IsNotPublic.ShouldBeTrue();
		typeof(GrpcTransportResponse).IsSealed.ShouldBeTrue();
	}

	[Fact]
	public void GrpcBatchRequest_BeInternalAndSealed()
	{
		typeof(GrpcBatchRequest).IsNotPublic.ShouldBeTrue();
		typeof(GrpcBatchRequest).IsSealed.ShouldBeTrue();
	}

	[Fact]
	public void GrpcBatchResponse_BeInternalAndSealed()
	{
		typeof(GrpcBatchResponse).IsNotPublic.ShouldBeTrue();
		typeof(GrpcBatchResponse).IsSealed.ShouldBeTrue();
	}

	[Fact]
	public void GrpcSubscribeRequest_BeInternalAndSealed()
	{
		typeof(GrpcSubscribeRequest).IsNotPublic.ShouldBeTrue();
		typeof(GrpcSubscribeRequest).IsSealed.ShouldBeTrue();
	}

	[Fact]
	public void GrpcReceivedMessage_BeInternalAndSealed()
	{
		typeof(GrpcReceivedMessage).IsNotPublic.ShouldBeTrue();
		typeof(GrpcReceivedMessage).IsSealed.ShouldBeTrue();
	}

	[Fact]
	public void GrpcReceiveRequest_BeInternalAndSealed()
	{
		typeof(GrpcReceiveRequest).IsNotPublic.ShouldBeTrue();
		typeof(GrpcReceiveRequest).IsSealed.ShouldBeTrue();
	}

	[Fact]
	public void GrpcReceiveResponse_BeInternalAndSealed()
	{
		typeof(GrpcReceiveResponse).IsNotPublic.ShouldBeTrue();
		typeof(GrpcReceiveResponse).IsSealed.ShouldBeTrue();
	}

	[Fact]
	public void GrpcAcknowledgeRequest_BeInternalAndSealed()
	{
		typeof(GrpcAcknowledgeRequest).IsNotPublic.ShouldBeTrue();
		typeof(GrpcAcknowledgeRequest).IsSealed.ShouldBeTrue();
	}

	[Fact]
	public void GrpcAcknowledgeResponse_BeInternalAndSealed()
	{
		typeof(GrpcAcknowledgeResponse).IsNotPublic.ShouldBeTrue();
		typeof(GrpcAcknowledgeResponse).IsSealed.ShouldBeTrue();
	}

	[Fact]
	public void GrpcTransportResponse_SupportsErrorFields()
	{
		// Arrange & Act
		var response = new GrpcTransportResponse
		{
			IsSuccess = false,
			MessageId = null,
			ErrorCode = "RATE_LIMITED",
			ErrorMessage = "Too many requests",
		};

		// Assert
		response.IsSuccess.ShouldBeFalse();
		response.MessageId.ShouldBeNull();
		response.ErrorCode.ShouldBe("RATE_LIMITED");
		response.ErrorMessage.ShouldBe("Too many requests");
	}

	[Fact]
	public void GrpcBatchResponse_SupportsMultipleResults()
	{
		// Arrange & Act
		var response = new GrpcBatchResponse
		{
			Results =
			[
				new GrpcTransportResponse { IsSuccess = true, MessageId = "m1" },
				new GrpcTransportResponse { IsSuccess = false, ErrorCode = "E1" },
				new GrpcTransportResponse { IsSuccess = true, MessageId = "m3" },
				new GrpcTransportResponse { IsSuccess = false, ErrorCode = "E4", ErrorMessage = "Quota exceeded" },
			],
		};

		// Assert
		response.Results.Count.ShouldBe(4);
		response.Results.Count(r => r.IsSuccess).ShouldBe(2);
		response.Results.Count(r => !r.IsSuccess).ShouldBe(2);
	}

	[Fact]
	public void GrpcAcknowledgeRequest_SupportsAllActionTypes()
	{
		// Act & Assert — acknowledge
		var ack = new GrpcAcknowledgeRequest { MessageId = "m1", Action = "acknowledge" };
		ack.Action.ShouldBe("acknowledge");

		// Act & Assert — reject
		var reject = new GrpcAcknowledgeRequest { MessageId = "m2", Action = "reject", Reason = "invalid" };
		reject.Action.ShouldBe("reject");
		reject.Reason.ShouldBe("invalid");

		// Act & Assert — requeue
		var requeue = new GrpcAcknowledgeRequest { MessageId = "m3", Action = "requeue" };
		requeue.Action.ShouldBe("requeue");
	}

	[Fact]
	public void GrpcReceiveResponse_SupportsMultipleMessages()
	{
		// Arrange & Act
		var response = new GrpcReceiveResponse
		{
			Messages =
			[
				new GrpcReceivedMessage { Id = "r1", Body = "AA==", DeliveryCount = 1 },
				new GrpcReceivedMessage { Id = "r2", Body = "Qg==", DeliveryCount = 2 },
				new GrpcReceivedMessage { Id = "r3", Body = "Qw==", DeliveryCount = 5 },
			],
		};

		// Assert
		response.Messages.Count.ShouldBe(3);
		response.Messages[2].DeliveryCount.ShouldBe(5);
	}

	[Fact]
	public void GrpcReceivedMessage_SupportsLargeBase64Body()
	{
		// Arrange
		var largeBody = new string('A', 65536); // ~48KB Base64 payload

		// Act
		var msg = new GrpcReceivedMessage
		{
			Id = "large",
			Body = largeBody,
		};

		// Assert
		msg.Body.Length.ShouldBe(65536);
	}
}
