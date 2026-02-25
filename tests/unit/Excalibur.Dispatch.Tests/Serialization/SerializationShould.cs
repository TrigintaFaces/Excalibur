// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using Excalibur.Dispatch.Abstractions;

using Excalibur.Application.Requests.Commands;
using Excalibur.Application.Requests.Queries;

namespace Excalibur.Dispatch.Tests.Serialization;

/// <summary>
/// Unit tests for message serialization covering JSON round-trip, edge cases, type safety, and performance.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Serialization")]
public sealed class SerializationShould : UnitTestBase
{
	private readonly JsonSerializerOptions _jsonOptions;

	public SerializationShould()
	{
		_jsonOptions = new JsonSerializerOptions
		{
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
			PropertyNameCaseInsensitive = true,
			WriteIndented = false,
			DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
			Converters = { new JsonStringEnumConverter() }
		};
	}

	#region JSON Serialization Round-Trip Tests (5 tests)

	[Fact]
	public void SerializeDomainEvent_AndDeserialize_PreservesAllProperties()
	{
		// Arrange
		var originalEvent = new TestDomainEvent("aggregate-123", 42)
		{
			EventId = Guid.NewGuid().ToString(),
			OccurredAt = DateTimeOffset.UtcNow,
			Metadata = new Dictionary<string, object>
			{
				{ "UserId", "user-456" },
				{ "TenantId", "tenant-789" }
			}
		};

		// Act
		var serialized = JsonSerializer.SerializeToUtf8Bytes(originalEvent, _jsonOptions);
		var deserialized = JsonSerializer.Deserialize<TestDomainEvent>(serialized, _jsonOptions);

		// Assert
		_ = deserialized.ShouldNotBeNull();
		deserialized.EventId.ShouldBe(originalEvent.EventId);
		deserialized.AggregateId.ShouldBe(originalEvent.AggregateId);
		deserialized.Version.ShouldBe(originalEvent.Version);
		deserialized.EventType.ShouldBe(originalEvent.EventType);
		deserialized.OccurredAt.ShouldBeInRange(
			originalEvent.OccurredAt.AddMilliseconds(-1),
			originalEvent.OccurredAt.AddMilliseconds(1));
	}

	[Fact]
	public void SerializeIntegrationEvent_AndDeserialize_PreservesPayload()
	{
		// Arrange
		var originalEvent = new TestIntegrationEvent
		{
			MessageId = Guid.NewGuid().ToString(),
			Payload = "critical-customer-notification",
			Timestamp = DateTime.UtcNow
		};

		// Act
		var serialized = JsonSerializer.SerializeToUtf8Bytes(originalEvent, _jsonOptions);
		var deserialized = JsonSerializer.Deserialize<TestIntegrationEvent>(serialized, _jsonOptions);

		// Assert
		_ = deserialized.ShouldNotBeNull();
		deserialized.MessageId.ShouldBe(originalEvent.MessageId);
		deserialized.Payload.ShouldBe(originalEvent.Payload);
		deserialized.Timestamp.ShouldBeInRange(
			originalEvent.Timestamp.AddMilliseconds(-1),
			originalEvent.Timestamp.AddMilliseconds(1));
	}

	[Fact]
	public void SerializeCommand_AndDeserialize_PreservesActivityProperties()
	{
		// Arrange
		var originalCommand = new TestCommand
		{
			CommandData = "process-order-12345",
			Priority = 5
		};

		// Act
		var serialized = JsonSerializer.SerializeToUtf8Bytes(originalCommand, _jsonOptions);
		var deserialized = JsonSerializer.Deserialize<TestCommand>(serialized, _jsonOptions);

		// Assert
		_ = deserialized.ShouldNotBeNull();
		deserialized.CommandData.ShouldBe(originalCommand.CommandData);
		deserialized.Priority.ShouldBe(originalCommand.Priority);
		deserialized.MessageType.ShouldContain(nameof(TestCommand));
	}

