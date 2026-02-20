// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Grpc;

namespace Excalibur.Dispatch.Transport.Tests.Grpc;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class GrpcTransportMessagesShould
{
	[Fact]
	public void CreateGrpcTransportRequestWithDefaults()
	{
		// Arrange & Act
		var request = new GrpcTransportRequest();

		// Assert
		request.Id.ShouldBe(string.Empty);
		request.Body.ShouldBe(string.Empty);
		request.ContentType.ShouldBeNull();
		request.MessageType.ShouldBeNull();
		request.CorrelationId.ShouldBeNull();
		request.Subject.ShouldBeNull();
		request.Destination.ShouldBeNull();
		request.Properties.ShouldNotBeNull();
		request.Properties.ShouldBeEmpty();
	}

	[Fact]
	public void SetGrpcTransportRequestProperties()
	{
		// Arrange & Act
		var request = new GrpcTransportRequest
		{
			Id = "msg-123",
			Body = "SGVsbG8=",
			ContentType = "application/json",
			MessageType = "OrderCreated",
			CorrelationId = "corr-456",
			Subject = "orders",
			Destination = "order-queue",
			Properties = new Dictionary<string, string>
			{
				["key1"] = "value1",
				["key2"] = "value2",
			},
		};

		// Assert
		request.Id.ShouldBe("msg-123");
		request.Body.ShouldBe("SGVsbG8=");
		request.ContentType.ShouldBe("application/json");
		request.MessageType.ShouldBe("OrderCreated");
		request.CorrelationId.ShouldBe("corr-456");
		request.Subject.ShouldBe("orders");
		request.Destination.ShouldBe("order-queue");
		request.Properties.Count.ShouldBe(2);
	}

	[Fact]
	public void CreateGrpcTransportResponseWithDefaults()
	{
		// Arrange & Act
		var response = new GrpcTransportResponse();

		// Assert
		response.IsSuccess.ShouldBeFalse();
		response.MessageId.ShouldBeNull();
		response.ErrorCode.ShouldBeNull();
		response.ErrorMessage.ShouldBeNull();
	}

	[Fact]
	public void SetGrpcTransportResponseProperties()
	{
		// Arrange & Act
		var response = new GrpcTransportResponse
		{
			IsSuccess = true,
			MessageId = "msg-789",
			ErrorCode = null,
			ErrorMessage = null,
		};

		// Assert
		response.IsSuccess.ShouldBeTrue();
		response.MessageId.ShouldBe("msg-789");
	}

	[Fact]
	public void CreateGrpcBatchRequestWithDefaults()
	{
		// Arrange & Act
		var request = new GrpcBatchRequest();

		// Assert
		request.Messages.ShouldNotBeNull();
		request.Messages.ShouldBeEmpty();
	}

	[Fact]
	public void AddMessagesToGrpcBatchRequest()
	{
		// Arrange & Act
		var request = new GrpcBatchRequest
		{
			Messages =
			[
				new GrpcTransportRequest { Id = "msg-1" },
				new GrpcTransportRequest { Id = "msg-2" },
			],
		};

		// Assert
		request.Messages.Count.ShouldBe(2);
		request.Messages[0].Id.ShouldBe("msg-1");
		request.Messages[1].Id.ShouldBe("msg-2");
	}

	[Fact]
	public void CreateGrpcBatchResponseWithDefaults()
	{
		// Arrange & Act
		var response = new GrpcBatchResponse();

		// Assert
		response.Results.ShouldNotBeNull();
		response.Results.ShouldBeEmpty();
	}

	[Fact]
	public void CreateGrpcSubscribeRequestWithDefaults()
	{
		// Arrange & Act
		var request = new GrpcSubscribeRequest();

		// Assert
		request.Source.ShouldBe(string.Empty);
	}

	[Fact]
	public void SetGrpcSubscribeRequestSource()
	{
		// Arrange & Act
		var request = new GrpcSubscribeRequest { Source = "my-subscription" };

		// Assert
		request.Source.ShouldBe("my-subscription");
	}

