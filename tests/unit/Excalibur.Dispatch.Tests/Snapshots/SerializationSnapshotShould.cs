// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Tests.Snapshots;

/// <summary>
/// Tests that verify the JSON serialization format of core message types
/// remains stable across changes. Uses direct JSON assertions instead of
/// Verify snapshots to avoid interference from global scrubbing settings.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Serialization")]
public sealed class SerializationSnapshotShould : UnitTestBase
{
	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		WriteIndented = true,
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
		Converters = { new JsonStringEnumConverter() }
	};

	[Fact]
	public void SerializeTransportMessageWithExpectedJsonFormat()
	{
		// Arrange
		var message = new TransportMessage
		{
			Id = "msg-00000000-0000-0000-0000-000000000001",
			Body = Encoding.UTF8.GetBytes("""{"orderId":"order-42","amount":99.95}"""),
			ContentType = "application/json",
			MessageType = "OrderPlaced",
			CorrelationId = "corr-00000000-0000-0000-0000-000000000001",
			Subject = "orders",
			TimeToLive = TimeSpan.FromMinutes(30),
			CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
			Properties =
			{
				["partitionKey"] = "tenant-001",
				["orderingKey"] = "order-42"
			}
		};

		// Act
		var json = JsonSerializer.Serialize(message, JsonOptions);
		var doc = JsonDocument.Parse(json);
		var root = doc.RootElement;

		// Assert - Verify key fields are present and correctly serialized
		root.GetProperty("id").GetString().ShouldBe("msg-00000000-0000-0000-0000-000000000001");
		root.GetProperty("contentType").GetString().ShouldBe("application/json");
		root.GetProperty("messageType").GetString().ShouldBe("OrderPlaced");
		root.GetProperty("correlationId").GetString().ShouldBe("corr-00000000-0000-0000-0000-000000000001");
		root.GetProperty("subject").GetString().ShouldBe("orders");
		root.GetProperty("createdAt").GetString().ShouldBe("2026-01-01T00:00:00+00:00");
		root.GetProperty("properties").GetProperty("partitionKey").GetString().ShouldBe("tenant-001");
		root.GetProperty("properties").GetProperty("orderingKey").GetString().ShouldBe("order-42");

		// Body should be base64-encoded
		root.GetProperty("body").GetString().ShouldNotBeNullOrEmpty();
	}

	[Fact]
	public void SerializeDomainEventWithExpectedJsonFormat()
	{
		// Arrange
		var domainEvent = new SnapshotTestDomainEvent
		{
			EventId = "evt-00000000-0000-0000-0000-000000000001",
			AggregateId = "order-42",
			Version = 3,
			OccurredAt = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero),
			EventType = "OrderPlaced",
			Metadata = new Dictionary<string, object>
			{
				["UserId"] = "user-001",
				["TenantId"] = "tenant-001",
				["CorrelationId"] = "corr-00000000-0000-0000-0000-000000000001"
			}
		};

		// Act
		var json = JsonSerializer.Serialize(domainEvent, JsonOptions);
		var doc = JsonDocument.Parse(json);
		var root = doc.RootElement;

		// Assert - Verify key fields
		root.GetProperty("eventId").GetString().ShouldBe("evt-00000000-0000-0000-0000-000000000001");
		root.GetProperty("aggregateId").GetString().ShouldBe("order-42");
		root.GetProperty("version").GetInt64().ShouldBe(3);
		root.GetProperty("occurredAt").GetString().ShouldBe("2026-01-01T12:00:00+00:00");
		root.GetProperty("eventType").GetString().ShouldBe("OrderPlaced");

		// Verify metadata
		var metadata = root.GetProperty("metadata");
		metadata.GetProperty("UserId").GetString().ShouldBe("user-001");
		metadata.GetProperty("TenantId").GetString().ShouldBe("tenant-001");
		metadata.GetProperty("CorrelationId").GetString().ShouldBe("corr-00000000-0000-0000-0000-000000000001");
	}

	[Fact]
	public void SerializeMinimalTransportMessageWithExpectedJsonFormat()
	{
		// Arrange - Verify the minimal representation (no optional fields)
		var message = new TransportMessage
		{
			Id = "msg-00000000-0000-0000-0000-000000000002",
			Body = Encoding.UTF8.GetBytes("hello"),
			CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
		};

		// Act
		var json = JsonSerializer.Serialize(message, JsonOptions);
		var doc = JsonDocument.Parse(json);
		var root = doc.RootElement;

		// Assert - Required fields present
		root.GetProperty("id").GetString().ShouldBe("msg-00000000-0000-0000-0000-000000000002");
		root.GetProperty("createdAt").GetString().ShouldBe("2026-01-01T00:00:00+00:00");
		root.GetProperty("body").GetString().ShouldNotBeNullOrEmpty();

		// Optional fields should be absent (WhenWritingNull)
		root.TryGetProperty("contentType", out _).ShouldBeFalse();
		root.TryGetProperty("messageType", out _).ShouldBeFalse();
		root.TryGetProperty("correlationId", out _).ShouldBeFalse();
		root.TryGetProperty("subject", out _).ShouldBeFalse();
	}

	/// <summary>
	/// Concrete IDomainEvent implementation for snapshot testing.
	/// Uses fixed values to produce deterministic snapshots.
	/// </summary>
	private sealed class SnapshotTestDomainEvent : IDomainEvent
	{
		public string EventId { get; init; } = string.Empty;
		public string AggregateId { get; init; } = string.Empty;
		public long Version { get; init; }
		public DateTimeOffset OccurredAt { get; init; }
		public string EventType { get; init; } = string.Empty;
		public IDictionary<string, object>? Metadata { get; init; }
	}
}
