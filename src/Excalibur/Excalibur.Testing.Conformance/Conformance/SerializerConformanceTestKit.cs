// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Buffers;

using Excalibur.Dispatch.Serialization;
using Excalibur.Testing;

namespace Excalibur.Testing.Conformance;

/// <summary>
/// Abstract conformance test kit for every <see cref="ISerializer"/> implementations.
/// Each concrete suite provides its own serializer instance, test DTOs, and equality assertions.
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
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Test method naming convention")]
public abstract class SerializerConformanceTestKit : ConformanceTestKit
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

	/// <summary>A serializer MUST identify itself: <c>Name</c> is never null or empty.</summary>
	public virtual void Name_ShouldReturnNonNullNonEmptyString()
	{
		// Arrange
		var serializer = CreateSerializer();

		// Act & Assert
		RequireNotEmpty(serializer.Name, "ISerializer.Name");
	}

	/// <summary>A serializer MUST report a version, so a consumer can record which format wrote a payload.</summary>
	public virtual void Version_ShouldReturnNonNullNonEmptyString()
	{
		// Arrange
		var serializer = CreateSerializer();

		// Act & Assert
		RequireNotEmpty(serializer.Version, "ISerializer.Version");
	}

	/// <summary>A serializer MUST report an IANA media type containing a "/", so it can be put on the wire as a content type.</summary>
	public virtual void ContentType_ShouldReturnValidIanaMediaType()
	{
		// Arrange
		var serializer = CreateSerializer();

		// Act
		var contentType = serializer.ContentType;

		// Assert
		RequireNotEmpty(contentType, "ISerializer.ContentType");
		RequireContains(contentType, "/", "ISerializer.ContentType");
	}

	/// <summary><c>Name</c> MUST be stable across reads — a consumer may cache it as a format discriminator.</summary>
	public virtual void Name_ShouldReturnConsistentValue()
	{
		// Arrange
		var serializer = CreateSerializer();

		// Act & Assert -- multiple calls return same value
		var name1 = serializer.Name;
		var name2 = serializer.Name;
		RequireEqual(name1, name2, "ISerializer.Name across two reads");
	}

	#endregion

	#region Round-Trip Tests (Object API)

	/// <summary>The object API MUST round-trip: serialize then deserialize yields a logically equal object.</summary>
	public virtual void RoundTrip_ObjectApi_ShouldProduceIdenticalObject()
	{
		// Arrange
		var serializer = CreateSerializer();
		var original = CreateTestObject();

		// Act
		var bytes = serializer.SerializeObject(original, TestObjectType);
		var deserialized = serializer.DeserializeObject(bytes, TestObjectType);

		// Assert
		RequireNotNull(deserialized, "the deserialized object");
		AssertObjectsEqual(original, deserialized);
	}

	/// <summary>Round-tripping MUST be idempotent — a second pass over an already round-tripped object does not degrade it.</summary>
	public virtual void RoundTrip_ObjectApi_MultipleRoundTrips_ShouldBeIdempotent()
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

	/// <summary>An empty or default object MUST round-trip like any other; absence of data is not an error.</summary>
	public virtual void RoundTrip_EmptyObject_ShouldProduceIdenticalObject()
	{
		// Arrange
		var serializer = CreateSerializer();
		var original = CreateEmptyTestObject();

		// Act
		var bytes = serializer.SerializeObject(original, TestObjectType);
		var deserialized = serializer.DeserializeObject(bytes, TestObjectType);

		// Assert
		RequireNotNull(deserialized, "the deserialized object");
		AssertObjectsEqual(original, deserialized);
	}

	#endregion

	#region Large Payload Tests

	/// <summary>A payload over 1MB MUST round-trip, so a serializer cannot pass by silently truncating.</summary>
	public virtual void RoundTrip_LargePayload_ShouldWorkWithoutError()
	{
		// Arrange
		var serializer = CreateSerializer();
		var original = CreateLargeTestObject();

		// Act
		var bytes = serializer.SerializeObject(original, TestObjectType);

		// Assert -- verify it's actually large (1MB+ serialized)
		RequireGreaterThan(bytes.Length, 1_000_000, "a large payload's serialized length");

		var deserialized = serializer.DeserializeObject(bytes, TestObjectType);
		RequireNotNull(deserialized, "the deserialized object");
		AssertObjectsEqual(original, deserialized);
	}

	#endregion

	#region Thread Safety Tests

	/// <summary>A serializer MUST be safe to share: concurrent serialize and deserialize from many threads raise nothing.</summary>
	public virtual void ConcurrentSerializeDeserialize_ShouldBeThreadSafe()
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
				for (var i = 0; i < 100; i++)
				{
					var serialized = serializer.SerializeObject(original, TestObjectType);
					var deserialized = serializer.DeserializeObject(serialized, TestObjectType);
					RequireNotNull(deserialized, "the deserialized object");
				}
			}
			catch (Exception ex)
			{
				exceptions.Add(ex);
			}
		});

		// Assert
		RequireNoExceptions(exceptions, "concurrent serialize/deserialize");
	}

	/// <summary>The buffer-writer path MUST also be thread-safe, since it is the allocation-free path a hot loop uses.</summary>
	public virtual void ConcurrentSerialize_BufferWriter_ShouldBeThreadSafe()
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
				for (var i = 0; i < 100; i++)
				{
					var buffer = new ArrayBufferWriter<byte>();
					SerializeTyped(serializer, original, buffer);
					RequireGreaterThan(buffer.WrittenCount, 0, "bytes written to the buffer");
				}
			}
			catch (Exception ex)
			{
				exceptions.Add(ex);
			}
		});

		// Assert
		RequireNoExceptions(exceptions, "concurrent serialize/deserialize");
	}

	#endregion

	#region Generic API Round-Trip Tests

	/// <summary>The generic buffer-writer API MUST round-trip equivalently to the object API.</summary>
	public virtual void RoundTrip_GenericApi_ShouldProduceIdenticalObject()
	{
		// Arrange
		var serializer = CreateSerializer();
		var original = CreateTestObject();

		// Act -- use IBufferWriter Serialize<T> + ReadOnlySpan DeserializeObject
		var buffer = new ArrayBufferWriter<byte>();
		SerializeTyped(serializer, original, buffer);

		var deserialized = serializer.DeserializeObject(buffer.WrittenSpan, TestObjectType);

		// Assert
		RequireNotNull(deserialized, "the deserialized object");
		AssertObjectsEqual(original, deserialized);
	}

	#endregion

	#region Null Input Tests

	/// <summary>Serializing a null value MUST throw rather than emit a payload that deserializes to nothing.</summary>
	public virtual void SerializeObject_NullValue_ShouldThrow()
	{
		// Arrange
		var serializer = CreateSerializer();

		// Act & Assert
		RequireThrows<ArgumentNullException>(
			() => serializer.SerializeObject(null!, TestObjectType),
			"the null-argument guard");
	}

	/// <summary>Serializing with a null type MUST throw — the type is what selects the schema.</summary>
	public virtual void SerializeObject_NullType_ShouldThrow()
	{
		// Arrange
		var serializer = CreateSerializer();
		var obj = CreateTestObject();

		// Act & Assert
		RequireThrows<ArgumentNullException>(
			() => serializer.SerializeObject(obj, null!),
			"the null-argument guard");
	}

	/// <summary>Deserializing with a null type MUST throw for the same reason.</summary>
	public virtual void DeserializeObject_NullType_ShouldThrow()
	{
		// Arrange
		var serializer = CreateSerializer();

		// Act & Assert
		RequireThrows<ArgumentNullException>(
			() => serializer.DeserializeObject(ReadOnlySpan<byte>.Empty, null!),
			"the null-argument guard");
	}

	/// <summary>Serializing to a null buffer writer MUST throw rather than discard the output.</summary>
	public virtual void Serialize_NullBufferWriter_ShouldThrow()
	{
		// Arrange
		var serializer = CreateSerializer();
		var obj = CreateTestObject();

		// Act & Assert
		RequireThrows<ArgumentNullException>(
			() => serializer.Serialize(obj, null!),
			"the null-argument guard");
	}

	/// <summary>The generic API MUST reject a null value too: an implementation that writes format-nil instead is not conformant.</summary>
	public virtual void Serialize_NullValue_GenericApi_ShouldThrowArgumentNullException()
	{
		// Arrange
		// The canonical null-argument contract for Serialize<T>. An implementation that serialises
		// null as format-nil instead of throwing fails here; ArgumentNullException.ThrowIfNull(value)
		// must fire before any serialization.
		var serializer = CreateSerializer();
		var buffer = new System.Buffers.ArrayBufferWriter<byte>();

		// Act & Assert — use string as an unambiguously-reference-type T so the null guard fires before
		// any format-specific type check. ThrowIfNull fires for any null reference regardless of T.
		RequireThrows<ArgumentNullException>(
			() => serializer.Serialize<string>(null!, buffer),
			"the null-argument guard");
	}

	#endregion

	// Kits in this package carry no test-framework or assertion-library dependency -- a consumer on
	// NUnit, MSTest or TUnit must be able to run them -- so failures are raised as
	// TestFixtureAssertionException. These are private: they are the kit's own plumbing, not surface a
	// deriving suite consumes, so they add nothing to the published API.
	private static void RequireNotNull(object? value, string what)
	{
		if (value is null)
		{
			throw new TestFixtureAssertionException($"{what} was null.");
		}
	}

	private static void RequireNotEmpty(string? value, string what)
	{
		if (string.IsNullOrEmpty(value))
		{
			throw new TestFixtureAssertionException($"{what} was null or empty.");
		}
	}

	private static void RequireContains(string value, string expected, string what)
	{
		if (!value.Contains(expected, StringComparison.Ordinal))
		{
			throw new TestFixtureAssertionException($"{what} was '{value}', which does not contain '{expected}'.");
		}
	}

	private static void RequireEqual<T>(T expected, T actual, string what)
	{
		if (!EqualityComparer<T>.Default.Equals(expected, actual))
		{
			throw new TestFixtureAssertionException($"{what}: expected '{expected}' but got '{actual}'.");
		}
	}

	private static void RequireGreaterThan(int actual, int floor, string what)
	{
		if (actual <= floor)
		{
			throw new TestFixtureAssertionException($"{what}: expected greater than {floor} but got {actual}.");
		}
	}

	private static void RequireNoExceptions(IReadOnlyCollection<Exception> exceptions, string what)
	{
		if (exceptions.Count > 0)
		{
			throw new TestFixtureAssertionException(
				$"{what}: {exceptions.Count} exception(s), first was {exceptions.First().Message}");
		}
	}

	private static void RequireThrows<TException>(Action action, string what)
		where TException : Exception
	{
		try
		{
			action();
		}
		catch (TException)
		{
			return;
		}
		catch (Exception ex)
		{
			throw new TestFixtureAssertionException(
				$"{what}: expected {typeof(TException).Name} but got {ex.GetType().Name}.");
		}

		throw new TestFixtureAssertionException($"{what}: expected {typeof(TException).Name} but nothing was thrown.");
	}

}
