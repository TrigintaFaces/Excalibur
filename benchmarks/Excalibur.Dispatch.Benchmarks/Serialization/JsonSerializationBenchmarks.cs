// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// Licensed under MIT. See LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Serialization;
using Excalibur.Dispatch.Options.Core;

namespace Excalibur.Dispatch.Benchmarks.Serialization;

/// <summary>
/// Benchmarks for System.Text.Json serialization performance.
/// Measures JSON serialization and deserialization performance across different message sizes.
/// </summary>
/// <remarks>
/// Sprint 185 - Performance Benchmarks Enhancement.
/// bd-n237x: Serialization Performance Benchmarks (10 scenarios).
///
/// Performance Targets (from testing-and-benchmarking-strategy-spec.md):
/// - Small message (&lt; 1KB): &lt; 50μs serialize, &lt; 75μs deserialize (P50)
/// - Medium message (1-10KB): &lt; 200μs serialize, &lt; 300μs deserialize (P50)
/// - Large message (&gt; 10KB): &lt; 1ms serialize, &lt; 1.5ms deserialize (P50)
/// - Polymorphic: &lt; 100μs serialize, &lt; 150μs deserialize (P50)
/// </remarks>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.HostProcess)]
public class JsonSerializationBenchmarks
{
	private IMessageSerializer? _serializer;
	private SmallMessage? _smallMessage;
	private MediumMessage? _mediumMessage;
	private LargeMessage? _largeMessage;
	private PolymorphicMessage? _polymorphicMessage;
	private byte[]? _smallMessageBytes;
	private byte[]? _mediumMessageBytes;
	private byte[]? _largeMessageBytes;
	private byte[]? _polymorphicMessageBytes;

	/// <summary>
	/// Initialize serializer and test messages before benchmarks.
	/// </summary>
	[GlobalSetup]
	public void GlobalSetup()
	{
		// Create JSON serializer
		var options = Microsoft.Extensions.Options.Options.Create(new JsonSerializationOptions
		{
			MaxDepth = 64,
			PreserveReferences = false,
		});
		_serializer = new SystemTextJsonMessageSerializer(options);

		// Create test messages
		_smallMessage = CreateSmallMessage();
		_mediumMessage = CreateMediumMessage();
		_largeMessage = CreateLargeMessage();
		_polymorphicMessage = CreatePolymorphicMessage();

		// Pre-serialize for deserialization benchmarks
		_smallMessageBytes = _serializer.Serialize(_smallMessage);
		_mediumMessageBytes = _serializer.Serialize(_mediumMessage);
		_largeMessageBytes = _serializer.Serialize(_largeMessage);
		_polymorphicMessageBytes = _serializer.Serialize(_polymorphicMessage);
	}

	/// <summary>
	/// Benchmark: Serialize small message (&lt; 1KB).
	/// </summary>
	[Benchmark(Baseline = true)]
	public byte[] SerializeSmallMessage()
	{
		return _serializer.Serialize(_smallMessage);
	}

	/// <summary>
	/// Benchmark: Deserialize small message (&lt; 1KB).
	/// </summary>
	[Benchmark]
	public SmallMessage DeserializeSmallMessage()
	{
		return _serializer.Deserialize<SmallMessage>(_smallMessageBytes);
	}

	/// <summary>
	/// Benchmark: Serialize medium message (1-10KB).
	/// </summary>
	[Benchmark]
	public byte[] SerializeMediumMessage()
	{
		return _serializer.Serialize(_mediumMessage);
	}

	/// <summary>
	/// Benchmark: Deserialize medium message (1-10KB).
	/// </summary>
	[Benchmark]
	public MediumMessage DeserializeMediumMessage()
	{
		return _serializer.Deserialize<MediumMessage>(_mediumMessageBytes);
	}

	/// <summary>
	/// Benchmark: Serialize large message (&gt; 10KB).
	/// </summary>
	[Benchmark]
	public byte[] SerializeLargeMessage()
	{
		return _serializer.Serialize(_largeMessage);
	}

	/// <summary>
	/// Benchmark: Deserialize large message (&gt; 10KB).
	/// </summary>
	[Benchmark]
	public LargeMessage DeserializeLargeMessage()
	{
		return _serializer.Deserialize<LargeMessage>(_largeMessageBytes);
	}

	/// <summary>
	/// Benchmark: Round-trip (serialize + deserialize) small message.
	/// </summary>
	[Benchmark]
	public SmallMessage RoundTripSmallMessage()
	{
		var bytes = _serializer.Serialize(_smallMessage);
		return _serializer.Deserialize<SmallMessage>(bytes);
	}

	/// <summary>
	/// Benchmark: Round-trip (serialize + deserialize) medium message.
	/// </summary>
	[Benchmark]
	public MediumMessage RoundTripMediumMessage()
	{
		var bytes = _serializer.Serialize(_mediumMessage);
		return _serializer.Deserialize<MediumMessage>(bytes);
	}