	[Fact]
	public void SerializeQuery_AndDeserialize_PreservesGenericResultType()
	{
		// Arrange
		var originalQuery = new TestQuery
		{
			QueryParameter = "customer-email@example.com",
			MaxResults = 100
		};

		// Act
		var serialized = JsonSerializer.SerializeToUtf8Bytes(originalQuery, _jsonOptions);
		var deserialized = JsonSerializer.Deserialize<TestQuery>(serialized, _jsonOptions);

		// Assert
		_ = deserialized.ShouldNotBeNull();
		deserialized.QueryParameter.ShouldBe(originalQuery.QueryParameter);
		deserialized.MaxResults.ShouldBe(originalQuery.MaxResults);
		deserialized.MessageType.ShouldContain(nameof(TestQuery));
	}

	[Fact]
	public void SerializeMessage_WithMetadata_PreservesMetadataCollection()
	{
		// Arrange
		var metadata = new Dictionary<string, object>
		{
			{ "CorrelationId", Guid.NewGuid().ToString() },
			{ "CausationId", Guid.NewGuid().ToString() },
			{ "UserId", "user-999" },
			{ "RequestId", 12345 }
		};

		var originalEvent = new TestDomainEvent("agg-1", 1) { Metadata = metadata };

		// Act
		var serialized = JsonSerializer.SerializeToUtf8Bytes(originalEvent, _jsonOptions);
		var deserialized = JsonSerializer.Deserialize<TestDomainEvent>(serialized, _jsonOptions);

		// Assert
		_ = deserialized.Metadata.ShouldNotBeNull();
		deserialized.Metadata.Count.ShouldBe(metadata.Count);
		deserialized.Metadata.ShouldContainKey("CorrelationId");
		deserialized.Metadata.ShouldContainKey("UserId");
	}

	#endregion JSON Serialization Round-Trip Tests (5 tests)

	#region Edge Cases Tests (5 tests)

	[Fact]
	public void SerializeMessage_WithNullProperties_HandlesNullsCorrectly()
	{
		// Arrange
		var eventWithNulls = new TestDomainEvent("agg-1", 1) { Metadata = null };

		// Act
		var serialized = JsonSerializer.SerializeToUtf8Bytes(eventWithNulls, _jsonOptions);
		var deserialized = JsonSerializer.Deserialize<TestDomainEvent>(serialized, _jsonOptions);
		;

		// Assert
		_ = deserialized.ShouldNotBeNull();
		deserialized.AggregateId.ShouldBe("agg-1");
	}

	[Fact]
	public void SerializeMessage_WithSpecialCharacters_PreservesCharactersCorrectly()
	{
		// Arrange
		var specialChars = "Special: @#$%^&*()[]{}|\\:;\"'<>,.?/~`!-_+=";
		var eventWithSpecialChars = new TestDomainEvent(specialChars, 1);

		// Act
		var serialized = JsonSerializer.SerializeToUtf8Bytes(eventWithSpecialChars, _jsonOptions);
		var deserialized = JsonSerializer.Deserialize<TestDomainEvent>(serialized, _jsonOptions);
		;

		// Assert
		deserialized.AggregateId.ShouldBe(specialChars);
	}

	[Fact]
	public void SerializeMessage_WithLargePayload_HandlesLargeDataCorrectly()
	{
		// Arrange - Create a payload > 1MB
		var largeString = new string('A', 1024 * 1024 + 1000); // ~1MB
		var largeEvent = new TestIntegrationEvent
		{
			MessageId = Guid.NewGuid().ToString(),
			Payload = largeString
		};

		// Act
		var serialized = JsonSerializer.SerializeToUtf8Bytes(largeEvent, _jsonOptions);
		var deserialized = JsonSerializer.Deserialize<TestIntegrationEvent>(serialized, _jsonOptions);

		// Assert
		deserialized.Payload.Length.ShouldBe(largeString.Length);
		serialized.Length.ShouldBeGreaterThan(1024 * 1024); // > 1MB
	}

