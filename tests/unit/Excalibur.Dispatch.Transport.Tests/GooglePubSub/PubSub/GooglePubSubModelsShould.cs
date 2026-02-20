// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Transport.Google;

namespace Excalibur.Dispatch.Transport.Tests.GooglePubSub.PubSub;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class GooglePubSubModelsShould
{
	[Fact]
	public void CreateAckError()
	{
		// Arrange & Act
		var error = new AckError("ack-123", "Failed to ack");

		// Assert
		error.AckId.ShouldBe("ack-123");
		error.Message.ShouldBe("Failed to ack");
		error.Exception.ShouldBeNull();
	}

	[Fact]
	public void CreateAckErrorWithException()
	{
		// Arrange
		var ex = new InvalidOperationException("test error");

		// Act
		var error = new AckError("ack-456", "Error", ex);

		// Assert
		error.Exception.ShouldBe(ex);
	}

	[Fact]
	public void SupportAckErrorRecordEquality()
	{
		var e1 = new AckError("a", "msg");
		var e2 = new AckError("a", "msg");
		e1.ShouldBe(e2);
	}

	[Fact]
	public void CreateSchemaDefinition()
	{
		// Arrange
		var metadata = new Dictionary<string, string> { ["owner"] = "team-a" };

		// Act
		var schema = new SchemaDefinition("schema-1", "JSON", "{\"type\":\"object\"}", metadata);

		// Assert
		schema.SchemaId.ShouldBe("schema-1");
		schema.SchemaType.ShouldBe("JSON");
		schema.Definition.ShouldBe("{\"type\":\"object\"}");
		schema.Metadata.ShouldNotBeNull();
		schema.Metadata!["owner"].ShouldBe("team-a");
	}

	[Fact]
	public void CreateSchemaDefinitionWithoutMetadata()
	{
		// Act
		var schema = new SchemaDefinition("s1", "Avro", "{}");

		// Assert
		schema.Metadata.ShouldBeNull();
	}

	[Fact]
	public void CreateSchemaMetadata()
	{
		// Arrange
		var now = DateTimeOffset.UtcNow;

		// Act
		var metadata = new SchemaMetadata
		{
			TypeName = "OrderCreated",
			Schema = "{\"type\":\"record\"}",
			Version = 3,
			Format = SerializationFormat.Json,
			RegisteredAt = now,
			Metadata = new Dictionary<string, string> { ["source"] = "api" },
		};

		// Assert
		metadata.TypeName.ShouldBe("OrderCreated");
		metadata.Schema.ShouldBe("{\"type\":\"record\"}");
		metadata.Version.ShouldBe(3);
		metadata.Format.ShouldBe(SerializationFormat.Json);
		metadata.RegisteredAt.ShouldBe(now);
		metadata.Metadata["source"].ShouldBe("api");
	}

	[Fact]
	public void HaveCorrectSchemaMetadataDefaults()
	{
		// Arrange & Act
		var metadata = new SchemaMetadata();

		// Assert
		metadata.TypeName.ShouldBe(string.Empty);
		metadata.Schema.ShouldBe(string.Empty);
		metadata.Version.ShouldBe(0);
		metadata.Metadata.ShouldBeEmpty();
	}

	[Fact]
	public void CreateProcessingResult()
	{
		// Arrange & Act
		var result = new ProcessingResult
		{
			Success = true,
			WorkerId = 3,
			ProcessingTime = TimeSpan.FromMilliseconds(50),
		};

		// Assert
		result.Success.ShouldBeTrue();
		result.WorkerId.ShouldBe(3);
		result.ProcessingTime.ShouldBe(TimeSpan.FromMilliseconds(50));
	}

	[Fact]
	public void HaveCorrectProcessingResultDefaults()
	{
		// Arrange & Act
		var result = new ProcessingResult();

		// Assert
		result.Success.ShouldBeFalse();
		result.WorkerId.ShouldBe(0);
		result.ProcessingTime.ShouldBe(TimeSpan.Zero);
	}
}
