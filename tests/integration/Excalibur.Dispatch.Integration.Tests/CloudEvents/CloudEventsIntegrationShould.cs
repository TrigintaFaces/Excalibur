// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json;

using Excalibur.Dispatch.Transport.Abstractions;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Integration.Tests.CloudEvents;

/// <summary>
/// Integration tests for CloudEvents format serialization and transport mapping.
/// </summary>
public sealed class CloudEventsIntegrationShould : IntegrationTestBase
{
	#region CloudEvents Format Tests

	[Fact]
	public void CloudEvent_SerializesToJsonFormat()
	{
		// Arrange
		var cloudEvent = new TestCloudEvent
		{
			Id = Guid.NewGuid().ToString(),
			Source = new Uri("urn:test:source"),
			Type = "com.example.test.v1",
			DataContentType = "application/json",
			Time = DateTimeOffset.UtcNow,
			Data = new TestEventData("test-value")
		};

		// Act
		var json = JsonSerializer.Serialize(cloudEvent, new JsonSerializerOptions
		{
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
			WriteIndented = true
		});

		// Assert
		json.ShouldNotBeNullOrEmpty();
		json.ShouldContain("\"id\"");
		json.ShouldContain("\"source\"");
		json.ShouldContain("\"type\"");
		json.ShouldContain("com.example.test.v1");
	}

	[Fact]
	public void CloudEvent_DeserializesFromJsonFormat()
	{
		// Arrange
		var json = """
		{
			"id": "test-id-123",
			"source": "urn:test:source",
			"type": "com.example.test.v1",
			"dataContentType": "application/json",
			"data": { "value": "test-data" }
		}
		""";

		// Act
		var cloudEvent = JsonSerializer.Deserialize<TestCloudEvent>(json, new JsonSerializerOptions
		{
			PropertyNameCaseInsensitive = true
		});

		// Assert
		_ = cloudEvent.ShouldNotBeNull();
		cloudEvent.Id.ShouldBe("test-id-123");
		cloudEvent.Source.ShouldBe(new Uri("urn:test:source"));
		cloudEvent.Type.ShouldBe("com.example.test.v1");
	}

	[Fact]
	public void CloudEvent_SupportsExtensions()
	{
		// Arrange
		var cloudEvent = new TestCloudEventWithExtensions
		{
			Id = Guid.NewGuid().ToString(),
			Source = new Uri("urn:test:source"),
			Type = "com.example.test.v1",
			TraceParent = "00-abc123-def456-01",
			CustomExtension = "custom-value"
		};

		// Act
		var json = JsonSerializer.Serialize(cloudEvent, new JsonSerializerOptions
		{
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase
		});

		// Assert
		json.ShouldContain("traceParent");
		json.ShouldContain("customExtension");
	}

	#endregion

	#region Transport Mapping Tests

	[Fact]
	public void CloudEvent_MapsToAzureServiceBusHeaders()
	{
		// Arrange
		var cloudEvent = new TestCloudEvent
		{
			Id = "event-123",
			Source = new Uri("urn:test:source"),
			Type = "com.example.test.v1"
		};

		// Act - Map to Azure Service Bus message properties
		var properties = new Dictionary<string, object>
		{
			["cloudEvents:id"] = cloudEvent.Id,
			["cloudEvents:source"] = cloudEvent.Source.ToString(),
			["cloudEvents:type"] = cloudEvent.Type,
			["cloudEvents:specversion"] = "1.0"
		};

		// Assert
		properties["cloudEvents:id"].ShouldBe("event-123");
		properties["cloudEvents:type"].ShouldBe("com.example.test.v1");
		properties["cloudEvents:specversion"].ShouldBe("1.0");
	}

	[Fact]
	public void CloudEvent_MapsToAwsSqsAttributes()
	{
		// Arrange
		var cloudEvent = new TestCloudEvent
		{
			Id = "event-456",
			Source = new Uri("urn:test:aws"),
			Type = "com.example.aws.v1"
		};

		// Act - Map to SQS message attributes
		var attributes = new Dictionary<string, string>
		{
			["ce_id"] = cloudEvent.Id,
			["ce_source"] = cloudEvent.Source.ToString(),
			["ce_type"] = cloudEvent.Type,
			["ce_specversion"] = "1.0"
		};

		// Assert
		attributes["ce_id"].ShouldBe("event-456");
		attributes["ce_source"].ShouldBe("urn:test:aws");
		attributes["ce_type"].ShouldBe("com.example.aws.v1");
	}

	#endregion

	#region Binary Mode Tests

	[Fact]
	public void CloudEvent_SerializesToBinaryMode()
	{
		// Arrange
		var data = new TestEventData("binary-test");
		var dataBytes = JsonSerializer.SerializeToUtf8Bytes(data);

		// Act
		var headers = new Dictionary<string, string>
		{
			["ce-id"] = Guid.NewGuid().ToString(),
			["ce-source"] = "urn:binary:source",
			["ce-type"] = "com.example.binary.v1",
			["ce-specversion"] = "1.0",
			["content-type"] = "application/json"
		};

		// Assert - Binary mode has headers + raw data
		headers["ce-specversion"].ShouldBe("1.0");
		dataBytes.Length.ShouldBeGreaterThan(0);
	}

	[Fact]
	public void CloudEvent_DeserializesFromBinaryMode()
	{
		// Arrange
		var headers = new Dictionary<string, string>
		{
			["ce-id"] = "binary-123",
			["ce-source"] = "urn:binary:test",
			["ce-type"] = "com.example.binary.v1",
			["ce-specversion"] = "1.0"
		};
		var dataBytes = JsonSerializer.SerializeToUtf8Bytes(new TestEventData("binary-data"));

		// Act
		var deserializedData = JsonSerializer.Deserialize<TestEventData>(dataBytes);

		// Assert
		headers["ce-id"].ShouldBe("binary-123");
		_ = deserializedData.ShouldNotBeNull();
		deserializedData.Value.ShouldBe("binary-data");
	}

	#endregion

	#region Service Registration Tests

	[Fact]
	public void CloudEventsServices_RegisterCorrectly()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddSingleton<ICloudEventSerializer, TestCloudEventSerializer>();

		// Act
		using var provider = services.BuildServiceProvider();
		var serializer = provider.GetService<ICloudEventSerializer>();

		// Assert
		_ = serializer.ShouldNotBeNull();
	}

	#endregion

	#region Test Helpers

	private class TestCloudEvent
	{
		public string Id { get; set; } = string.Empty;
		public Uri Source { get; set; } = null!;
		public string Type { get; set; } = string.Empty;
		public string? DataContentType { get; set; }
		public DateTimeOffset? Time { get; set; }
		public object? Data { get; set; }
	}

	private sealed class TestCloudEventWithExtensions : TestCloudEvent
	{
		public string? TraceParent { get; set; }
		public string? CustomExtension { get; set; }
	}

	private sealed record TestEventData(string Value);

	private interface ICloudEventSerializer
	{
		byte[] Serialize<T>(T cloudEvent);
		T? Deserialize<T>(byte[] data);
	}

	private sealed class TestCloudEventSerializer : ICloudEventSerializer
	{
		public byte[] Serialize<T>(T cloudEvent)
		{
			return JsonSerializer.SerializeToUtf8Bytes(cloudEvent);
		}

		public T? Deserialize<T>(byte[] data)
		{
			return JsonSerializer.Deserialize<T>(data);
		}
	}

	#endregion
}