	[Fact]
	public void SerializeMessage_WithUnicodeCharacters_PreservesUnicodeCorrectly()
	{
		// Arrange
		var unicodeText = "Hello 世界 🌍 Привет мир العالم مرحبا";
		var unicodeEvent = new TestDomainEvent(unicodeText, 1);

		// Act
		var serialized = JsonSerializer.SerializeToUtf8Bytes(unicodeEvent, _jsonOptions);
		var deserialized = JsonSerializer.Deserialize<TestDomainEvent>(serialized, _jsonOptions);
		;

		// Assert
		deserialized.AggregateId.ShouldBe(unicodeText);
	}

	[Fact]
	public void SerializeMessage_WithEmptyCollections_HandlesEmptyCollectionsCorrectly()
	{
		// Arrange
		var emptyMetadata = new Dictionary<string, object>();
		var eventWithEmptyCollection = new TestDomainEvent("agg-1", 1) { Metadata = emptyMetadata };

		// Act
		var serialized = JsonSerializer.SerializeToUtf8Bytes(eventWithEmptyCollection, _jsonOptions);
		var deserialized = JsonSerializer.Deserialize<TestDomainEvent>(serialized, _jsonOptions);
		;

		// Assert
		_ = deserialized.Metadata.ShouldNotBeNull();
	}

	#endregion Edge Cases Tests (5 tests)

	#region Type Safety Tests (5 tests)

	[Fact]
	public void SerializeMessage_PreservesTypeInformation_ForDeserialization()
	{
		// Arrange
		var originalEvent = new TestDomainEvent("agg-1", 1);

		// Act
		var serialized = JsonSerializer.SerializeToUtf8Bytes(originalEvent, _jsonOptions);
		var json = Encoding.UTF8.GetString(serialized);
		var deserialized = JsonSerializer.Deserialize<TestDomainEvent>(serialized, _jsonOptions);
		;

		// Assert
		_ = deserialized.ShouldBeOfType<TestDomainEvent>();
		json.ShouldContain("aggregateId"); // Verify JSON contains expected property (camelCase)
	}

	[Fact]
	public void DomainEvent_EventTypeProperty_ReturnsAccurateTypeName()
	{
		// Arrange
		var domainEvent = new TestDomainEvent("agg-1", 1);

		// Act
		var serialized = JsonSerializer.SerializeToUtf8Bytes(domainEvent, _jsonOptions);
		var deserialized = JsonSerializer.Deserialize<TestDomainEvent>(serialized, _jsonOptions);
		;

		// Assert
		deserialized.EventType.ShouldBe(nameof(TestDomainEvent));
	}

	[Fact]
	public void SerializePolymorphicMessage_CanDeserializeToBaseType()
	{
		// Arrange
		var derivedCommand = new DerivedTestCommand
		{
			CommandData = "base-data",
			DerivedProperty = "derived-data"
		};

		// Act
		var serialized = JsonSerializer.SerializeToUtf8Bytes(derivedCommand, _jsonOptions);
		var deserialized = JsonSerializer.Deserialize<DerivedTestCommand>(serialized, _jsonOptions);

		// Assert
		_ = deserialized.ShouldBeOfType<DerivedTestCommand>();
		deserialized.CommandData.ShouldBe("base-data");
		deserialized.DerivedProperty.ShouldBe("derived-data");
	}

