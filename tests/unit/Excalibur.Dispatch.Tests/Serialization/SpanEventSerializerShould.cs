// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Buffers;
using System.Text;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Serialization;

using FakeItEasy;

namespace Excalibur.Dispatch.Tests.Serialization;

/// <summary>
/// Unit tests for <see cref="SpanEventSerializer"/> validating zero-allocation serialization,
/// Span-based operations, backward compatibility with byte[] APIs, type resolution, and error handling.
/// </summary>
/// <remarks>
/// Sprint 415 - Task T415.1: SpanEventSerializer tests (0% → 80%).
/// Tests critical infrastructure for event sourcing serialization.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Serialization")]
public sealed class SpanEventSerializerShould
{
	private readonly IPluggableSerializer _pluggableSerializer;
	private readonly ISerializerRegistry _registry;

	public SpanEventSerializerShould()
	{
		_pluggableSerializer = A.Fake<IPluggableSerializer>();
		_registry = A.Fake<ISerializerRegistry>();

		// Default setup for pluggable serializer
		_ = A.CallTo(() => _pluggableSerializer.Name).Returns("FakeSerializer");
		_ = A.CallTo(() => _pluggableSerializer.Version).Returns("1.0.0");
	}

	#region Constructor Tests - IPluggableSerializer

	[Fact]
	public void ThrowArgumentNullException_WhenPluggableSerializerIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new SpanEventSerializer((IPluggableSerializer)null!));
	}

	[Fact]
	public void CreateInstance_WithValidPluggableSerializer()
	{
		// Act
		var serializer = new SpanEventSerializer(_pluggableSerializer);

		// Assert
		_ = serializer.ShouldNotBeNull();
	}

	#endregion

	#region Constructor Tests - ISerializerRegistry

	[Fact]
	public void ThrowArgumentNullException_WhenRegistryIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new SpanEventSerializer((ISerializerRegistry)null!));
	}

	[Fact]
	public void UseMemoryPackFromRegistry_WhenAvailableByName()
	{
		// Arrange
		var memoryPackSerializer = A.Fake<IPluggableSerializer>();
		_ = A.CallTo(() => memoryPackSerializer.Name).Returns("MemoryPack");
		_ = A.CallTo(() => _registry.GetAll()).Returns(
			[(SerializerIds.MemoryPack, "MemoryPack", memoryPackSerializer)]);

		// Act
		var serializer = new SpanEventSerializer(_registry);

		// Assert
		_ = serializer.ShouldNotBeNull();
	}

	[Fact]
	public void UseMemoryPackFromRegistry_WhenAvailableById()
	{
		// Arrange
		var memoryPackSerializer = A.Fake<IPluggableSerializer>();
		_ = A.CallTo(() => _registry.GetAll())
			.Returns(Array.Empty<(byte, string, IPluggableSerializer)>());
		_ = A.CallTo(() => _registry.GetById(SerializerIds.MemoryPack)).Returns(memoryPackSerializer);

		// Act
		var serializer = new SpanEventSerializer(_registry);

		// Assert
		_ = serializer.ShouldNotBeNull();
	}

	[Fact]
	public void FallBackToCurrentSerializer_WhenMemoryPackNotAvailable()
	{
		// Arrange
		var currentSerializer = A.Fake<IPluggableSerializer>();
		_ = A.CallTo(() => _registry.GetAll())
			.Returns(Array.Empty<(byte, string, IPluggableSerializer)>());
		_ = A.CallTo(() => _registry.GetById(SerializerIds.MemoryPack)).Returns(null);
		_ = A.CallTo(() => _registry.GetCurrent()).Returns((SerializerIds.SystemTextJson, currentSerializer));

		// Act
		var serializer = new SpanEventSerializer(_registry);

		// Assert
		_ = serializer.ShouldNotBeNull();
	}

	[Fact]
	public void ThrowInvalidOperationException_WhenNoSerializerAvailable()
	{
		// Arrange
		_ = A.CallTo(() => _registry.GetAll())
			.Returns(Array.Empty<(byte, string, IPluggableSerializer)>());
		_ = A.CallTo(() => _registry.GetById(SerializerIds.MemoryPack)).Returns(null);
		_ = A.CallTo(() => _registry.GetCurrent()).Returns((SerializerIds.Unknown, (IPluggableSerializer)null!));

		// Act & Assert
		var ex = Should.Throw<InvalidOperationException>(() =>
			new SpanEventSerializer(_registry));
		ex.Message.ShouldContain("No serializer available");
	}

	#endregion

	#region SerializeEvent Span Tests

	[Fact]
	public void ThrowArgumentNullException_WhenSerializeEventCalledWithNullEvent()
	{
		// Arrange
		var serializer = new SpanEventSerializer(_pluggableSerializer);

		// Act & Assert
		var ex = Assert.Throws<ArgumentNullException>(() =>
		{
			var buffer = new byte[256];
			_ = serializer.SerializeEvent(null!, buffer.AsSpan());
		});
		_ = ex.ParamName.ShouldNotBeNull();
	}

	[Fact]
	public void SerializeEvent_WithSpan_ReturnsWrittenByteCount()
	{
		// Arrange
		var testEvent = new TestDomainEvent("agg-123", 1);
		var expectedBytes = Encoding.UTF8.GetBytes("serialized-event-data");

		_ = A.CallTo(() => _pluggableSerializer.SerializeObject(testEvent, A<Type>._))
			.Returns(expectedBytes);

		var serializer = new SpanEventSerializer(_pluggableSerializer);
		var buffer = new byte[256];

		// Act
		var bytesWritten = serializer.SerializeEvent(testEvent, buffer.AsSpan());

		// Assert
		bytesWritten.ShouldBe(expectedBytes.Length);
	}

	[Fact]
	public void SerializeEvent_CopiesDataToBuffer()
	{
		// Arrange
		var testEvent = new TestDomainEvent("agg-123", 1);
		var expectedBytes = new byte[] { 1, 2, 3, 4, 5 };

		_ = A.CallTo(() => _pluggableSerializer.SerializeObject(testEvent, A<Type>._))
			.Returns(expectedBytes);

		var serializer = new SpanEventSerializer(_pluggableSerializer);
		var buffer = new byte[256];

		// Act
		var bytesWritten = serializer.SerializeEvent(testEvent, buffer.AsSpan());

		// Assert
		buffer[0..bytesWritten].ShouldBe(expectedBytes);
	}

	[Fact]
	public void ThrowArgumentException_WhenBufferTooSmall()
	{
		// Arrange
		var testEvent = new TestDomainEvent("agg-123", 1);
		var largeData = new byte[100];

		_ = A.CallTo(() => _pluggableSerializer.SerializeObject(testEvent, A<Type>._))
			.Returns(largeData);

		var serializer = new SpanEventSerializer(_pluggableSerializer);

		// Act & Assert
		var ex = Assert.Throws<ArgumentException>(() =>
		{
			var smallBuffer = new byte[10]; // Too small
			_ = serializer.SerializeEvent(testEvent, smallBuffer.AsSpan());
		});
		ex.Message.ShouldContain("Buffer too small");
		ex.Message.ShouldContain("GetEventSize()");
	}

	#endregion

	#region DeserializeEvent Span Tests

	[Fact]
	public void ThrowArgumentNullException_WhenDeserializeEventCalledWithNullType()
	{
		// Arrange
		var serializer = new SpanEventSerializer(_pluggableSerializer);

		// Act & Assert
		var ex = Assert.Throws<ArgumentNullException>(() =>
		{
			var data = new byte[] { 1, 2, 3 };
			_ = serializer.DeserializeEvent(data.AsSpan(), null!);
		});
		_ = ex.ParamName.ShouldNotBeNull();
	}

	[Fact]
	public void DeserializeEvent_FromSpan_ReturnsEvent()
	{
		// Arrange - Use real serializer for Span-based deserialization
		var realSerializer = new SystemTextJsonPluggableSerializer();
		var serializer = new SpanEventSerializer(realSerializer);

		var testEvent = new TestDomainEvent("agg-123", 1);
		var serializedData = realSerializer.SerializeObject(testEvent, typeof(TestDomainEvent));

		// Act
		var result = serializer.DeserializeEvent(serializedData.AsSpan(), typeof(TestDomainEvent));

		// Assert
		_ = result.ShouldBeOfType<TestDomainEvent>();
		var resultEvent = (TestDomainEvent)result;
		resultEvent.AggregateId.ShouldBe("agg-123");
	}

	[Fact]
	public void ThrowSerializationException_WhenDeserializedObjectIsNotIDomainEvent()
	{
		// Arrange - Use real serializer
		var realSerializer = new SystemTextJsonPluggableSerializer();
		var serializer = new SpanEventSerializer(realSerializer);

		// Serialize a non-event object
		var notAnEvent = new TestSnapshot("state", 42);
		var serializedData = realSerializer.SerializeObject(notAnEvent, typeof(TestSnapshot));

		// Act & Assert - Try to deserialize as TestDomainEvent but it's actually TestSnapshot
		// This will fail with SerializationException because TestSnapshot is not IDomainEvent
		var ex = Assert.Throws<SerializationException>(() =>
		{
			_ = serializer.DeserializeEvent(serializedData.AsSpan(), typeof(TestSnapshot));
		});
		ex.Message.ShouldContain("not an IDomainEvent");
	}

	#endregion

	#region GetEventSize Tests

	[Fact]
	public void ThrowArgumentNullException_WhenGetEventSizeCalledWithNullEvent()
	{
		// Arrange
		var serializer = new SpanEventSerializer(_pluggableSerializer);

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			serializer.GetEventSize(null!));
	}

	[Fact]
	public void GetEventSize_ReturnsSerializedSizePlusMargin()
	{
		// Arrange
		var testEvent = new TestDomainEvent("agg-123", 1);
		var serializedBytes = new byte[100];

		_ = A.CallTo(() => _pluggableSerializer.SerializeObject(testEvent, A<Type>._))
			.Returns(serializedBytes);

		var serializer = new SpanEventSerializer(_pluggableSerializer);

		// Act
		var size = serializer.GetEventSize(testEvent);

		// Assert - Should be serialized size (100) + SizeMargin (64)
		size.ShouldBe(164);
	}

	[Fact]
	public void GetEventSize_IsLargerThanActualSerializedBytes()
	{
		// Arrange
		var testEvent = new TestDomainEvent("agg-123", 1);
		var serializedBytes = new byte[50];

		_ = A.CallTo(() => _pluggableSerializer.SerializeObject(testEvent, A<Type>._))
			.Returns(serializedBytes);

		var serializer = new SpanEventSerializer(_pluggableSerializer);

		// Act
		var estimatedSize = serializer.GetEventSize(testEvent);

		// Assert - Estimated size should always be larger than actual
		estimatedSize.ShouldBeGreaterThan(serializedBytes.Length);
	}

	#endregion

	#region SerializeSnapshot Span Tests

	[Fact]
	public void ThrowArgumentNullException_WhenSerializeSnapshotCalledWithNullSnapshot()
	{
		// Arrange
		var serializer = new SpanEventSerializer(_pluggableSerializer);

		// Act & Assert
		var ex = Assert.Throws<ArgumentNullException>(() =>
		{
			var buffer = new byte[256];
			_ = serializer.SerializeSnapshot(null!, buffer.AsSpan());
		});
		_ = ex.ParamName.ShouldNotBeNull();
	}

	[Fact]
	public void SerializeSnapshot_WithSpan_ReturnsWrittenByteCount()
	{
		// Arrange
		var snapshot = new TestSnapshot("state-data", 42);
		var expectedBytes = new byte[] { 10, 20, 30, 40, 50 };

		_ = A.CallTo(() => _pluggableSerializer.SerializeObject(snapshot, A<Type>._))
			.Returns(expectedBytes);

		var serializer = new SpanEventSerializer(_pluggableSerializer);
		var buffer = new byte[256];

		// Act
		var bytesWritten = serializer.SerializeSnapshot(snapshot, buffer.AsSpan());

		// Assert
		bytesWritten.ShouldBe(expectedBytes.Length);
	}

	[Fact]
	public void ThrowArgumentException_WhenSnapshotBufferTooSmall()
	{
		// Arrange
		var snapshot = new TestSnapshot("state-data", 42);
		var largeData = new byte[200];

		_ = A.CallTo(() => _pluggableSerializer.SerializeObject(snapshot, A<Type>._))
			.Returns(largeData);

		var serializer = new SpanEventSerializer(_pluggableSerializer);

		// Act & Assert
		var ex = Assert.Throws<ArgumentException>(() =>
		{
			var smallBuffer = new byte[50];
			_ = serializer.SerializeSnapshot(snapshot, smallBuffer.AsSpan());
		});
		ex.Message.ShouldContain("Buffer too small");
		ex.Message.ShouldContain("GetSnapshotSize()");
	}

	#endregion

	#region DeserializeSnapshot Span Tests

	[Fact]
	public void ThrowArgumentNullException_WhenDeserializeSnapshotCalledWithNullType()
	{
		// Arrange
		var serializer = new SpanEventSerializer(_pluggableSerializer);

		// Act & Assert
		var ex = Assert.Throws<ArgumentNullException>(() =>
		{
			var data = new byte[] { 1, 2, 3 };
			_ = serializer.DeserializeSnapshot(data.AsSpan(), null!);
		});
		_ = ex.ParamName.ShouldNotBeNull();
	}

	[Fact]
	public void DeserializeSnapshot_FromSpan_ReturnsSnapshot()
	{
		// Arrange - Use real serializer
		var realSerializer = new SystemTextJsonPluggableSerializer();
		var serializer = new SpanEventSerializer(realSerializer);

		var expectedSnapshot = new TestSnapshot("state-data", 42);
		var serializedData = realSerializer.SerializeObject(expectedSnapshot, typeof(TestSnapshot));

		// Act
		var result = serializer.DeserializeSnapshot(serializedData.AsSpan(), typeof(TestSnapshot));

		// Assert
		var resultSnapshot = result.ShouldBeOfType<TestSnapshot>();
		resultSnapshot.State.ShouldBe("state-data");
		resultSnapshot.Counter.ShouldBe(42);
	}

	#endregion

	#region GetSnapshotSize Tests

	[Fact]
	public void ThrowArgumentNullException_WhenGetSnapshotSizeCalledWithNullSnapshot()
	{
		// Arrange
		var serializer = new SpanEventSerializer(_pluggableSerializer);

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			serializer.GetSnapshotSize(null!));
	}

	[Fact]
	public void GetSnapshotSize_ReturnsSerializedSizePlusMargin()
	{
		// Arrange
		var snapshot = new TestSnapshot("state-data", 42);
		var serializedBytes = new byte[80];

		_ = A.CallTo(() => _pluggableSerializer.SerializeObject(snapshot, A<Type>._))
			.Returns(serializedBytes);

		var serializer = new SpanEventSerializer(_pluggableSerializer);

		// Act
		var size = serializer.GetSnapshotSize(snapshot);

		// Assert - Should be serialized size (80) + SizeMargin (64)
		size.ShouldBe(144);
	}

	#endregion

	#region byte[] Backward Compatibility - SerializeEvent

	[Fact]
	public void ThrowArgumentNullException_WhenByteArraySerializeEventCalledWithNullEvent()
	{
		// Arrange
		var serializer = new SpanEventSerializer(_pluggableSerializer);

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			serializer.SerializeEvent((IDomainEvent)null!));
	}

	[Fact]
	public void SerializeEvent_ByteArray_ReturnsSerializedBytes()
	{
		// Arrange
		var testEvent = new TestDomainEvent("agg-123", 1);
		var expectedBytes = new byte[] { 1, 2, 3, 4, 5 };

		_ = A.CallTo(() => _pluggableSerializer.SerializeObject(testEvent, A<Type>._))
			.Returns(expectedBytes);

		var serializer = new SpanEventSerializer(_pluggableSerializer);

		// Act
		var result = serializer.SerializeEvent(testEvent);

		// Assert
		result.ShouldBe(expectedBytes);
	}

	#endregion

	#region byte[] Backward Compatibility - DeserializeEvent

	[Fact]
	public void ThrowArgumentNullException_WhenByteArrayDeserializeEventCalledWithNullData()
	{
		// Arrange
		var serializer = new SpanEventSerializer(_pluggableSerializer);

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			serializer.DeserializeEvent((byte[])null!, typeof(TestDomainEvent)));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenByteArrayDeserializeEventCalledWithNullType()
	{
		// Arrange
		var serializer = new SpanEventSerializer(_pluggableSerializer);

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			serializer.DeserializeEvent(new byte[] { 1, 2, 3 }, null!));
	}

	[Fact]
	public void DeserializeEvent_ByteArray_ReturnsEvent()
	{
		// Arrange - Use real serializer to avoid FakeItEasy byte[] issues
		var realSerializer = new SystemTextJsonPluggableSerializer();
		var serializer = new SpanEventSerializer(realSerializer);

		var testEvent = new TestDomainEvent("agg-123", 1);
		var data = realSerializer.SerializeObject(testEvent, typeof(TestDomainEvent));

		// Act
		var result = serializer.DeserializeEvent(data, typeof(TestDomainEvent));

		// Assert
		var resultEvent = result.ShouldBeOfType<TestDomainEvent>();
		resultEvent.AggregateId.ShouldBe("agg-123");
		resultEvent.Version.ShouldBe(1);
	}

	[Fact]
	public void ThrowSerializationException_WhenByteArrayDeserializedObjectIsNotIDomainEvent()
	{
		// Arrange - Use real serializer
		var realSerializer = new SystemTextJsonPluggableSerializer();
		var serializer = new SpanEventSerializer(realSerializer);

		// Serialize a non-event object
		var notAnEvent = new TestSnapshot("state", 42);
		var data = realSerializer.SerializeObject(notAnEvent, typeof(TestSnapshot));

		// Act & Assert - Try to deserialize as TestSnapshot (which is not IDomainEvent)
		var ex = Should.Throw<SerializationException>(() =>
			serializer.DeserializeEvent(data, typeof(TestSnapshot)));
		ex.Message.ShouldContain("not an IDomainEvent");
	}

	#endregion

	#region Type Resolution Tests

	[Fact]
	public void ThrowArgumentNullException_WhenGetTypeNameCalledWithNullType()
	{
		// Arrange
		var serializer = new SpanEventSerializer(_pluggableSerializer);

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			serializer.GetTypeName(null!));
	}

	[Fact]
	public void GetTypeName_ReturnsAssemblyQualifiedName()
	{
		// Arrange
		var serializer = new SpanEventSerializer(_pluggableSerializer);
		var type = typeof(TestDomainEvent);

		// Act
		var result = serializer.GetTypeName(type);

		// Assert
		result.ShouldBe(type.AssemblyQualifiedName);
	}

	[Fact]
	public void ThrowArgumentNullException_WhenResolveTypeCalledWithNullTypeName()
	{
		// Arrange
		var serializer = new SpanEventSerializer(_pluggableSerializer);

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			serializer.ResolveType(null!));
	}

	[Fact]
	public void ResolveType_ReturnsCorrectType()
	{
		// Arrange
		var serializer = new SpanEventSerializer(_pluggableSerializer);
		var expectedType = typeof(TestDomainEvent);
		var typeName = expectedType.AssemblyQualifiedName;

		// Act
		var result = serializer.ResolveType(typeName);

		// Assert
		result.ShouldBe(expectedType);
	}

	[Fact]
	public void ThrowSerializationException_WhenResolveTypeCannotResolve()
	{
		// Arrange
		var serializer = new SpanEventSerializer(_pluggableSerializer);

		// Act & Assert
		var ex = Should.Throw<SerializationException>(() =>
			serializer.ResolveType("NonExistent.Type.Name, FakeAssembly"));
		ex.Message.ShouldContain("Cannot resolve type");
	}

	#endregion

	#region Round-Trip Tests with Real Serializer

	[Fact]
	public void RoundTrip_Event_WithSystemTextJson()
	{
		// Arrange
		var realSerializer = new SystemTextJsonPluggableSerializer();
		var serializer = new SpanEventSerializer(realSerializer);

		var originalEvent = new TestDomainEvent("agg-roundtrip", 42);

		// Act - Serialize
		var size = serializer.GetEventSize(originalEvent);
		var buffer = new byte[size];
		var bytesWritten = serializer.SerializeEvent(originalEvent, buffer.AsSpan());

		// Act - Deserialize
		var result = serializer.DeserializeEvent(buffer.AsSpan(0, bytesWritten), typeof(TestDomainEvent));

		// Assert
		var roundTripped = result.ShouldBeOfType<TestDomainEvent>();
		roundTripped.AggregateId.ShouldBe(originalEvent.AggregateId);
		roundTripped.Version.ShouldBe(originalEvent.Version);
	}

	[Fact]
	public void RoundTrip_Snapshot_WithSystemTextJson()
	{
		// Arrange
		var realSerializer = new SystemTextJsonPluggableSerializer();
		var serializer = new SpanEventSerializer(realSerializer);

		var originalSnapshot = new TestSnapshot("snapshot-state", 999);

		// Act - Serialize
		var size = serializer.GetSnapshotSize(originalSnapshot);
		var buffer = new byte[size];
		var bytesWritten = serializer.SerializeSnapshot(originalSnapshot, buffer.AsSpan());

		// Act - Deserialize
		var result = serializer.DeserializeSnapshot(buffer.AsSpan(0, bytesWritten), typeof(TestSnapshot));

		// Assert
		var roundTripped = result.ShouldBeOfType<TestSnapshot>();
		roundTripped.State.ShouldBe(originalSnapshot.State);
		roundTripped.Counter.ShouldBe(originalSnapshot.Counter);
	}

	[Fact]
	public void RoundTrip_ByteArray_PreservesData()
	{
		// Arrange
		var realSerializer = new SystemTextJsonPluggableSerializer();
		var serializer = new SpanEventSerializer(realSerializer);

		var originalEvent = new TestDomainEvent("agg-bytearray", 7);

		// Act - Serialize with byte[] API
		var bytes = serializer.SerializeEvent(originalEvent);

		// Act - Deserialize with byte[] API
		var result = serializer.DeserializeEvent(bytes, typeof(TestDomainEvent));

		// Assert
		var roundTripped = result.ShouldBeOfType<TestDomainEvent>();
		roundTripped.AggregateId.ShouldBe(originalEvent.AggregateId);
		roundTripped.Version.ShouldBe(originalEvent.Version);
	}

	#endregion

	#region ArrayPool Integration Tests

	[Fact]
	public void GetEventSize_AllowsArrayPoolUsage()
	{
		// Arrange
		var realSerializer = new SystemTextJsonPluggableSerializer();
		var serializer = new SpanEventSerializer(realSerializer);
		var testEvent = new TestDomainEvent("agg-pool", 100);

		// Act - Use ArrayPool pattern
		var size = serializer.GetEventSize(testEvent);
		var buffer = ArrayPool<byte>.Shared.Rent(size);
		try
		{
			var written = serializer.SerializeEvent(testEvent, buffer.AsSpan());

			// Assert
			written.ShouldBeGreaterThan(0);
			written.ShouldBeLessThanOrEqualTo(size);
		}
		finally
		{
			ArrayPool<byte>.Shared.Return(buffer);
		}
	}

	[Fact]
	public void GetSnapshotSize_AllowsArrayPoolUsage()
	{
		// Arrange
		var realSerializer = new SystemTextJsonPluggableSerializer();
		var serializer = new SpanEventSerializer(realSerializer);
		var snapshot = new TestSnapshot("pool-state", 200);

		// Act - Use ArrayPool pattern
		var size = serializer.GetSnapshotSize(snapshot);
		var buffer = ArrayPool<byte>.Shared.Rent(size);
		try
		{
			var written = serializer.SerializeSnapshot(snapshot, buffer.AsSpan());

			// Assert
			written.ShouldBeGreaterThan(0);
			written.ShouldBeLessThanOrEqualTo(size);
		}
		finally
		{
			ArrayPool<byte>.Shared.Return(buffer);
		}
	}

	#endregion

	#region Concurrent Serialization Tests

	[Fact]
	public async Task ConcurrentSerialization_AllSucceed()
	{
		// Arrange
		var realSerializer = new SystemTextJsonPluggableSerializer();
		var serializer = new SpanEventSerializer(realSerializer);
		var events = Enumerable.Range(0, 100)
			.Select(i => new TestDomainEvent($"agg-{i}", i))
			.ToList();

		// Act
		var tasks = events.Select(async evt =>
		{
			await Task.Yield(); // Force async execution
			var size = serializer.GetEventSize(evt);
			var buffer = new byte[size];
			return serializer.SerializeEvent(evt, buffer.AsSpan());
		});

		var results = await Task.WhenAll(tasks);

		// Assert
		results.Length.ShouldBe(100);
		results.All(r => r > 0).ShouldBeTrue();
	}

	[Fact]
	public async Task ConcurrentDeserialization_AllSucceed()
	{
		// Arrange
		var realSerializer = new SystemTextJsonPluggableSerializer();
		var serializer = new SpanEventSerializer(realSerializer);
		var events = Enumerable.Range(0, 100)
			.Select(i => new TestDomainEvent($"agg-{i}", i))
			.ToList();

		var serializedEvents = events.Select(evt => serializer.SerializeEvent(evt)).ToList();

		// Act
		var tasks = serializedEvents.Select(async (bytes, index) =>
		{
			await Task.Yield();
			return serializer.DeserializeEvent(bytes, typeof(TestDomainEvent));
		});

		var results = await Task.WhenAll(tasks);

		// Assert
		results.Length.ShouldBe(100);
		for (var i = 0; i < results.Length; i++)
		{
			var evt = results[i].ShouldBeOfType<TestDomainEvent>();
			evt.AggregateId.ShouldBe($"agg-{i}");
			evt.Version.ShouldBe(i);
		}
	}

	#endregion

	#region Test Fixtures

	/// <summary>
	/// Test domain event implementation for serialization testing.
	/// </summary>
	private sealed class TestDomainEvent : IDomainEvent
	{
		public TestDomainEvent() { }

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
	/// Test snapshot implementation for snapshot serialization testing.
	/// </summary>
	private sealed class TestSnapshot
	{
		public TestSnapshot() { }

		public TestSnapshot(string state, int counter)
		{
			State = state;
			Counter = counter;
		}

		public string State { get; init; } = string.Empty;
		public int Counter { get; init; }
	}

	#endregion
}
