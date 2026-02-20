// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Serialization.MemoryPack;

namespace Excalibur.Dispatch.Serialization.Tests.MemoryPack;

/// <summary>
/// Unit tests for <see cref="MemoryPackPluggableSerializer" />.
/// </summary>
[Trait("Component", "Serialization")]
[Trait("Category", "Unit")]
public sealed class MemoryPackPluggableSerializerShould
{
	private readonly MemoryPackPluggableSerializer _sut;

	public MemoryPackPluggableSerializerShould()
	{
		_sut = new MemoryPackPluggableSerializer();
	}

	#region Property Tests

	[Fact]
	public void Name_ReturnsMemoryPack()
	{
		// Act & Assert
		_sut.Name.ShouldBe("MemoryPack");
	}

	[Fact]
	public void Version_ReturnsNonEmptyString()
	{
		// Act & Assert
		_sut.Version.ShouldNotBeNullOrEmpty();
	}

	[Fact]
	public void Version_ReturnsValidVersionFormat()
	{
		// Act
		var version = _sut.Version;

		// Assert - Either valid version or "Unknown"
		if (version != "Unknown")
		{
			System.Version.TryParse(version, out var parsedVersion).ShouldBeTrue();
			_ = parsedVersion.ShouldNotBeNull();
		}
	}

	#endregion Property Tests

	#region Serialize<T> Tests

	[Fact]
	public void Serialize_WithValidValue_ReturnsBytes()
	{
		// Arrange
		var value = new TestPayload { Id = 42, Name = "Test" };

		// Act
		var result = _sut.Serialize(value);

		// Assert
		_ = result.ShouldNotBeNull();
		result.Length.ShouldBeGreaterThan(0);
	}

