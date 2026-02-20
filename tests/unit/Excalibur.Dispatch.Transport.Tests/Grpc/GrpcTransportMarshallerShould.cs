// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Grpc;

namespace Excalibur.Dispatch.Transport.Tests.Grpc;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class GrpcTransportMarshallerShould
{
	[Fact]
	public void RoundTripGrpcTransportRequest()
	{
		// Arrange
		var original = new GrpcTransportRequest
		{
			Id = "msg-1",
			Body = "SGVsbG8=",
			ContentType = "application/json",
			MessageType = "OrderCreated",
			CorrelationId = "corr-1",
			Subject = "orders",
			Destination = "order-queue",
			Properties = new Dictionary<string, string> { ["key"] = "value" },
		};

		// Act
		var bytes = GrpcTransportMarshaller.RequestMarshaller.Serializer(original);
		var deserialized = GrpcTransportMarshaller.RequestMarshaller.Deserializer(bytes);

		// Assert
		deserialized.Id.ShouldBe("msg-1");
		deserialized.Body.ShouldBe("SGVsbG8=");
		deserialized.ContentType.ShouldBe("application/json");
		deserialized.MessageType.ShouldBe("OrderCreated");
		deserialized.CorrelationId.ShouldBe("corr-1");
		deserialized.Subject.ShouldBe("orders");
		deserialized.Destination.ShouldBe("order-queue");
		deserialized.Properties["key"].ShouldBe("value");
	}

	[Fact]
	public void RoundTripGrpcTransportResponse()
	{
		// Arrange
		var original = new GrpcTransportResponse
		{
			IsSuccess = true,
			MessageId = "msg-2",
		};

		// Act
		var bytes = GrpcTransportMarshaller.ResponseMarshaller.Serializer(original);
		var deserialized = GrpcTransportMarshaller.ResponseMarshaller.Deserializer(bytes);

		// Assert
		deserialized.IsSuccess.ShouldBeTrue();
		deserialized.MessageId.ShouldBe("msg-2");
	}

	[Fact]
	public void RoundTripGrpcBatchRequest()
	{
		// Arrange
		var original = new GrpcBatchRequest
		{
			Messages =
			[
				new GrpcTransportRequest { Id = "msg-a" },
				new GrpcTransportRequest { Id = "msg-b" },
			],
		};

		// Act
		var bytes = GrpcTransportMarshaller.BatchRequestMarshaller.Serializer(original);
		var deserialized = GrpcTransportMarshaller.BatchRequestMarshaller.Deserializer(bytes);

		// Assert
		deserialized.Messages.Count.ShouldBe(2);
		deserialized.Messages[0].Id.ShouldBe("msg-a");
		deserialized.Messages[1].Id.ShouldBe("msg-b");
	}

	[Fact]
	public void RoundTripGrpcBatchResponse()
	{
		// Arrange
		var original = new GrpcBatchResponse
		{
			Results =
			[
				new GrpcTransportResponse { IsSuccess = true, MessageId = "r-1" },
				new GrpcTransportResponse { IsSuccess = false, ErrorCode = "ERR", ErrorMessage = "fail" },
			],
		};

		// Act
		var bytes = GrpcTransportMarshaller.BatchResponseMarshaller.Serializer(original);
		var deserialized = GrpcTransportMarshaller.BatchResponseMarshaller.Deserializer(bytes);

		// Assert
		deserialized.Results.Count.ShouldBe(2);
		deserialized.Results[0].IsSuccess.ShouldBeTrue();
		deserialized.Results[1].ErrorCode.ShouldBe("ERR");
		deserialized.Results[1].ErrorMessage.ShouldBe("fail");
	}

	[Fact]
	public void RoundTripGrpcSubscribeRequest()
	{
		// Arrange
		var original = new GrpcSubscribeRequest { Source = "test-subscription" };

		// Act
		var bytes = GrpcTransportMarshaller.SubscribeRequestMarshaller.Serializer(original);
		var deserialized = GrpcTransportMarshaller.SubscribeRequestMarshaller.Deserializer(bytes);

		// Assert
		deserialized.Source.ShouldBe("test-subscription");
	}

	[Fact]
	public void RoundTripGrpcReceivedMessage()
	{
		// Arrange
		var original = new GrpcReceivedMessage
		{
			Id = "recv-1",
			Body = "QUFB",
			ContentType = "text/plain",
			MessageType = "TestEvent",
			CorrelationId = "corr-x",
			Subject = "test",
			DeliveryCount = 2,
			Source = "test-source",
			Properties = new Dictionary<string, string> { ["p1"] = "v1" },
			ProviderData = new Dictionary<string, string> { ["pd1"] = "pdv1" },
		};

		// Act
		var bytes = GrpcTransportMarshaller.ReceivedMessageMarshaller.Serializer(original);
		var deserialized = GrpcTransportMarshaller.ReceivedMessageMarshaller.Deserializer(bytes);

		// Assert
		deserialized.Id.ShouldBe("recv-1");
		deserialized.Body.ShouldBe("QUFB");
		deserialized.ContentType.ShouldBe("text/plain");
		deserialized.DeliveryCount.ShouldBe(2);
		deserialized.Source.ShouldBe("test-source");
		deserialized.Properties["p1"].ShouldBe("v1");
		deserialized.ProviderData["pd1"].ShouldBe("pdv1");
	}

	[Fact]
	public void RoundTripGrpcReceiveRequest()
	{
		// Arrange
		var original = new GrpcReceiveRequest { Source = "test-queue", MaxMessages = 5 };

		// Act
		var bytes = GrpcTransportMarshaller.ReceiveRequestMarshaller.Serializer(original);
		var deserialized = GrpcTransportMarshaller.ReceiveRequestMarshaller.Deserializer(bytes);

		// Assert
		deserialized.Source.ShouldBe("test-queue");
		deserialized.MaxMessages.ShouldBe(5);
	}

	[Fact]
	public void RoundTripGrpcReceiveResponse()
	{
		// Arrange
		var original = new GrpcReceiveResponse
		{
			Messages =
			[
				new GrpcReceivedMessage { Id = "m1" },
				new GrpcReceivedMessage { Id = "m2" },
			],
		};

		// Act
		var bytes = GrpcTransportMarshaller.ReceiveResponseMarshaller.Serializer(original);
		var deserialized = GrpcTransportMarshaller.ReceiveResponseMarshaller.Deserializer(bytes);

		// Assert
		deserialized.Messages.Count.ShouldBe(2);
	}

	[Fact]
	public void RoundTripGrpcAcknowledgeRequest()
	{
		// Arrange
		var original = new GrpcAcknowledgeRequest
		{
			MessageId = "msg-ack",
			Action = "acknowledge",
			Reason = "done",
		};

		// Act
		var bytes = GrpcTransportMarshaller.AcknowledgeRequestMarshaller.Serializer(original);
		var deserialized = GrpcTransportMarshaller.AcknowledgeRequestMarshaller.Deserializer(bytes);

		// Assert
		deserialized.MessageId.ShouldBe("msg-ack");
		deserialized.Action.ShouldBe("acknowledge");
		deserialized.Reason.ShouldBe("done");
	}

	[Fact]
	public void RoundTripGrpcAcknowledgeResponse()
	{
		// Arrange
		var original = new GrpcAcknowledgeResponse { IsSuccess = true };

		// Act
		var bytes = GrpcTransportMarshaller.AcknowledgeResponseMarshaller.Serializer(original);
		var deserialized = GrpcTransportMarshaller.AcknowledgeResponseMarshaller.Deserializer(bytes);

		// Assert
		deserialized.IsSuccess.ShouldBeTrue();
	}
}