	[Fact]
	public void CreateGrpcReceivedMessageWithDefaults()
	{
		// Arrange & Act
		var msg = new GrpcReceivedMessage();

		// Assert
		msg.Id.ShouldBe(string.Empty);
		msg.Body.ShouldBe(string.Empty);
		msg.ContentType.ShouldBeNull();
		msg.MessageType.ShouldBeNull();
		msg.CorrelationId.ShouldBeNull();
		msg.Subject.ShouldBeNull();
		msg.DeliveryCount.ShouldBe(0);
		msg.Source.ShouldBeNull();
		msg.Properties.ShouldNotBeNull();
		msg.Properties.ShouldBeEmpty();
		msg.ProviderData.ShouldNotBeNull();
		msg.ProviderData.ShouldBeEmpty();
	}

	[Fact]
	public void SetGrpcReceivedMessageProperties()
	{
		// Arrange & Act
		var msg = new GrpcReceivedMessage
		{
			Id = "recv-123",
			Body = "SGVsbG8=",
			ContentType = "application/json",
			MessageType = "OrderCreated",
			CorrelationId = "corr-123",
			Subject = "orders",
			DeliveryCount = 3,
			Source = "test-source",
			Properties = new Dictionary<string, string> { ["key"] = "value" },
			ProviderData = new Dictionary<string, string> { ["provider-key"] = "provider-value" },
		};

		// Assert
		msg.Id.ShouldBe("recv-123");
		msg.Body.ShouldBe("SGVsbG8=");
		msg.ContentType.ShouldBe("application/json");
		msg.MessageType.ShouldBe("OrderCreated");
		msg.CorrelationId.ShouldBe("corr-123");
		msg.Subject.ShouldBe("orders");
		msg.DeliveryCount.ShouldBe(3);
		msg.Source.ShouldBe("test-source");
		msg.Properties.Count.ShouldBe(1);
		msg.ProviderData.Count.ShouldBe(1);
	}

	[Fact]
	public void CreateGrpcReceiveRequestWithDefaults()
	{
		// Arrange & Act
		var request = new GrpcReceiveRequest();

		// Assert
		request.Source.ShouldBe(string.Empty);
		request.MaxMessages.ShouldBe(0);
	}

	[Fact]
	public void SetGrpcReceiveRequestProperties()
	{
		// Arrange & Act
		var request = new GrpcReceiveRequest
		{
			Source = "test-queue",
			MaxMessages = 10,
		};

		// Assert
		request.Source.ShouldBe("test-queue");
		request.MaxMessages.ShouldBe(10);
	}

	[Fact]
	public void CreateGrpcReceiveResponseWithDefaults()
	{
		// Arrange & Act
		var response = new GrpcReceiveResponse();

		// Assert
		response.Messages.ShouldNotBeNull();
		response.Messages.ShouldBeEmpty();
	}

	[Fact]
	public void CreateGrpcAcknowledgeRequestWithDefaults()
	{
		// Arrange & Act
		var request = new GrpcAcknowledgeRequest();

		// Assert
		request.MessageId.ShouldBe(string.Empty);
		request.Action.ShouldBe(string.Empty);
		request.Reason.ShouldBeNull();
	}

	[Fact]
	public void SetGrpcAcknowledgeRequestProperties()
	{
		// Arrange & Act
		var request = new GrpcAcknowledgeRequest
		{
			MessageId = "msg-123",
			Action = "acknowledge",
			Reason = "processed",
		};

		// Assert
		request.MessageId.ShouldBe("msg-123");
		request.Action.ShouldBe("acknowledge");
		request.Reason.ShouldBe("processed");
	}

	[Fact]
	public void CreateGrpcAcknowledgeResponseWithDefaults()
	{
		// Arrange & Act
		var response = new GrpcAcknowledgeResponse();

		// Assert
		response.IsSuccess.ShouldBeFalse();
	}

	[Fact]
	public void SetGrpcAcknowledgeResponseSuccess()
	{
		// Arrange & Act
		var response = new GrpcAcknowledgeResponse { IsSuccess = true };

		// Assert
		response.IsSuccess.ShouldBeTrue();
	}
}