	[Fact]
	public void Serialize_WithNullValue_ThrowsArgumentNullException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			_sut.Serialize<TestPayload>(null!));
	}

	[Fact]
	public void Serialize_WithEmptyString_Succeeds()
	{
		// Arrange
		var value = new TestPayload { Id = 1, Name = string.Empty };

		// Act
		var result = _sut.Serialize(value);

		// Assert
		_ = result.ShouldNotBeNull();
		result.Length.ShouldBeGreaterThan(0);
	}

	[Fact]
	public void Serialize_WithLargePayload_Succeeds()
	{
		// Arrange
		var largeString = new string('X', 10000);
		var value = new TestPayload { Id = 999, Name = largeString };

		// Act
		var result = _sut.Serialize(value);

		// Assert
		_ = result.ShouldNotBeNull();
		result.Length.ShouldBeGreaterThan(0);
	}

	[Fact]
	public void Serialize_WithZeroId_Succeeds()
	{
		// Arrange
		var value = new TestPayload { Id = 0, Name = "Zero" };

		// Act
		var result = _sut.Serialize(value);

		// Assert
		_ = result.ShouldNotBeNull();
	}

	[Fact]
	public void Serialize_WithNegativeId_Succeeds()
	{
		// Arrange
		var value = new TestPayload { Id = -999, Name = "Negative" };

		// Act
		var result = _sut.Serialize(value);

		// Assert
		_ = result.ShouldNotBeNull();
	}

	[Fact]
	[RequiresUnreferencedCode("Test")]
	[RequiresDynamicCode("Test")]
	public void Serialize_WhenMemoryPackThrows_WrapsInSerializationException()
	{
		// Arrange - Use a type that is NOT registered with MemoryPack (no [MemoryPackable] attribute)
		var unregisteredValue = new UnregisteredType { Value = "test" };

		// Act & Assert - MemoryPack will throw because the type has no formatter
		var ex = Should.Throw<SerializationException>(() =>
			_sut.Serialize(unregisteredValue));

		_ = ex.InnerException.ShouldNotBeNull();
		ex.Message.ShouldContain("serialize");
		ex.Message.ShouldContain(nameof(UnregisteredType));
	}

	#endregion Serialize<T> Tests

	#region Deserialize<T> Tests

	[Fact]
	public void Deserialize_WithValidData_ReturnsObject()
	{
		// Arrange
		var original = new TestPayload { Id = 123, Name = "Deserialize Test" };
		var bytes = _sut.Serialize(original);

		// Act
		var result = _sut.Deserialize<TestPayload>(bytes.AsSpan());

		// Assert
		_ = result.ShouldNotBeNull();
		result.Id.ShouldBe(123);
		result.Name.ShouldBe("Deserialize Test");
	}

	[Fact]
	public void Deserialize_WithUnicodeString_PreservesCharacters()
	{
		// Arrange
		var original = new TestPayload { Id = 1, Name = "Unicode: \u00e9\u00e0\u00fc\u4e2d\u6587" };
		var bytes = _sut.Serialize(original);

		// Act
		var result = _sut.Deserialize<TestPayload>(bytes.AsSpan());

		// Assert
		result.Name.ShouldBe(original.Name);
	}

	[Fact]
	public void Deserialize_WithSpecialCharacters_PreservesCharacters()
	{
		// Arrange
		var original = new TestPayload { Id = 2, Name = "Special: \t\n\r\"'\\/" };
		var bytes = _sut.Serialize(original);

		// Act
		var result = _sut.Deserialize<TestPayload>(bytes.AsSpan());

		// Assert
		result.Name.ShouldBe(original.Name);
	}

	[Fact]
	[RequiresUnreferencedCode("Test")]
	[RequiresDynamicCode("Test")]
	public void Deserialize_WithCorruptData_ThrowsSerializationException()
	{
		// Arrange - Provide completely invalid byte data
		var corruptData = new byte[] { 0xFF, 0xFE, 0xFD, 0xFC, 0xFB, 0xFA, 0x01, 0x02, 0x03 };

		// Act & Assert - MemoryPack may return null (triggering NullResult) or throw (triggering Wrap)
		var ex = Should.Throw<SerializationException>(() =>
			_sut.Deserialize<TestPayload>(corruptData.AsSpan()));

		ex.Message.ShouldContain(nameof(TestPayload));
	}

	[Fact]
	[RequiresUnreferencedCode("Test")]
	[RequiresDynamicCode("Test")]
	public void Deserialize_WhenUnregisteredType_WrapsInSerializationException()
	{
		// Arrange - Provide some bytes and try to deserialize to an unregistered type
		var someBytes = new byte[] { 0x00, 0x01, 0x02, 0x03 };

		// Act & Assert - MemoryPack will throw because the type has no formatter
		var ex = Should.Throw<SerializationException>(() =>
			_sut.Deserialize<UnregisteredType>(someBytes.AsSpan()));

		ex.Message.ShouldContain("deserialize");
		ex.Message.ShouldContain(nameof(UnregisteredType));
		_ = ex.InnerException.ShouldNotBeNull();
	}

	#endregion Deserialize<T> Tests

	#region SerializeObject Tests

	[Fact]
	public void SerializeObject_WithValidValue_ReturnsBytes()
	{
		// Arrange
		object value = new TestPayload { Id = 42, Name = "Object Test" };

		// Act
		var result = _sut.SerializeObject(value, typeof(TestPayload));

		// Assert
		_ = result.ShouldNotBeNull();
		result.Length.ShouldBeGreaterThan(0);
	}

	[Fact]
	public void SerializeObject_WithNullValue_ThrowsArgumentNullException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			_sut.SerializeObject(null!, typeof(TestPayload)));
	}

	[Fact]
	public void SerializeObject_WithNullType_ThrowsArgumentNullException()
	{
		// Arrange
		object value = new TestPayload { Id = 1, Name = "Test" };

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			_sut.SerializeObject(value, null!));
	}

	[Fact]
	[RequiresUnreferencedCode("Test")]
	[RequiresDynamicCode("Test")]
	public void SerializeObject_WhenMemoryPackThrows_WrapsInSerializationException()
	{
		// Arrange - Use a type that is NOT registered with MemoryPack
		var unregisteredValue = new UnregisteredType { Value = "test" };

		// Act & Assert
		var ex = Should.Throw<SerializationException>(() =>
			_sut.SerializeObject(unregisteredValue, typeof(UnregisteredType)));

		_ = ex.InnerException.ShouldNotBeNull();
		ex.Message.ShouldContain("serialize");
		ex.Message.ShouldContain(nameof(UnregisteredType));
	}

	#endregion SerializeObject Tests

	#region DeserializeObject Tests

	[Fact]
	public void DeserializeObject_WithValidData_ReturnsObject()
	{
		// Arrange
		var original = new TestPayload { Id = 456, Name = "Object Deserialize" };
		var bytes = _sut.SerializeObject(original, typeof(TestPayload));

		// Act
		var result = _sut.DeserializeObject(bytes.AsSpan(), typeof(TestPayload));

		// Assert
		_ = result.ShouldNotBeNull();
		var payload = result.ShouldBeOfType<TestPayload>();
		payload.Id.ShouldBe(456);
		payload.Name.ShouldBe("Object Deserialize");
	}

	[Fact]
	public void DeserializeObject_WithNullType_ThrowsArgumentNullException()
	{
		// Arrange
		var bytes = new byte[] { 1, 2, 3 };

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			_sut.DeserializeObject(bytes.AsSpan(), null!));
	}

	[Fact]
	[RequiresUnreferencedCode("Test")]
	[RequiresDynamicCode("Test")]
	public void DeserializeObject_WithCorruptData_ThrowsSerializationException()
	{
		// Arrange - Provide completely invalid byte data
		var corruptData = new byte[] { 0xFF, 0xFE, 0xFD, 0xFC, 0xFB, 0xFA, 0x01, 0x02, 0x03 };

		// Act & Assert - MemoryPack may return null (triggering NullResult) or throw (triggering Wrap)
		var ex = Should.Throw<SerializationException>(() =>
			_sut.DeserializeObject(corruptData.AsSpan(), typeof(TestPayload)));

		ex.Message.ShouldContain(nameof(TestPayload));
	}

	[Fact]
	[RequiresUnreferencedCode("Test")]
	[RequiresDynamicCode("Test")]
	public void DeserializeObject_WhenUnregisteredType_ThrowsSerializationException()
	{
		// Arrange - Provide some bytes and try to deserialize to an unregistered type
		var someBytes = new byte[] { 0x00, 0x01, 0x02, 0x03 };

		// Act & Assert - MemoryPack may return null or throw for unregistered types
		var ex = Should.Throw<SerializationException>(() =>
			_sut.DeserializeObject(someBytes.AsSpan(), typeof(UnregisteredType)));

		ex.Message.ShouldContain(nameof(UnregisteredType));
	}

	#endregion DeserializeObject Tests

	#region RoundTrip Tests

	[Fact]
	public void Serialize_AndDeserialize_RoundTrips()
	{
		// Arrange
		var original = new TestPayload { Id = 789, Name = "RoundTrip Test" };

		// Act
		var serialized = _sut.Serialize(original);
		var deserialized = _sut.Deserialize<TestPayload>(serialized);

		// Assert
		_ = deserialized.ShouldNotBeNull();
		deserialized.Id.ShouldBe(original.Id);
		deserialized.Name.ShouldBe(original.Name);
	}

	[Fact]
	public void SerializeObject_AndDeserializeObject_RoundTrips()
	{
		// Arrange
		var original = new TestPayload { Id = 101, Name = "Object RoundTrip" };

		// Act
		var serialized = _sut.SerializeObject(original, typeof(TestPayload));
		var deserialized = (TestPayload)_sut.DeserializeObject(serialized.AsSpan(), typeof(TestPayload));

		// Assert
		_ = deserialized.ShouldNotBeNull();
		deserialized.Id.ShouldBe(original.Id);
		deserialized.Name.ShouldBe(original.Name);
	}

	[Fact]
	public void RoundTrip_PreservesMaxInt()
	{
		// Arrange
		var original = new TestPayload { Id = int.MaxValue, Name = "Max" };

		// Act
		var bytes = _sut.Serialize(original);
		var result = _sut.Deserialize<TestPayload>(bytes);

		// Assert
		result.Id.ShouldBe(int.MaxValue);
	}

	[Fact]
	public void RoundTrip_PreservesMinInt()
	{
		// Arrange
		var original = new TestPayload { Id = int.MinValue, Name = "Min" };

		// Act
		var bytes = _sut.Serialize(original);
		var result = _sut.Deserialize<TestPayload>(bytes);

		// Assert
		result.Id.ShouldBe(int.MinValue);
	}

	#endregion RoundTrip Tests

	#region Interface Implementation Tests

	[Fact]
	public void ImplementsIPluggableSerializer()
	{
		// Assert
		_ = _sut.ShouldBeAssignableTo<IPluggableSerializer>();
	}

	#endregion Interface Implementation Tests

	#region Complex Type Tests

	[Fact]
	public void Serialize_WithCollections_Succeeds()
	{
		// Arrange
		var value = new TestPayloadWithCollections
		{
			Id = 1,
			Tags = ["tag1", "tag2", "tag3"],
			Metadata = new Dictionary<string, string>
			{
				["key1"] = "value1",
				["key2"] = "value2",
			},
		};

		// Act
		var bytes = _sut.Serialize(value);
		var result = _sut.Deserialize<TestPayloadWithCollections>(bytes);

		// Assert
		_ = result.ShouldNotBeNull();
		result.Tags.ShouldBe(["tag1", "tag2", "tag3"]);
		result.Metadata.ShouldContainKeyAndValue("key1", "value1");
	}

	[Fact]
	public void Serialize_WithEmptyCollections_Succeeds()
	{
		// Arrange
		var value = new TestPayloadWithCollections
		{
			Id = 1,
			Tags = [],
			Metadata = [],
		};

		// Act
		var bytes = _sut.Serialize(value);
		var result = _sut.Deserialize<TestPayloadWithCollections>(bytes);

		// Assert
		_ = result.ShouldNotBeNull();
		result.Tags.ShouldBeEmpty();
		result.Metadata.ShouldBeEmpty();
	}

	[Fact]
	public void Serialize_WithNullableProperties_PreservesNull()
	{
		// Arrange
		var value = new TestPayloadWithNullables
		{
			Id = 1,
			OptionalName = null,
			OptionalValue = null,
		};

		// Act
		var bytes = _sut.Serialize(value);
		var result = _sut.Deserialize<TestPayloadWithNullables>(bytes);

		// Assert
		_ = result.ShouldNotBeNull();
		result.OptionalName.ShouldBeNull();
		result.OptionalValue.ShouldBeNull();
	}

	[Fact]
	public void Serialize_WithNullableProperties_PreservesValues()
	{
		// Arrange
		var value = new TestPayloadWithNullables
		{
			Id = 1,
			OptionalName = "HasValue",
			OptionalValue = 42,
		};

		// Act
		var bytes = _sut.Serialize(value);
		var result = _sut.Deserialize<TestPayloadWithNullables>(bytes);

		// Assert
		_ = result.ShouldNotBeNull();
		result.OptionalName.ShouldBe("HasValue");
		result.OptionalValue.ShouldBe(42);
	}

	[Fact]
	public void Serialize_WithDateTimeOffset_PreservesValue()
	{
		// Arrange
		var timestamp = new DateTimeOffset(2025, 11, 26, 12, 30, 45, TimeSpan.FromHours(-5));
		var value = new TestPayloadWithDateTime
		{
			Id = 1,
			Timestamp = timestamp,
		};

		// Act
		var bytes = _sut.Serialize(value);
		var result = _sut.Deserialize<TestPayloadWithDateTime>(bytes);

		// Assert
		result.Timestamp.ShouldBe(timestamp);
	}

	[Fact]
	public void Serialize_WithGuid_PreservesValue()
	{
		// Arrange
		var guid = Guid.NewGuid();
		var value = new TestPayloadWithGuid
		{
			Id = guid,
			Name = "Guid Test",
		};

		// Act
		var bytes = _sut.Serialize(value);
		var result = _sut.Deserialize<TestPayloadWithGuid>(bytes);

		// Assert
		result.Id.ShouldBe(guid);
	}

	#endregion Complex Type Tests
}

/// <summary>
/// A type that is NOT decorated with [MemoryPackable], used to trigger serialization errors
/// in MemoryPack when no formatter is registered.
/// </summary>
public class UnregisteredType
{
	public string? Value { get; set; }
}