	/// <summary>
	/// Benchmark: Round-trip (serialize + deserialize) large message.
	/// </summary>
	[Benchmark]
	public LargeMessage RoundTripLargeMessage()
	{
		var bytes = _serializer.Serialize(_largeMessage);
		return _serializer.Deserialize<LargeMessage>(bytes);
	}

	// ========================================================================
	// Sprint 185 - New Benchmark Scenario (bd-n237x)
	// ========================================================================

	/// <summary>
	/// Benchmark: Serialize polymorphic message with derived types.
	/// Tests System.Text.Json polymorphic serialization with discriminator.
	/// </summary>
	[Benchmark(Description = "Serialize Polymorphic Message")]
	public byte[] SerializePolymorphicMessage()
	{
		return _serializer.Serialize(_polymorphicMessage);
	}

	/// <summary>
	/// Benchmark: Deserialize polymorphic message with derived types.
	/// Tests System.Text.Json polymorphic deserialization with discriminator.
	/// </summary>
	[Benchmark(Description = "Deserialize Polymorphic Message")]
	public PolymorphicMessage DeserializePolymorphicMessage()
	{
		return _serializer.Deserialize<PolymorphicMessage>(_polymorphicMessageBytes);
	}

	// Test Message Factories

	private static SmallMessage CreateSmallMessage()
	{
		return new SmallMessage
		{
			Id = Guid.NewGuid().ToString(),
			Name = "TestEvent",
			Count = 42,
			Timestamp = DateTimeOffset.UtcNow,
		};
	}

	private static MediumMessage CreateMediumMessage()
	{
		return new MediumMessage
		{
			OrderId = Guid.NewGuid().ToString(),
			CustomerId = Guid.NewGuid().ToString(),
			Items = [.. Enumerable.Range(0, 10).Select(i => new OrderItem
			{
				ProductId = $"PROD-{i:D5}",
				ProductName = $"Product {i} with a longer descriptive name",
				Quantity = i + 1,
				UnitPrice = 19.99m + i,
				TotalPrice = (19.99m + i) * (i + 1),
			})],
			ShippingAddress = new Address
			{
				Street = "123 Main St, Apt 4B",
				City = "San Francisco",
				State = "CA",
				PostalCode = "94102",
				Country = "United States",
			},
			BillingAddress = new Address
			{
				Street = "456 Oak Ave, Suite 200",
				City = "San Francisco",
				State = "CA",
				PostalCode = "94103",
				Country = "United States",
			},
			Metadata = Enumerable.Range(0, 20).ToDictionary(
				i => $"key-{i}",
				i => $"value-{i}-with-some-additional-data"),
			OrderDate = DateTimeOffset.UtcNow,
			TotalAmount = 1234.56m,
		};
	}

	private static LargeMessage CreateLargeMessage()
	{
		var largeData = new string('X', 1024); // 1KB of data per item

		return new LargeMessage
		{
			BatchId = Guid.NewGuid().ToString(),
			Items = [.. Enumerable.Range(0, 50).Select(i => new BatchItem
			{
				Id = Guid.NewGuid().ToString(),
				Type = $"Type-{i % 5}",
				Data = largeData,
				Properties = Enumerable.Range(0, 10).ToDictionary(
					j => $"prop-{j}",
					j => $"value-{j}"),
				CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-i),
			})],
			AuditTrail = [.. Enumerable.Range(0, 100).Select(i => new AuditEntry
			{
				Action = $"Action-{i % 10}",
				User = $"user-{i % 5}@example.com",
				Details = $"Detailed description of action {i} with additional context and information",
				Timestamp = DateTimeOffset.UtcNow.AddSeconds(-i),
			})],
			ExtensionData = Enumerable.Range(0, 50).ToDictionary(
				i => $"extension-{i}",
				i => (object)$"extension-value-{i}"),
			ProcessedAt = DateTimeOffset.UtcNow,
		};
	}

	private static PolymorphicMessage CreatePolymorphicMessage()
	{
		return new PolymorphicMessage
		{
			Id = Guid.NewGuid().ToString(),
			Events =
			[
				new UserCreatedEvent
				{
					UserId = Guid.NewGuid().ToString(),
					Username = "testuser",
					Email = "test@example.com",
					CreatedAt = DateTimeOffset.UtcNow,
				},
				new UserUpdatedEvent
				{
					UserId = Guid.NewGuid().ToString(),
					ChangedProperties = new Dictionary<string, string>
					{
						["Email"] = "new@example.com",
						["DisplayName"] = "New Display Name",
					},
					UpdatedAt = DateTimeOffset.UtcNow,
				},
				new UserDeletedEvent
				{
					UserId = Guid.NewGuid().ToString(),
					Reason = "Account closure requested",
					DeletedAt = DateTimeOffset.UtcNow,
				},
				new UserCreatedEvent
				{
					UserId = Guid.NewGuid().ToString(),
					Username = "anotheruser",
					Email = "another@example.com",
					CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
				},
				new UserUpdatedEvent
				{
					UserId = Guid.NewGuid().ToString(),
					ChangedProperties = new Dictionary<string, string>
					{
						["Phone"] = "+1234567890",
					},
					UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
				},
			],
			Timestamp = DateTimeOffset.UtcNow,
		};
	}
}

