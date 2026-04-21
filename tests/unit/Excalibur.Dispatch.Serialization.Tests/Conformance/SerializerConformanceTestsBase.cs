// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Buffers;

namespace Excalibur.Dispatch.Serialization.Tests.Conformance;

/// <summary>
/// Sprint 623 A.1: Abstract conformance test base for all <see cref="ISerializer"/> implementations.
/// Each concrete subclass provides its own serializer instance, test DTOs, and equality assertions.
/// </summary>
/// <remarks>
/// Conformance contracts tested:
/// <list type="bullet">
///   <item>Round-trip: Serialize -> Deserialize produces identical object</item>
///   <item>Null handling: Null input behavior documented and consistent</item>
///   <item>Empty payloads: Empty/default objects serialize/deserialize correctly</item>
///   <item>Large payloads: 1MB+ payloads work without error</item>
///   <item>Thread safety: Concurrent serialize/deserialize from multiple threads</item>
///   <item>ContentType validation: Returns valid IANA type</item>
///   <item>Name/Version: Properties return non-null, non-empty strings</item>
/// </list>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Serialization)]
public abstract class SerializerConformanceTestsBase
{
	/// <summary>Creates the serializer under test.</summary>
	protected abstract ISerializer CreateSerializer();

	/// <summary>Creates a test object with populated properties.</summary>
	protected abstract object CreateTestObject();

	/// <summary>Creates an empty/default test object.</summary>
	protected abstract object CreateEmptyTestObject();

	/// <summary>Gets the runtime type of the test object.</summary>
	protected abstract Type TestObjectType { get; }

	/// <summary>Asserts that two test objects are logically equal.</summary>
	protected abstract void AssertObjectsEqual(object expected, object actual);

	/// <summary>Creates a large test object (1MB+ when serialized).</summary>
	protected abstract object CreateLargeTestObject();

	/// <summary>
	/// Serializes <paramref name="value"/> using the generic Serialize&lt;T&gt; API.
	/// Override in subclasses where T must be the concrete DTO type (MemoryPack, MessagePack, Protobuf).
	/// </summary>
	protected virtual void SerializeTyped(ISerializer serializer, object value, IBufferWriter<byte> bufferWriter)
		=> serializer.Serialize(value, bufferWriter);

	#region Property Tests

	[Fact]
	public void Name_ShouldReturnNonNullNonEmptyString()
	{
		// Arrange
		var serializer = CreateSerializer();

		// Act & Assert
		serializer.Name.ShouldNotBeNull();
		serializer.Name.ShouldNotBeEmpty();
	}

	[Fact]
	public void Version_ShouldReturnNonNullNonEmptyString()
	{
		// Arrange
		var serializer = CreateSerializer();

		// Act & Assert
		serializer.Version.ShouldNotBeNull();
		serializer.Version.ShouldNotBeEmpty();
	}

	[Fact]
	public void ContentType_ShouldReturnValidIanaMediaType()
	{
		// Arrange
		var serializer = CreateSerializer();

		// Act
		var contentType = serializer.ContentType;

		// Assert
		contentType.ShouldNotBeNull();
		contentType.ShouldNotBeEmpty();
		contentType.ShouldContain("/");
	}

	[Fact]
	public void Name_ShouldReturnConsistentValue()
	{
		// Arrange
		var serializer = CreateSerializer();

		// Act & Assert -- multiple calls return same value
		var name1 = serializer.Name;
		var name2 = serializer.Name;
		name1.ShouldBe(name2);
	}

	#endregion

	#region Round-Trip Tests (Object API)

	[Fact]
	public void RoundTrip_ObjectApi_ShouldProduceIdenticalObject()
	{
		// Arrange
		var serializer = CreateSerializer();
		var original = CreateTestObject();

		// Act
		var bytes = serializer.SerializeObject(original, TestObjectType);
		var deserialized = serializer.DeserializeObject(bytes, TestObjectType);

		// Assert
		deserialized.ShouldNotBeNull();
		AssertObjectsEqual(original, deserialized);
	}

	[Fact]
	public void RoundTrip_ObjectApi_MultipleRoundTrips_ShouldBeIdempotent()
	{
		// Arrange
		var serializer = CreateSerializer();
		var original = CreateTestObject();

		// Act -- serialize -> deserialize -> serialize -> deserialize
		var bytes1 = serializer.SerializeObject(original, TestObjectType);
		var deserialized1 = serializer.DeserializeObject(bytes1, TestObjectType);
		var bytes2 = serializer.SerializeObject(deserialized1, TestObjectType);
		var deserialized2 = serializer.DeserializeObject(bytes2, TestObjectType);

		// Assert
		AssertObjectsEqual(original, deserialized2);
	}

	#endregion

