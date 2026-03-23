// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Tests.ContextValues;

/// <summary>
/// Context propagation tests across transport boundaries.
/// Validates that CorrelationId, CausationId, TraceParent, and TenantId
/// survive serialize/deserialize round-trips through MessageEnvelope.
/// </summary>
/// <remarks>
/// Sprint 693, Task T.4 (bd-j954v): Closes the gap where no test validates
/// context value survival through transport serialization boundaries.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ContextPropagationShould
{
	private static readonly JsonSerializerOptions SerializerOptions = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
		PropertyNameCaseInsensitive = true,
	};

	#region Round-Trip: Context Values Survive Serialization

	[Fact]
	public void PreserveCorrelationId_WhenRoundTripping()
	{
		// Arrange
		var original = new MessageEnvelope
		{
			CorrelationId = "corr-preserve-test-12345",
		};

		// Act
		var json = JsonSerializer.Serialize(original, SerializerOptions);
		var deserialized = JsonSerializer.Deserialize<MessageEnvelope>(json, SerializerOptions)!;

		// Assert
		deserialized.CorrelationId.ShouldBe("corr-preserve-test-12345");
	}

	[Fact]
	public void PreserveCausationId_WhenRoundTripping()
	{
		// Arrange
		var original = new MessageEnvelope
		{
			CausationId = "cause-abc-def-456",
		};

		// Act
		var json = JsonSerializer.Serialize(original, SerializerOptions);
		var deserialized = JsonSerializer.Deserialize<MessageEnvelope>(json, SerializerOptions)!;

		// Assert
		deserialized.CausationId.ShouldBe("cause-abc-def-456");
	}

	[Fact]
	public void PreserveTraceParent_WhenRoundTripping()
	{
		// Arrange - W3C Trace Context format
		const string traceParent = "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01";
		var original = new MessageEnvelope
		{
			TraceParent = traceParent,
		};

		// Act
		var json = JsonSerializer.Serialize(original, SerializerOptions);
		var deserialized = JsonSerializer.Deserialize<MessageEnvelope>(json, SerializerOptions)!;

		// Assert
		deserialized.TraceParent.ShouldBe(traceParent);
	}

	[Fact]
	public void PreserveTenantId_WhenRoundTripping()
	{
		// Arrange
		var original = new MessageEnvelope
		{
			TenantId = "tenant-acme-corp-789",
		};

		// Act
		var json = JsonSerializer.Serialize(original, SerializerOptions);
		var deserialized = JsonSerializer.Deserialize<MessageEnvelope>(json, SerializerOptions)!;

		// Assert
		deserialized.TenantId.ShouldBe("tenant-acme-corp-789");
	}

	[Fact]
	public void PreserveAllContextValues_WhenRoundTripping()
	{
		// Arrange - Full context with all propagation-relevant fields
		var original = new MessageEnvelope
		{
			MessageId = "msg-full-context-001",
			CorrelationId = "corr-full-001",
			CausationId = "cause-full-001",
			TraceParent = "00-12345678901234567890123456789012-1234567890123456-01",
			TenantId = "tenant-full-001",
			UserId = "user-full-001",
			Source = "service-a",
			MessageType = "OrderCreated",
		};

		// Act
		var json = JsonSerializer.Serialize(original, SerializerOptions);
		var deserialized = JsonSerializer.Deserialize<MessageEnvelope>(json, SerializerOptions)!;

		// Assert - ALL context values must survive
		deserialized.MessageId.ShouldBe("msg-full-context-001");
		deserialized.CorrelationId.ShouldBe("corr-full-001");
		deserialized.CausationId.ShouldBe("cause-full-001");
		deserialized.TraceParent.ShouldBe("00-12345678901234567890123456789012-1234567890123456-01");
		deserialized.TenantId.ShouldBe("tenant-full-001");
		deserialized.UserId.ShouldBe("user-full-001");
		deserialized.Source.ShouldBe("service-a");
		deserialized.MessageType.ShouldBe("OrderCreated");
	}

	#endregion

	#region JSON Contract: Property Names Are Stable

	[Fact]
	public void ProduceCorrectJsonPropertyNames_ForContextValues()
	{
		// Arrange
		var envelope = new MessageEnvelope
		{
			MessageId = "msg-json-keys",
			CorrelationId = "corr-json-keys",
			CausationId = "cause-json-keys",
			TraceParent = "00-trace-json-keys",
			TenantId = "tenant-json-keys",
		};

		// Act
		var json = JsonSerializer.Serialize(envelope, SerializerOptions);
		using var doc = JsonDocument.Parse(json);
		var root = doc.RootElement;

		// Assert - Verify exact camelCase JSON key names (contract stability)
		root.TryGetProperty("messageId", out _).ShouldBeTrue("Missing 'messageId' key");
		root.TryGetProperty("correlationId", out _).ShouldBeTrue("Missing 'correlationId' key");
		root.TryGetProperty("causationId", out _).ShouldBeTrue("Missing 'causationId' key");
		root.TryGetProperty("traceParent", out _).ShouldBeTrue("Missing 'traceParent' key");
		root.TryGetProperty("tenantId", out _).ShouldBeTrue("Missing 'tenantId' key");
	}

	#endregion

	#region Backward Compatibility: Deserialize From Fixed JSON

	[Fact]
	public void DeserializeContextValues_FromFixedJson()
	{
		// Arrange - Simulates JSON payload from another service or persisted state
		var fixedJson = """
			{
				"messageId": "msg-external-001",
				"correlationId": "corr-external-001",
				"causationId": "cause-external-001",
				"traceParent": "00-abcdef1234567890abcdef1234567890-abcdef1234567890-01",
				"tenantId": "tenant-external-001",
				"userId": "user-external-001",
				"source": "external-service"
			}
			""";

		// Act
		var deserialized = JsonSerializer.Deserialize<MessageEnvelope>(fixedJson, SerializerOptions)!;

		// Assert
		deserialized.MessageId.ShouldBe("msg-external-001");
		deserialized.CorrelationId.ShouldBe("corr-external-001");
		deserialized.CausationId.ShouldBe("cause-external-001");
		deserialized.TraceParent.ShouldBe("00-abcdef1234567890abcdef1234567890-abcdef1234567890-01");
		deserialized.TenantId.ShouldBe("tenant-external-001");
		deserialized.UserId.ShouldBe("user-external-001");
		deserialized.Source.ShouldBe("external-service");
	}

	#endregion

	#region Edge Cases: Null and Empty Context Values

	[Fact]
	public void HandleNullContextValues_WhenRoundTripping()
	{
		// Arrange - No context values set
		var original = new MessageEnvelope();
		original.CorrelationId = null;
		original.CausationId = null;
		original.TraceParent = null;
		original.TenantId = null;

		// Act
		var json = JsonSerializer.Serialize(original, SerializerOptions);
		var deserialized = JsonSerializer.Deserialize<MessageEnvelope>(json, SerializerOptions)!;

		// Assert - Null values should remain null
		deserialized.CorrelationId.ShouldBeNull();
		deserialized.CausationId.ShouldBeNull();
		deserialized.TraceParent.ShouldBeNull();
		deserialized.TenantId.ShouldBeNull();
	}

	[Fact]
	public void HandleEmptyStringContextValues_WhenRoundTripping()
	{
		// Arrange
		var original = new MessageEnvelope
		{
			CorrelationId = string.Empty,
			CausationId = string.Empty,
			TraceParent = string.Empty,
			TenantId = string.Empty,
		};

		// Act
		var json = JsonSerializer.Serialize(original, SerializerOptions);
		var deserialized = JsonSerializer.Deserialize<MessageEnvelope>(json, SerializerOptions)!;

		// Assert
		deserialized.CorrelationId.ShouldBe(string.Empty);
		deserialized.CausationId.ShouldBe(string.Empty);
		deserialized.TraceParent.ShouldBe(string.Empty);
		deserialized.TenantId.ShouldBe(string.Empty);
	}

	#endregion

	#region Headers (Non-Serialized, Runtime Only)

	[Fact]
	public void SupportRuntimeHeaders_WithoutSerialization()
	{
		// Arrange - Headers are a runtime-only ConcurrentDictionary, not serialized
		var envelope = new MessageEnvelope();

		// Act
		envelope.Headers["X-Custom-Header"] = "custom-value";
		envelope.Headers["Authorization"] = "Bearer token123";

		// Assert - Headers are accessible at runtime
		envelope.Headers.Count.ShouldBe(2);
		envelope.Headers["X-Custom-Header"].ShouldBe("custom-value");
		envelope.Headers["Authorization"].ShouldBe("Bearer token123");
	}

	[Fact]
	public void IsolateHeaders_BetweenEnvelopeInstances()
	{
		// Arrange
		var envelope1 = new MessageEnvelope();
		var envelope2 = new MessageEnvelope();

		// Act
		envelope1.Headers["key"] = "value-1";
		envelope2.Headers["key"] = "value-2";

		// Assert
		envelope1.Headers["key"].ShouldBe("value-1");
		envelope2.Headers["key"].ShouldBe("value-2");
	}

	#endregion

	#region Context Isolation per MessageEnvelope Instance

	[Fact]
	public void IsolateContextBetweenEnvelopeInstances()
	{
		// Arrange
		var envelope1 = new MessageEnvelope
		{
			MessageId = "msg-1",
			CorrelationId = "corr-1",
			TenantId = "tenant-1",
		};

		var envelope2 = new MessageEnvelope
		{
			MessageId = "msg-2",
			CorrelationId = "corr-2",
			TenantId = "tenant-2",
		};

		// Assert - Different instances have different values
		envelope1.MessageId.ShouldNotBe(envelope2.MessageId);
		envelope1.CorrelationId.ShouldNotBe(envelope2.CorrelationId);
		envelope1.TenantId.ShouldNotBe(envelope2.TenantId);

		// Act - Modifying one doesn't affect the other
		envelope1.CorrelationId = "modified";
		envelope2.CorrelationId.ShouldBe("corr-2");
	}

	[Fact]
	public void IsolateItemsBetweenEnvelopeInstances()
	{
		// Arrange
		var envelope1 = new MessageEnvelope();
		var envelope2 = new MessageEnvelope();

		// Act
		envelope1.Items["key"] = "value-1";
		envelope2.Items["key"] = "value-2";

		// Assert - Items are isolated
		envelope1.Items["key"].ShouldBe("value-1");
		envelope2.Items["key"].ShouldBe("value-2");
	}

	#endregion

	#region CorrelationId Type Round-Trip

	[Fact]
	public void CreateCorrelationId_FromGuid()
	{
		// Arrange
		var guid = Guid.Parse("12345678-1234-1234-1234-123456789012");

		// Act
		var corrId = new CorrelationId(guid);

		// Assert
		corrId.Value.ShouldBe(guid);
		corrId.ToString().ShouldBe("12345678-1234-1234-1234-123456789012");
	}

	[Fact]
	public void CreateCorrelationId_FromString()
	{
		// Arrange
		const string guidStr = "abcdef01-2345-6789-abcd-ef0123456789";

		// Act
		var corrId = new CorrelationId(guidStr);

		// Assert
		corrId.Value.ShouldBe(Guid.Parse(guidStr));
	}

	[Fact]
	public void ThrowOnNullOrWhiteSpace_WhenCreatingCorrelationIdFromString()
	{
		Should.Throw<ArgumentException>(() => new CorrelationId((string?)null));
		Should.Throw<ArgumentException>(() => new CorrelationId(string.Empty));
		Should.Throw<ArgumentException>(() => new CorrelationId("   "));
	}

	[Fact]
	public void ThrowOnInvalidGuidString_WhenCreatingCorrelationId()
	{
		Should.Throw<FormatException>(() => new CorrelationId("not-a-guid"));
	}

	[Fact]
	public void SupportEquality_ForCorrelationId()
	{
		// Arrange
		var guid = Guid.NewGuid();
		var corrId1 = new CorrelationId(guid);
		var corrId2 = new CorrelationId(guid);
		var corrId3 = new CorrelationId(Guid.NewGuid());

		// Assert
		corrId1.Equals(corrId2).ShouldBeTrue();
		corrId1.Equals(corrId3).ShouldBeFalse();
		corrId1.GetHashCode().ShouldBe(corrId2.GetHashCode());
		corrId1.Equals((object)corrId2).ShouldBeTrue();
		corrId1.Equals(null).ShouldBeFalse();
	}

	#endregion
}
