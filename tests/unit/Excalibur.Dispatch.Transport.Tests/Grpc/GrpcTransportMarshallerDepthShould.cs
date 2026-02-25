// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Grpc;

namespace Excalibur.Dispatch.Transport.Tests.Grpc;

/// <summary>
/// Depth coverage tests for <see cref="GrpcTransportMarshaller"/> covering
/// camelCase JSON naming, empty/minimal objects, null optional fields,
/// empty collections, and large payloads.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class GrpcTransportMarshallerDepthShould
{
	[Fact]
	public void SerializeRequestWithCamelCasePropertyNames()
	{
		// Arrange
		var request = new GrpcTransportRequest
		{
			Id = "test",
			Body = "SGVsbG8=",
			ContentType = "text/plain",
		};

		// Act
		var bytes = GrpcTransportMarshaller.RequestMarshaller.Serializer(request);
		var json = System.Text.Encoding.UTF8.GetString(bytes);

		// Assert — camelCase naming policy
		json.ShouldContain("\"id\":");
		json.ShouldContain("\"body\":");
		json.ShouldContain("\"contentType\":");
	}

	[Fact]
	public void RoundTripRequestWithAllNullOptionalFields()
	{
		// Arrange
		var request = new GrpcTransportRequest
		{
			Id = "minimal",
			Body = "",
			ContentType = null,
			MessageType = null,
			CorrelationId = null,
			Subject = null,
			Destination = null,
		};

		// Act
		var bytes = GrpcTransportMarshaller.RequestMarshaller.Serializer(request);
		var deserialized = GrpcTransportMarshaller.RequestMarshaller.Deserializer(bytes);

		// Assert
		deserialized.Id.ShouldBe("minimal");
		deserialized.Body.ShouldBe("");
		deserialized.ContentType.ShouldBeNull();
		deserialized.MessageType.ShouldBeNull();
		deserialized.CorrelationId.ShouldBeNull();
		deserialized.Subject.ShouldBeNull();
		deserialized.Destination.ShouldBeNull();
	}

	[Fact]
	public void RoundTripRequestWithEmptyProperties()
	{
		// Arrange
		var request = new GrpcTransportRequest { Id = "empty-props", Body = "", Properties = [] };

		// Act
		var bytes = GrpcTransportMarshaller.RequestMarshaller.Serializer(request);
		var deserialized = GrpcTransportMarshaller.RequestMarshaller.Deserializer(bytes);

		// Assert
		deserialized.Properties.ShouldNotBeNull();
		deserialized.Properties.ShouldBeEmpty();
	}

	[Fact]
	public void RoundTripRequestWithMultipleProperties()
	{
		// Arrange
		var props = new Dictionary<string, string>
		{
			["key1"] = "value1",
			["key2"] = "value2",
			["key3"] = "value3",
		};
		var request = new GrpcTransportRequest { Id = "multi-props", Body = "", Properties = props };

		// Act
		var bytes = GrpcTransportMarshaller.RequestMarshaller.Serializer(request);
		var deserialized = GrpcTransportMarshaller.RequestMarshaller.Deserializer(bytes);

		// Assert
		deserialized.Properties.Count.ShouldBe(3);
		deserialized.Properties["key1"].ShouldBe("value1");
		deserialized.Properties["key2"].ShouldBe("value2");
		deserialized.Properties["key3"].ShouldBe("value3");
	}

	[Fact]
	public void RoundTripResponseWithErrorFields()
	{
		// Arrange
		var response = new GrpcTransportResponse
		{
			IsSuccess = false,
			MessageId = null,
			ErrorCode = "UNAVAILABLE",
			ErrorMessage = "Server unavailable",
		};

		// Act
		var bytes = GrpcTransportMarshaller.ResponseMarshaller.Serializer(response);
		var deserialized = GrpcTransportMarshaller.ResponseMarshaller.Deserializer(bytes);

		// Assert
		deserialized.IsSuccess.ShouldBeFalse();
		deserialized.MessageId.ShouldBeNull();
		deserialized.ErrorCode.ShouldBe("UNAVAILABLE");
		deserialized.ErrorMessage.ShouldBe("Server unavailable");
	}

	[Fact]
	public void RoundTripBatchRequestWithEmptyMessages()
	{
		// Arrange
		var request = new GrpcBatchRequest { Messages = [] };

		// Act
		var bytes = GrpcTransportMarshaller.BatchRequestMarshaller.Serializer(request);
		var deserialized = GrpcTransportMarshaller.BatchRequestMarshaller.Deserializer(bytes);

		// Assert
		deserialized.Messages.ShouldNotBeNull();
		deserialized.Messages.ShouldBeEmpty();
	}

	[Fact]
	public void RoundTripBatchResponseWithMixedResults()
	{
		// Arrange
		var response = new GrpcBatchResponse
		{
			Results =
			[
				new GrpcTransportResponse { IsSuccess = true, MessageId = "ok-1" },
				new GrpcTransportResponse { IsSuccess = false, ErrorCode = "ERR", ErrorMessage = "fail" },
				new GrpcTransportResponse { IsSuccess = true, MessageId = "ok-2" },
			],
		};

		// Act
		var bytes = GrpcTransportMarshaller.BatchResponseMarshaller.Serializer(response);
		var deserialized = GrpcTransportMarshaller.BatchResponseMarshaller.Deserializer(bytes);

		// Assert
		deserialized.Results.Count.ShouldBe(3);
		deserialized.Results[0].IsSuccess.ShouldBeTrue();
		deserialized.Results[1].IsSuccess.ShouldBeFalse();
		deserialized.Results[1].ErrorCode.ShouldBe("ERR");
		deserialized.Results[2].IsSuccess.ShouldBeTrue();
	}

	[Fact]
	public void RoundTripReceivedMessageWithEmptyCollections()
	{
		// Arrange
		var msg = new GrpcReceivedMessage
		{
			Id = "msg-1",
			Body = "",
			Properties = [],
			ProviderData = [],
		};

		// Act
		var bytes = GrpcTransportMarshaller.ReceivedMessageMarshaller.Serializer(msg);
		var deserialized = GrpcTransportMarshaller.ReceivedMessageMarshaller.Deserializer(bytes);

		// Assert
		deserialized.Properties.ShouldBeEmpty();
		deserialized.ProviderData.ShouldBeEmpty();
	}

	[Fact]
	public void RoundTripReceivedMessageWithAllOptionalFieldsNull()
	{
		// Arrange
		var msg = new GrpcReceivedMessage
		{
			Id = "msg-null",
			Body = "AA==",
			ContentType = null,
			MessageType = null,
			CorrelationId = null,
			Subject = null,
			Source = null,
		};

		// Act
		var bytes = GrpcTransportMarshaller.ReceivedMessageMarshaller.Serializer(msg);
		var deserialized = GrpcTransportMarshaller.ReceivedMessageMarshaller.Deserializer(bytes);

		// Assert
		deserialized.ContentType.ShouldBeNull();
		deserialized.MessageType.ShouldBeNull();
		deserialized.CorrelationId.ShouldBeNull();
		deserialized.Subject.ShouldBeNull();
		deserialized.Source.ShouldBeNull();
	}

	[Fact]
	public void RoundTripReceiveResponseWithMessages()
	{
		// Arrange
		var response = new GrpcReceiveResponse
		{
			Messages =
			[
				new GrpcReceivedMessage { Id = "r1", Body = "AA==" },
				new GrpcReceivedMessage { Id = "r2", Body = "Qg==" },
			],
		};

		// Act
		var bytes = GrpcTransportMarshaller.ReceiveResponseMarshaller.Serializer(response);
		var deserialized = GrpcTransportMarshaller.ReceiveResponseMarshaller.Deserializer(bytes);

		// Assert
		deserialized.Messages.Count.ShouldBe(2);
		deserialized.Messages[0].Id.ShouldBe("r1");
		deserialized.Messages[1].Id.ShouldBe("r2");
	}

	[Fact]
	public void RoundTripAcknowledgeRequestWithNullReason()
	{
		// Arrange
		var request = new GrpcAcknowledgeRequest
		{
			MessageId = "ack-1",
			Action = "reject",
			Reason = null,
		};

		// Act
		var bytes = GrpcTransportMarshaller.AcknowledgeRequestMarshaller.Serializer(request);
		var deserialized = GrpcTransportMarshaller.AcknowledgeRequestMarshaller.Deserializer(bytes);

		// Assert
		deserialized.MessageId.ShouldBe("ack-1");
		deserialized.Action.ShouldBe("reject");
		deserialized.Reason.ShouldBeNull();
	}

	[Fact]
	public void BeInternalAndStatic()
	{
		// Assert
		typeof(GrpcTransportMarshaller).IsNotPublic.ShouldBeTrue();
		typeof(GrpcTransportMarshaller).IsAbstract.ShouldBeTrue();
		typeof(GrpcTransportMarshaller).IsSealed.ShouldBeTrue();
	}
}