	#region Empty/Default Payload Tests

	[Fact]
	public void RoundTrip_EmptyObject_ShouldProduceIdenticalObject()
	{
		// Arrange
		var serializer = CreateSerializer();
		var original = CreateEmptyTestObject();

		// Act
		var bytes = serializer.SerializeObject(original, TestObjectType);
		var deserialized = serializer.DeserializeObject(bytes, TestObjectType);

		// Assert
		deserialized.ShouldNotBeNull();
		AssertObjectsEqual(original, deserialized);
	}

	#endregion

	#region Large Payload Tests

	[Fact]
	public void RoundTrip_LargePayload_ShouldWorkWithoutError()
	{
		// Arrange
		var serializer = CreateSerializer();
		var original = CreateLargeTestObject();

		// Act
		var bytes = serializer.SerializeObject(original, TestObjectType);

		// Assert -- verify it's actually large (1MB+ serialized)
		bytes.Length.ShouldBeGreaterThan(1_000_000, "Large payload should serialize to > 1MB");

		var deserialized = serializer.DeserializeObject(bytes, TestObjectType);
		deserialized.ShouldNotBeNull();
		AssertObjectsEqual(original, deserialized);
	}

	#endregion

	#region Thread Safety Tests

	[Fact]
	public void ConcurrentSerializeDeserialize_ShouldBeThreadSafe()
	{
		// Arrange
		var serializer = CreateSerializer();
		var original = CreateTestObject();
		var bytes = serializer.SerializeObject(original, TestObjectType);
		var exceptions = new System.Collections.Concurrent.ConcurrentBag<Exception>();

		// Act -- concurrent serialize + deserialize from 8 threads, 100 iterations each
		Parallel.For(0, 8, _ =>
		{
			try
			{
				for (int i = 0; i < 100; i++)
				{
					var serialized = serializer.SerializeObject(original, TestObjectType);
					var deserialized = serializer.DeserializeObject(serialized, TestObjectType);
					deserialized.ShouldNotBeNull();
				}
			}
			catch (Exception ex)
			{
				exceptions.Add(ex);
			}
		});

		// Assert
		exceptions.ShouldBeEmpty($"Thread safety violation: {exceptions.FirstOrDefault()?.Message}");
	}

	[Fact]
	public void ConcurrentSerialize_BufferWriter_ShouldBeThreadSafe()
	{
		// Arrange
		var serializer = CreateSerializer();
		var original = CreateTestObject();
		var exceptions = new System.Collections.Concurrent.ConcurrentBag<Exception>();

		// Act -- concurrent serialize using IBufferWriter API
		Parallel.For(0, 8, _ =>
		{
			try
			{
				for (int i = 0; i < 100; i++)
				{
					var buffer = new ArrayBufferWriter<byte>();
					SerializeTyped(serializer, original, buffer);
					buffer.WrittenCount.ShouldBeGreaterThan(0);
				}
			}
			catch (Exception ex)
			{
				exceptions.Add(ex);
			}
		});

		// Assert
		exceptions.ShouldBeEmpty($"Thread safety violation: {exceptions.FirstOrDefault()?.Message}");
	}

	#endregion

	#region Generic API Round-Trip Tests

	[Fact]
	public void RoundTrip_GenericApi_ShouldProduceIdenticalObject()
	{
		// Arrange
		var serializer = CreateSerializer();
		var original = CreateTestObject();

		// Act -- use IBufferWriter Serialize<T> + ReadOnlySpan DeserializeObject
		var buffer = new ArrayBufferWriter<byte>();
		SerializeTyped(serializer, original, buffer);

		var deserialized = serializer.DeserializeObject(buffer.WrittenSpan, TestObjectType);

		// Assert
		deserialized.ShouldNotBeNull();
		AssertObjectsEqual(original, deserialized);
	}

	#endregion

	#region Null Input Tests

	[Fact]
	public void SerializeObject_NullValue_ShouldThrow()
	{
		// Arrange
		var serializer = CreateSerializer();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			serializer.SerializeObject(null!, TestObjectType));
	}

	[Fact]
	public void SerializeObject_NullType_ShouldThrow()
	{
		// Arrange
		var serializer = CreateSerializer();
		var obj = CreateTestObject();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			serializer.SerializeObject(obj, null!));
	}

	[Fact]
	public void DeserializeObject_NullType_ShouldThrow()
	{
		// Arrange
		var serializer = CreateSerializer();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			serializer.DeserializeObject(ReadOnlySpan<byte>.Empty, null!));
	}

	[Fact]
	public void Serialize_NullBufferWriter_ShouldThrow()
	{
		// Arrange
		var serializer = CreateSerializer();
		var obj = CreateTestObject();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			serializer.Serialize(obj, null!));
	}

	#endregion
}
