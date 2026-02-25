// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text;
using System.Text.Json;

using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Contract.Tests;

/// <summary>
/// Contract tests verifying TransportMessage serialization schema remains
/// backward compatible across versions. These tests ensure consumers can
/// safely deserialize messages produced by any compatible version.
/// </summary>
[Trait("Category", "Contract")]
public sealed class TransportMessageContractShould
{
	private static readonly JsonSerializerOptions SerializerOptions = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		WriteIndented = false,
	};

	[Fact]
	public void Serialize_WithAllProperties_ProducesExpectedSchema()
	{
		// Arrange
		var message = new TransportMessage
		{
			Id = "test-id-123",
			Body = Encoding.UTF8.GetBytes("{\"key\":\"value\"}"),
			ContentType = "application/json",
			MessageType = "OrderPlaced",
			CorrelationId = "corr-456",
			Subject = "orders",
			TimeToLive = TimeSpan.FromMinutes(5),
			CreatedAt = new DateTimeOffset(2026, 1, 15, 10, 30, 0, TimeSpan.Zero),
		};
		message.Properties["partitionKey"] = "tenant-1";

		// Act
		var json = JsonSerializer.Serialize(message, SerializerOptions);
		using var doc = JsonDocument.Parse(json);
		var root = doc.RootElement;

		// Assert — verify all expected properties exist in the serialized output
		root.TryGetProperty("id", out _).ShouldBeTrue("TransportMessage must have 'id' property");
		root.TryGetProperty("body", out _).ShouldBeTrue("TransportMessage must have 'body' property");
		root.TryGetProperty("contentType", out _).ShouldBeTrue("TransportMessage must have 'contentType' property");
		root.TryGetProperty("messageType", out _).ShouldBeTrue("TransportMessage must have 'messageType' property");
		root.TryGetProperty("correlationId", out _).ShouldBeTrue("TransportMessage must have 'correlationId' property");
		root.TryGetProperty("subject", out _).ShouldBeTrue("TransportMessage must have 'subject' property");
		root.TryGetProperty("timeToLive", out _).ShouldBeTrue("TransportMessage must have 'timeToLive' property");
		root.TryGetProperty("createdAt", out _).ShouldBeTrue("TransportMessage must have 'createdAt' property");
		root.TryGetProperty("properties", out _).ShouldBeTrue("TransportMessage must have 'properties' property");
	}

	[Fact]
	public void Deserialize_WithMinimalProperties_ProducesValidMessage()
	{
		// Arrange — minimal JSON with only required fields
		const string json = """{"id":"min-id","body":"AQID","createdAt":"2026-01-15T10:30:00+00:00"}""";

		// Act
		var message = JsonSerializer.Deserialize<TransportMessage>(json, SerializerOptions);

		// Assert
		message.ShouldNotBeNull();
		message.Id.ShouldBe("min-id");
		message.ContentType.ShouldBeNull();
		message.MessageType.ShouldBeNull();
		message.CorrelationId.ShouldBeNull();
		message.Subject.ShouldBeNull();
		message.TimeToLive.ShouldBeNull();
	}

	[Fact]
	public void Roundtrip_WithAllProperties_PreservesData()
	{
		// Arrange
		var original = new TransportMessage
		{
			Id = "roundtrip-id",
			Body = Encoding.UTF8.GetBytes("hello world"),
			ContentType = "text/plain",
			MessageType = "TestEvent",
			CorrelationId = "corr-789",
			Subject = "test-subject",
			TimeToLive = TimeSpan.FromHours(1),
			CreatedAt = new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero),
		};
		original.Properties["key1"] = "value1";

		// Act
		var json = JsonSerializer.Serialize(original, SerializerOptions);
		var deserialized = JsonSerializer.Deserialize<TransportMessage>(json, SerializerOptions);

		// Assert
		deserialized.ShouldNotBeNull();
		deserialized.Id.ShouldBe(original.Id);
		deserialized.ContentType.ShouldBe(original.ContentType);
		deserialized.MessageType.ShouldBe(original.MessageType);
		deserialized.CorrelationId.ShouldBe(original.CorrelationId);
		deserialized.Subject.ShouldBe(original.Subject);
	}

	[Fact]
	public void Schema_HasExpectedPropertyCount()
	{
		// Contract guard: TransportMessage should have exactly 10 public properties
		// (Id, Body, ContentType, MessageType, CorrelationId, Subject, TimeToLive, CreatedAt, Properties, HasProperties)
		// If this count changes, it indicates a schema change that needs review.
		var publicProperties = typeof(TransportMessage)
			.GetProperties()
			.ToList();

		publicProperties.Count.ShouldBe(10, "TransportMessage property count changed — this is a contract change that requires review");
	}

	[Fact]
	public void Deserialize_WithUnknownProperties_DoesNotThrow()
	{
		// Arrange — JSON with extra properties simulating a newer producer
		const string json = """
		{
			"id": "compat-id",
			"body": "AQID",
			"createdAt": "2026-01-15T10:30:00+00:00",
			"futureField": "some-value",
			"anotherNew": 42
		}
		""";

		// Act & Assert — deserialization should not throw for forward compatibility
		var message = JsonSerializer.Deserialize<TransportMessage>(json, SerializerOptions);
		message.ShouldNotBeNull();
		message.Id.ShouldBe("compat-id");
	}
}