// Test Messages

#pragma warning disable SA1402 // File may only contain a single type

/// <summary>
/// Small message (&lt; 1KB) - simple event with minimal payload.
/// </summary>
public record SmallMessage : IDispatchEvent
{
	public string Id { get; init; } = string.Empty;
	public string Name { get; init; } = string.Empty;
	public int Count { get; init; }
	public DateTimeOffset Timestamp { get; init; }
}

/// <summary>
/// Medium message (1-10KB) - event with moderate payload (addresses, metadata).
/// </summary>
public record MediumMessage : IDispatchEvent
{
	public string OrderId { get; init; } = string.Empty;
	public string CustomerId { get; init; } = string.Empty;
	public List<OrderItem> Items { get; init; } = new();
	public Address ShippingAddress { get; init; } = new();
	public Address BillingAddress { get; init; } = new();
	public Dictionary<string, string> Metadata { get; init; } = new();
	public DateTimeOffset OrderDate { get; init; }
	public decimal TotalAmount { get; init; }
}

public record OrderItem
{
	public string ProductId { get; init; } = string.Empty;
	public string ProductName { get; init; } = string.Empty;
	public int Quantity { get; init; }
	public decimal UnitPrice { get; init; }
	public decimal TotalPrice { get; init; }
}

public record Address
{
	public string Street { get; init; } = string.Empty;
	public string City { get; init; } = string.Empty;
	public string State { get; init; } = string.Empty;
	public string PostalCode { get; init; } = string.Empty;
	public string Country { get; init; } = string.Empty;
}

/// <summary>
/// Large message (&gt; 10KB) - event with large payload (many items, audit trail).
/// </summary>
public record LargeMessage : IDispatchEvent
{
	public string BatchId { get; init; } = string.Empty;
	public List<BatchItem> Items { get; init; } = new();
	public List<AuditEntry> AuditTrail { get; init; } = new();
	public Dictionary<string, object> ExtensionData { get; init; } = new();
	public DateTimeOffset ProcessedAt { get; init; }
}

public record BatchItem
{
	public string Id { get; init; } = string.Empty;
	public string Type { get; init; } = string.Empty;
	public string Data { get; init; } = string.Empty;
	public Dictionary<string, string> Properties { get; init; } = new();
	public DateTimeOffset CreatedAt { get; init; }
}

public record AuditEntry
{
	public string Action { get; init; } = string.Empty;
	public string User { get; init; } = string.Empty;
	public string Details { get; init; } = string.Empty;
	public DateTimeOffset Timestamp { get; init; }
}

// Sprint 185 - Polymorphic message types for serialization benchmarks (bd-n237x)

/// <summary>
/// Polymorphic message containing a collection of derived event types.
/// Tests System.Text.Json polymorphic serialization with [JsonDerivedType].
/// </summary>
public record PolymorphicMessage : IDispatchEvent
{
	public string Id { get; init; } = string.Empty;
	public List<BaseUserEvent> Events { get; init; } = [];
	public DateTimeOffset Timestamp { get; init; }
}

/// <summary>
/// Base class for polymorphic user events.
/// Uses System.Text.Json polymorphic serialization attributes.
/// </summary>
[System.Text.Json.Serialization.JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(UserCreatedEvent), "UserCreated")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(UserUpdatedEvent), "UserUpdated")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(UserDeletedEvent), "UserDeleted")]
public abstract record BaseUserEvent
{
	public string UserId { get; init; } = string.Empty;
}

/// <summary>
/// User created event - derived type for polymorphic serialization.
/// </summary>
public record UserCreatedEvent : BaseUserEvent
{
	public string Username { get; init; } = string.Empty;
	public string Email { get; init; } = string.Empty;
	public DateTimeOffset CreatedAt { get; init; }
}

/// <summary>
/// User updated event - derived type for polymorphic serialization.
/// </summary>
public record UserUpdatedEvent : BaseUserEvent
{
	public Dictionary<string, string> ChangedProperties { get; init; } = [];
	public DateTimeOffset UpdatedAt { get; init; }
}

/// <summary>
/// User deleted event - derived type for polymorphic serialization.
/// </summary>
public record UserDeletedEvent : BaseUserEvent
{
	public string Reason { get; init; } = string.Empty;
	public DateTimeOffset DeletedAt { get; init; }
}

#pragma warning restore SA1402 // File may only contain a single type
