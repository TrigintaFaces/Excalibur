// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using CloudNative.CloudEvents;

using Excalibur.Dispatch.CloudEvents;

namespace Excalibur.Dispatch.Tests.Messaging.CloudEvents;

/// <summary>
/// Unit tests for <see cref="CloudEventContentTypes"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class CloudEventContentTypesShould
{
	[Fact]
	public void HaveCloudEventsJsonConstant()
	{
		// Assert
		CloudEventContentTypes.CloudEventsJson.ShouldBe("APPLICATION/CLOUDEVENTS+JSON");
	}

	[Fact]
	public void HaveCloudEventsBatchJsonConstant()
	{
		// Assert
		CloudEventContentTypes.CloudEventsBatchJson.ShouldBe("APPLICATION/CLOUDEVENTS-BATCH+JSON");
	}

	[Fact]
	public void HaveApplicationJsonConstant()
	{
		// Assert
		CloudEventContentTypes.ApplicationJson.ShouldBe("APPLICATION/JSON");
	}

	[Fact]
	public void HaveApplicationOctetStreamConstant()
	{
		// Assert
		CloudEventContentTypes.ApplicationOctetStream.ShouldBe("application/octet-stream");
	}

	// GetFormatter tests
	[Fact]
	public void ThrowOnGetFormatterWithNullContentType()
	{
		// Arrange & Act & Assert
		Should.Throw<ArgumentException>(() => CloudEventContentTypes.GetFormatter(null!));
	}

	[Fact]
	public void ThrowOnGetFormatterWithEmptyContentType()
	{
		// Arrange & Act & Assert
		Should.Throw<ArgumentException>(() => CloudEventContentTypes.GetFormatter(""));
	}

	[Fact]
	public void ThrowOnGetFormatterWithWhitespaceContentType()
	{
		// Arrange & Act & Assert
		Should.Throw<ArgumentException>(() => CloudEventContentTypes.GetFormatter("   "));
	}

	[Fact]
	public void ReturnFormatterForCloudEventsJson()
	{
		// Arrange & Act
		var formatter = CloudEventContentTypes.GetFormatter(CloudEventContentTypes.CloudEventsJson);

		// Assert
		formatter.ShouldNotBeNull();
	}

	[Fact]
	public void ReturnFormatterForApplicationJson()
	{
		// Arrange & Act
		var formatter = CloudEventContentTypes.GetFormatter(CloudEventContentTypes.ApplicationJson);

		// Assert
		formatter.ShouldNotBeNull();
	}

	[Fact]
	public void ReturnFormatterForLowercaseContentType()
	{
		// Arrange & Act
		var formatter = CloudEventContentTypes.GetFormatter("application/cloudevents+json");

		// Assert
		formatter.ShouldNotBeNull();
	}

	[Fact]
	public void ReturnFormatterForContentTypeWithCharset()
	{
		// Arrange & Act
		var formatter = CloudEventContentTypes.GetFormatter("application/cloudevents+json; charset=utf-8");

		// Assert
		formatter.ShouldNotBeNull();
	}

	[Fact]
	public void ThrowOnGetFormatterForUnsupportedContentType()
	{
		// Arrange & Act & Assert
		Should.Throw<NotSupportedException>(() => CloudEventContentTypes.GetFormatter("text/plain"));
	}

	// Serialize tests
	[Fact]
	public void ThrowOnSerializeWithNullCloudEvent()
	{
		// Arrange & Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			CloudEventContentTypes.Serialize(null!, CloudEventContentTypes.CloudEventsJson));
	}

	[Fact]
	public void ThrowOnSerializeWithNullContentType()
	{
		// Arrange
		var cloudEvent = CreateTestCloudEvent();

		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			CloudEventContentTypes.Serialize(cloudEvent, null!));
	}

	[Fact]
	public void SerializeCloudEventToBytes()
	{
		// Arrange
		var cloudEvent = CreateTestCloudEvent();

		// Act
		var bytes = CloudEventContentTypes.Serialize(cloudEvent, CloudEventContentTypes.CloudEventsJson);

		// Assert
		bytes.ShouldNotBeNull();
		bytes.Length.ShouldBeGreaterThan(0);
	}

	// SerializeBatch tests
	[Fact]
	public void ThrowOnSerializeBatchWithNullBatch()
	{
		// Arrange & Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			CloudEventContentTypes.SerializeBatch(null!));
	}

	[Fact]
	public void ThrowOnSerializeBatchWithUnsupportedContentType()
	{
		// Arrange
		var batch = new CloudEventBatch();

		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			CloudEventContentTypes.SerializeBatch(batch, "text/plain"));
	}

	[Fact]
	public void SerializeEmptyBatch()
	{
		// Arrange
		var batch = new CloudEventBatch();

		// Act
		var bytes = CloudEventContentTypes.SerializeBatch(batch);

		// Assert
		bytes.ShouldNotBeNull();
	}

	[Fact]
	public void SerializeBatchWithEvents()
	{
		// Arrange
		var batch = new CloudEventBatch();
		batch.TryAdd(CreateTestCloudEvent());
		batch.TryAdd(CreateTestCloudEvent());

		// Act
		var bytes = CloudEventContentTypes.SerializeBatch(batch);

		// Assert
		bytes.ShouldNotBeNull();
		bytes.Length.ShouldBeGreaterThan(0);
	}

	// Deserialize tests
	[Fact]
	public void ThrowOnDeserializeWithNullData()
	{
		// Arrange & Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			CloudEventContentTypes.Deserialize(null!, CloudEventContentTypes.CloudEventsJson));
	}

	[Fact]
	public void ThrowOnDeserializeWithNullContentType()
	{
		// Arrange
		var data = Array.Empty<byte>();

		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			CloudEventContentTypes.Deserialize(data, null!));
	}

	[Fact]
	public void RoundTripSerializeAndDeserialize()
	{
		// Arrange
		var originalEvent = CreateTestCloudEvent();
		var bytes = CloudEventContentTypes.Serialize(originalEvent, CloudEventContentTypes.CloudEventsJson);

		// Act
		var deserialized = CloudEventContentTypes.Deserialize(bytes, CloudEventContentTypes.CloudEventsJson);

		// Assert
		deserialized.ShouldNotBeNull();
		deserialized.Id.ShouldBe(originalEvent.Id);
		deserialized.Type.ShouldBe(originalEvent.Type);
		deserialized.Source.ShouldBe(originalEvent.Source);
	}

	// DeserializeBatch tests
	[Fact]
	public void ThrowOnDeserializeBatchWithNullData()
	{
		// Arrange & Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			CloudEventContentTypes.DeserializeBatch(null!));
	}

	[Fact]
	public void ThrowOnDeserializeBatchWithUnsupportedContentType()
	{
		// Arrange
		var data = Array.Empty<byte>();

		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			CloudEventContentTypes.DeserializeBatch(data, "text/plain"));
	}

	[Fact]
	public void RoundTripSerializeAndDeserializeBatch()
	{
		// Arrange
		var batch = new CloudEventBatch();
		var event1 = CreateTestCloudEvent();
		var event2 = CreateTestCloudEvent();
		batch.TryAdd(event1);
		batch.TryAdd(event2);
		var bytes = CloudEventContentTypes.SerializeBatch(batch);

		// Act
		var deserialized = CloudEventContentTypes.DeserializeBatch(bytes);

		// Assert
		deserialized.ShouldNotBeNull();
		deserialized.Count.ShouldBe(2);
	}

	// NegotiateContentType tests
	[Fact]
	public void ReturnCloudEventsJsonForNullAcceptHeader()
	{
		// Arrange & Act
		var result = CloudEventContentTypes.NegotiateContentType(null);

		// Assert
		result.ShouldBe(CloudEventContentTypes.CloudEventsJson);
	}

	[Fact]
	public void ReturnCloudEventsJsonForEmptyAcceptHeader()
	{
		// Arrange & Act
		var result = CloudEventContentTypes.NegotiateContentType("");

		// Assert
		result.ShouldBe(CloudEventContentTypes.CloudEventsJson);
	}

	[Fact]
	public void ReturnCloudEventsJsonForWhitespaceAcceptHeader()
	{
		// Arrange & Act
		var result = CloudEventContentTypes.NegotiateContentType("   ");

		// Assert
		result.ShouldBe(CloudEventContentTypes.CloudEventsJson);
	}

	[Fact]
	public void ReturnCloudEventsJsonForExactMatch()
	{
		// Arrange & Act
		var result = CloudEventContentTypes.NegotiateContentType(CloudEventContentTypes.CloudEventsJson);

		// Assert
		result.ShouldBe(CloudEventContentTypes.CloudEventsJson);
	}

	[Fact]
	public void ReturnApplicationJsonForExactMatch()
	{
		// Arrange & Act
		var result = CloudEventContentTypes.NegotiateContentType(CloudEventContentTypes.ApplicationJson);

		// Assert
		result.ShouldBe(CloudEventContentTypes.ApplicationJson);
	}

	[Fact]
	public void ReturnCloudEventsJsonForWildcard()
	{
		// Arrange & Act
		var result = CloudEventContentTypes.NegotiateContentType("*/*");

		// Assert
		result.ShouldBe(CloudEventContentTypes.CloudEventsJson);
	}

	[Fact]
	public void ReturnCloudEventsJsonForApplicationWildcard()
	{
		// Arrange & Act
		var result = CloudEventContentTypes.NegotiateContentType("application/*");

		// Assert
		result.ShouldBe(CloudEventContentTypes.CloudEventsJson);
	}

	[Fact]
	public void ReturnBatchJsonWhenSupportedAndRequested()
	{
		// Arrange & Act
		var result = CloudEventContentTypes.NegotiateContentType(
			CloudEventContentTypes.CloudEventsBatchJson,
			supportsBatch: true);

		// Assert
		result.ShouldBe(CloudEventContentTypes.CloudEventsBatchJson);
	}

	[Fact]
	public void SkipBatchJsonWhenNotSupported()
	{
		// Arrange & Act
		var result = CloudEventContentTypes.NegotiateContentType(
			CloudEventContentTypes.CloudEventsBatchJson,
			supportsBatch: false);

		// Assert
		result.ShouldBe(CloudEventContentTypes.CloudEventsJson);
	}

	[Fact]
	public void SelectHighestQualityAcceptType()
	{
		// Arrange - JSON with q=0.9, CloudEvents with q=1.0
		var acceptHeader = $"{CloudEventContentTypes.ApplicationJson}; q=0.9, {CloudEventContentTypes.CloudEventsJson}; q=1.0";

		// Act
		var result = CloudEventContentTypes.NegotiateContentType(acceptHeader);

		// Assert
		result.ShouldBe(CloudEventContentTypes.CloudEventsJson);
	}

	[Fact]
	public void HandleMultipleAcceptTypes()
	{
		// Arrange
		var acceptHeader = "text/plain, application/json, application/cloudevents+json";

		// Act
		var result = CloudEventContentTypes.NegotiateContentType(acceptHeader);

		// Assert
		// Should return application/json (first matching supported type)
		result.ShouldBe(CloudEventContentTypes.ApplicationJson);
	}

	// CreateContentTypeHeader tests
	[Fact]
	public void CreateContentTypeHeaderWithCharset()
	{
		// Arrange & Act
		var result = CloudEventContentTypes.CreateContentTypeHeader(
			CloudEventContentTypes.CloudEventsJson,
			charset: "utf-8");

		// Assert
		result.ShouldBe($"{CloudEventContentTypes.CloudEventsJson}; charset=utf-8");
	}

	[Fact]
	public void CreateContentTypeHeaderWithoutCharset()
	{
		// Arrange & Act
		var result = CloudEventContentTypes.CreateContentTypeHeader(
			CloudEventContentTypes.CloudEventsJson,
			charset: null);

		// Assert
		result.ShouldBe(CloudEventContentTypes.CloudEventsJson);
	}

	[Fact]
	public void CreateContentTypeHeaderWithEmptyCharset()
	{
		// Arrange & Act
		var result = CloudEventContentTypes.CreateContentTypeHeader(
			CloudEventContentTypes.CloudEventsJson,
			charset: "");

		// Assert
		result.ShouldBe(CloudEventContentTypes.CloudEventsJson);
	}

	[Fact]
	public void CreateContentTypeHeaderWithParameters()
	{
		// Arrange
		var parameters = new Dictionary<string, string>
		{
			["boundary"] = "boundary123",
			["profile"] = "custom",
		};

		// Act
		var result = CloudEventContentTypes.CreateContentTypeHeader(
			CloudEventContentTypes.CloudEventsJson,
			charset: "utf-8",
			parameters: parameters);

		// Assert
		result.ShouldContain("; charset=utf-8");
		result.ShouldContain("; boundary=boundary123");
		result.ShouldContain("; profile=custom");
	}

	[Fact]
	public void CreateContentTypeHeaderWithNullParameters()
	{
		// Arrange & Act
		var result = CloudEventContentTypes.CreateContentTypeHeader(
			CloudEventContentTypes.ApplicationJson,
			charset: "utf-8",
			parameters: null);

		// Assert
		result.ShouldBe($"{CloudEventContentTypes.ApplicationJson}; charset=utf-8");
	}

	private static CloudEvent CreateTestCloudEvent() =>
		new()
		{
			Id = Guid.NewGuid().ToString(),
			Source = new Uri("https://test.example.com/source"),
			Type = "test.event",
			Time = DateTimeOffset.UtcNow,
			DataContentType = "application/json",
			Data = new { Message = "Test" },
		};
}
