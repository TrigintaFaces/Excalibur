// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.CloudEvents;

namespace Excalibur.Dispatch.Tests.CloudEvents;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class CloudEventContentTypesShould
{
	[Fact]
	public void DefineCloudEventsJsonConstant()
	{
		CloudEventContentTypes.CloudEventsJson.ShouldBe("APPLICATION/CLOUDEVENTS+JSON");
	}

	[Fact]
	public void DefineCloudEventsBatchJsonConstant()
	{
		CloudEventContentTypes.CloudEventsBatchJson.ShouldBe("APPLICATION/CLOUDEVENTS-BATCH+JSON");
	}

	[Fact]
	public void DefineApplicationJsonConstant()
	{
		CloudEventContentTypes.ApplicationJson.ShouldBe("APPLICATION/JSON");
	}

	[Fact]
	public void GetFormatterForCloudEventsJson()
	{
		// Act
		var formatter = CloudEventContentTypes.GetFormatter("application/cloudevents+json");

		// Assert
		formatter.ShouldNotBeNull();
	}

	[Fact]
	public void GetFormatterForApplicationJson()
	{
		// Act
		var formatter = CloudEventContentTypes.GetFormatter("application/json");

		// Assert
		formatter.ShouldNotBeNull();
	}

	[Fact]
	public void GetFormatterForContentTypeWithParameters()
	{
		// Act - should strip charset parameter before matching
		var formatter = CloudEventContentTypes.GetFormatter("application/cloudevents+json; charset=utf-8");

		// Assert
		formatter.ShouldNotBeNull();
	}

	[Fact]
	public void ThrowForUnsupportedContentType()
	{
		// Act & Assert
		Should.Throw<NotSupportedException>(() => CloudEventContentTypes.GetFormatter("text/xml"));
	}

	[Fact]
	public void ThrowForNullContentType()
	{
		// Act & Assert
		Should.Throw<ArgumentException>(() => CloudEventContentTypes.GetFormatter(null!));
		Should.Throw<ArgumentException>(() => CloudEventContentTypes.GetFormatter(""));
	}

	[Fact]
	public void NegotiateDefaultContentType()
	{
		// Act
		var result = CloudEventContentTypes.NegotiateContentType(null);

		// Assert
		result.ShouldBe(CloudEventContentTypes.CloudEventsJson);
	}

	[Fact]
	public void NegotiateCloudEventsJsonContentType()
	{
		// Act
		var result = CloudEventContentTypes.NegotiateContentType("application/cloudevents+json");

		// Assert
		result.ShouldBe(CloudEventContentTypes.CloudEventsJson);
	}

	[Fact]
	public void NegotiateApplicationJsonContentType()
	{
		// Act
		var result = CloudEventContentTypes.NegotiateContentType("application/json");

		// Assert
		result.ShouldBe(CloudEventContentTypes.ApplicationJson);
	}

	[Fact]
	public void NegotiateWildcardContentType()
	{
		// Act
		var result = CloudEventContentTypes.NegotiateContentType("*/*");

		// Assert
		result.ShouldBe(CloudEventContentTypes.CloudEventsJson);
	}

	[Fact]
	public void NegotiateApplicationWildcardContentType()
	{
		// Act
		var result = CloudEventContentTypes.NegotiateContentType("application/*");

		// Assert
		result.ShouldBe(CloudEventContentTypes.CloudEventsJson);
	}

	[Fact]
	public void NegotiateBatchContentTypeWhenSupported()
	{
		// Act
		var result = CloudEventContentTypes.NegotiateContentType("application/cloudevents-batch+json", supportsBatch: true);

		// Assert
		result.ShouldBe(CloudEventContentTypes.CloudEventsBatchJson);
	}

	[Fact]
	public void IgnoreBatchContentTypeWhenNotSupported()
	{
		// Act - batch requested but not supported, should fallback to default
		var result = CloudEventContentTypes.NegotiateContentType("application/cloudevents-batch+json", supportsBatch: false);

		// Assert
		result.ShouldBe(CloudEventContentTypes.CloudEventsJson);
	}

	[Fact]
	public void NegotiateWithQualityFactors()
	{
		// Act - json at q=0.9, cloudevents at q=1.0
		var result = CloudEventContentTypes.NegotiateContentType(
			"application/json;q=0.9, application/cloudevents+json;q=1.0");

		// Assert - higher quality should win
		result.ShouldBe(CloudEventContentTypes.CloudEventsJson);
	}

	[Fact]
	public void NegotiateWithEmptyAcceptHeader()
	{
		// Act
		var result = CloudEventContentTypes.NegotiateContentType("   ");

		// Assert
		result.ShouldBe(CloudEventContentTypes.CloudEventsJson);
	}

	[Fact]
	public void CreateContentTypeHeaderWithCharset()
	{
		// Act
		var result = CloudEventContentTypes.CreateContentTypeHeader("application/json", charset: "utf-8");

		// Assert
		result.ShouldBe("application/json; charset=utf-8");
	}

	[Fact]
	public void CreateContentTypeHeaderWithoutCharset()
	{
		// Act
		var result = CloudEventContentTypes.CreateContentTypeHeader("application/json", charset: null);

		// Assert
		result.ShouldBe("application/json");
	}

	[Fact]
	public void CreateContentTypeHeaderWithParameters()
	{
		// Act
		var parameters = new Dictionary<string, string>
		{
			["profile"] = "cloudevents",
			["version"] = "1.0",
		};
		var result = CloudEventContentTypes.CreateContentTypeHeader("application/json", charset: "utf-8", parameters: parameters);

		// Assert
		result.ShouldContain("charset=utf-8");
		result.ShouldContain("profile=cloudevents");
		result.ShouldContain("version=1.0");
	}

	[Fact]
	public void SerializeThrowsForNullCloudEvent()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(
			() => CloudEventContentTypes.Serialize(null!, "application/cloudevents+json"));
	}

	[Fact]
	public void SerializeThrowsForNullContentType()
	{
		// Act & Assert
		Should.Throw<ArgumentException>(
			() => CloudEventContentTypes.Serialize(new global::CloudNative.CloudEvents.CloudEvent(), null!));
	}

	[Fact]
	public void SerializeBatchThrowsForNullBatch()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(
			() => CloudEventContentTypes.SerializeBatch(null!));
	}

	[Fact]
	public void SerializeBatchThrowsForUnsupportedContentType()
	{
		// Arrange
		var batch = new CloudEventBatch();

		// Act & Assert
		Should.Throw<ArgumentException>(
			() => CloudEventContentTypes.SerializeBatch(batch, "text/xml"));
	}

	[Fact]
	public void DeserializeThrowsForNullData()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(
			() => CloudEventContentTypes.Deserialize(null!, "application/cloudevents+json"));
	}

	[Fact]
	public void DeserializeBatchThrowsForNullData()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(
			() => CloudEventContentTypes.DeserializeBatch(null!));
	}

	[Fact]
	public void DeserializeBatchThrowsForUnsupportedContentType()
	{
		// Act & Assert
		Should.Throw<ArgumentException>(
			() => CloudEventContentTypes.DeserializeBatch([], "text/xml"));
	}
}