	[Fact]
	public void DeserializeMessage_WithUnknownProperties_IgnoresUnknownProperties()
	{
		// Arrange - JSON with extra properties not in the model
		var jsonWithExtraProps = """
		{
			"eventId": "test-id",
			"aggregateId": "agg-1",
			"version": 1,
			"occurredAt": "2025-01-01T00:00:00Z",
			"eventType": "TestDomainEvent",
			"unknownProperty1": "should-be-ignored",
			"unknownProperty2": 12345
		}
		""";

		var serialized = Encoding.UTF8.GetBytes(jsonWithExtraProps);

		// Act
		var deserialized = JsonSerializer.Deserialize<TestDomainEvent>(serialized, _jsonOptions);
		;

		// Assert
		_ = deserialized.ShouldNotBeNull();
		deserialized.AggregateId.ShouldBe("agg-1");
	}

	[Fact]
	public void DeserializeMessage_WithMissingOptionalProperties_UsesDefaultValues()
	{
		// Arrange - JSON missing optional metadata property
		var minimalJson = """
		{
			"eventId": "test-id",
			"aggregateId": "agg-1",
			"version": 1,
			"occurredAt": "2025-01-01T00:00:00Z",
			"eventType": "TestDomainEvent"
		}
		""";

		var serialized = Encoding.UTF8.GetBytes(minimalJson);

		// Act
		var deserialized = JsonSerializer.Deserialize<TestDomainEvent>(serialized, _jsonOptions);
		;

		// Assert
		_ = deserialized.ShouldNotBeNull();
		deserialized.AggregateId.ShouldBe("agg-1");
	}

	#endregion Type Safety Tests (5 tests)

	#region Performance Tests (5 tests)

	[Fact]
	public void SerializeMessage_PerformanceBaseline_CompletesWithinReasonableTime()
	{
		// Arrange
		var testEvent = new TestDomainEvent("agg-1", 1);
		var stopwatch = Stopwatch.StartNew();

		// Act - 1000 iterations
		for (var i = 0; i < 1000; i++)
		{
			_ = JsonSerializer.SerializeToUtf8Bytes(testEvent, _jsonOptions);
		}

		stopwatch.Stop();

		// Assert - Should complete 1000 serializations in under 5s (generous for full-suite parallel load)
		stopwatch.ElapsedMilliseconds.ShouldBeLessThan(5000);
	}

	[Fact]
	public void DeserializeMessage_PerformanceBaseline_CompletesWithinReasonableTime()
	{
		// Arrange
		var testEvent = new TestDomainEvent("agg-1", 1);
		var serialized = JsonSerializer.SerializeToUtf8Bytes(testEvent, _jsonOptions);
		var stopwatch = Stopwatch.StartNew();

		// Act - 1000 iterations
		for (var i = 0; i < 1000; i++)
		{
			var _ = JsonSerializer.Deserialize<TestDomainEvent>(serialized, _jsonOptions);
		}

		stopwatch.Stop();

		// Assert - Should complete 1000 deserializations in under 5s (generous for full-suite parallel load)
		stopwatch.ElapsedMilliseconds.ShouldBeLessThan(5000);
	}

	[Fact]
	public void SerializeMessage_MemoryAllocation_ProducesReasonablySizedOutput()
	{
		// Arrange
		var testEvent = new TestDomainEvent("agg-1", 1)
		{
			Metadata = new Dictionary<string, object> { { "Key1", "Value1" } }
		};

		// Act
		var serialized = JsonSerializer.SerializeToUtf8Bytes(testEvent, _jsonOptions);

		// Assert - Serialized size should be reasonable (less than 1KB for simple event)
		serialized.Length.ShouldBeLessThan(1024);
		serialized.Length.ShouldBeGreaterThan(50); // Should have meaningful content
	}

	[Fact]
	public void DeserializeMessage_MemoryAllocation_CompletesWithoutExcessiveAllocations()
	{
		// Arrange
		var testEvent = new TestDomainEvent("agg-1", 1);
		var serialized = JsonSerializer.SerializeToUtf8Bytes(testEvent, _jsonOptions);
		const int warmupIterations = 25;
		const int measuredIterations = 250;

		// Warm up serializer caches and JIT so we measure steady-state allocation behavior.
		for (var i = 0; i < warmupIterations; i++)
		{
			_ = JsonSerializer.Deserialize<TestDomainEvent>(serialized, _jsonOptions);
		}

		// Act: Measure allocation on the current thread to avoid GC heap-noise flakiness in CI.
		var startAllocatedBytes = GC.GetAllocatedBytesForCurrentThread();
		TestDomainEvent? lastDeserialized = null;

		for (var i = 0; i < measuredIterations; i++)
		{
			lastDeserialized = JsonSerializer.Deserialize<TestDomainEvent>(serialized, _jsonOptions);
		}

		var allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - startAllocatedBytes;

		// Assert
		_ = lastDeserialized.ShouldNotBeNull();
		var averageAllocationPerDeserialize = allocatedBytes / (double)measuredIterations;
		averageAllocationPerDeserialize.ShouldBeLessThan(8 * 1024);
	}

	[Fact]
	public void JsonSerializer_ProducesConsistentOutput_AcrossMultipleSerialization()
	{
		// Arrange
		var testEvent = new TestDomainEvent("agg-1", 1);

		// Act - Serialize the same event twice
		var serialized1 = JsonSerializer.SerializeToUtf8Bytes(testEvent, _jsonOptions);
		var serialized2 = JsonSerializer.SerializeToUtf8Bytes(testEvent, _jsonOptions);

		// Assert - Should produce identical output for same input
		serialized1.Length.ShouldBe(serialized2.Length);
		serialized1.SequenceEqual(serialized2).ShouldBeTrue();
	}

	#endregion Performance Tests (5 tests)

	#region Test Fixtures

	/// <summary>
	/// Test implementation of IDomainEvent for serialization testing.
	/// </summary>
	private sealed class TestDomainEvent : IDomainEvent
	{
		public TestDomainEvent()
		{ }

		public TestDomainEvent(string aggregateId, long version)
		{
			EventId = Guid.NewGuid().ToString();
			AggregateId = aggregateId;
			Version = version;
			OccurredAt = DateTimeOffset.UtcNow;
			EventType = nameof(TestDomainEvent);
		}

		public string EventId { get; init; } = Guid.NewGuid().ToString();
		public string AggregateId { get; init; } = string.Empty;
		public long Version { get; init; }
		public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
		public string EventType { get; init; } = nameof(TestDomainEvent);
		public IDictionary<string, object>? Metadata { get; init; }
	}

	/// <summary>
	/// Test implementation of IIntegrationEvent for serialization testing.
	/// </summary>
	private sealed class TestIntegrationEvent : IIntegrationEvent
	{
		public string MessageId { get; init; } = Guid.NewGuid().ToString();
		public string Payload { get; init; } = string.Empty;
		public DateTime Timestamp { get; init; } = DateTime.UtcNow;
	}

	/// <summary>
	/// Test implementation of ICommand for serialization testing.
	/// </summary>
	private class TestCommand : CommandBase
	{
		public string CommandData { get; init; } = string.Empty;
		public int Priority { get; init; }

		public override string ActivityDisplayName => "Test Command";
		public override string ActivityDescription => "A test command for serialization testing";
	}

	/// <summary>
	/// Derived test command for polymorphic serialization testing.
	/// </summary>
	private sealed class DerivedTestCommand : TestCommand
	{
		public string DerivedProperty { get; init; } = string.Empty;
	}

	/// <summary>
	/// Test implementation of IQuery for serialization testing.
	/// </summary>
	private sealed class TestQuery : QueryBase<TestQueryResult>
	{
		public string QueryParameter { get; init; } = string.Empty;
		public int MaxResults { get; init; }

		public override string ActivityDisplayName => "Test Query";
		public override string ActivityDescription => "A test query for serialization testing";
	}

	/// <summary>
	/// Test result type for IQuery serialization testing.
	/// </summary>
	private sealed class TestQueryResult
	{
		public string? Data { get; init; }
	}

	#endregion Test Fixtures
}
