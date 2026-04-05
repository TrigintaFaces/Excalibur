// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json;

using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Serialization;

namespace Excalibur.Dispatch.Tests.Messaging.Serialization;

/// <summary>
/// Unit tests for SystemTextJsonSerializer covering JSON serialization scenarios.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Dispatch.Core")]
public sealed class SystemTextJsonMessageSerializerShould : UnitTestBase
{
	private readonly SystemTextJsonSerializer _sut;

	public SystemTextJsonMessageSerializerShould()
	{
		_sut = new SystemTextJsonSerializer(new JsonSerializerOptions
		{
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		});
	}

	#region Serializer Properties Tests

	[Fact]
	public void Name_ReturnsSystemTextJson()
	{
		// Assert
		_sut.Name.ShouldBe("System.Text.Json");
	}

	[Fact]
	public void Version_ReturnsExpectedVersion()
	{
		// Assert
		_sut.Version.ShouldNotBeNullOrEmpty();
	}

	#endregion Serializer Properties Tests

	#region Basic Serialization Tests

	[Fact]
	public void Serialize_SimpleObject_ReturnsByteArray()
	{
		// Arrange
		var message = new SimpleMessage { Id = 1, Name = "Test" };

		// Act
		var result = _sut.SerializeToBytes(message);

		// Assert
		_ = result.ShouldNotBeNull();
		result.Length.ShouldBeGreaterThan(0);
	}

	[Fact]
	public void Serialize_ComplexNestedObject_ReturnsByteArray()
	{
		// Arrange
		var message = new ComplexMessage
		{
			Id = 1,
			Name = "Parent",
			Child = new ChildMessage { Value = "Nested" },
		};

		// Act
		var result = _sut.SerializeToBytes(message);

		// Assert
		_ = result.ShouldNotBeNull();
		result.Length.ShouldBeGreaterThan(0);
	}

	[Fact]
	public void Serialize_Collection_ReturnsByteArray()
	{
		// Arrange
		var messages = new List<SimpleMessage>
		{
			new() { Id = 1, Name = "First" },
			new() { Id = 2, Name = "Second" },
		};

		// Act
		var result = _sut.SerializeToBytes(messages);

		// Assert
		_ = result.ShouldNotBeNull();
		result.Length.ShouldBeGreaterThan(0);
	}

	[Fact]
	public void Serialize_Dictionary_ReturnsByteArray()
	{
		// Arrange
		var dictionary = new Dictionary<string, int>
		{
			["one"] = 1,
			["two"] = 2,
		};

		// Act
		var result = _sut.SerializeToBytes(dictionary);

		// Assert
		_ = result.ShouldNotBeNull();
		result.Length.ShouldBeGreaterThan(0);
	}

	#endregion Basic Serialization Tests

	#region Deserialization Tests

	[Fact]
	public void Deserialize_SimpleObject_ReturnsCorrectObject()
	{
		// Arrange
		var original = new SimpleMessage { Id = 42, Name = "Test" };
		var serialized = _sut.SerializeToBytes(original);

		// Act
		var result = _sut.Deserialize<SimpleMessage>(serialized);

		// Assert
		_ = result.ShouldNotBeNull();
		result.Id.ShouldBe(original.Id);
		result.Name.ShouldBe(original.Name);
	}

	[Fact]
	public void Deserialize_ComplexNestedObject_ReturnsCorrectObject()
	{
		// Arrange
		var original = new ComplexMessage
		{
			Id = 1,
			Name = "Parent",
			Child = new ChildMessage { Value = "Nested" },
		};
		var serialized = _sut.SerializeToBytes(original);

		// Act
		var result = _sut.Deserialize<ComplexMessage>(serialized);

		// Assert
		_ = result.ShouldNotBeNull();
		result.Id.ShouldBe(original.Id);
		result.Name.ShouldBe(original.Name);
		_ = result.Child.ShouldNotBeNull();
		result.Child.Value.ShouldBe(original.Child.Value);
	}

	[Fact]
	public void Deserialize_Collection_ReturnsCorrectList()
	{
		// Arrange
		var original = new List<SimpleMessage>
		{
			new() { Id = 1, Name = "First" },
			new() { Id = 2, Name = "Second" },
		};
		var serialized = _sut.SerializeToBytes(original);

		// Act
		var result = _sut.Deserialize<List<SimpleMessage>>(serialized);

		// Assert
		_ = result.ShouldNotBeNull();
		result.Count.ShouldBe(2);
		result[0].Name.ShouldBe("First");
		result[1].Name.ShouldBe("Second");
	}

	[Fact]
	public void Deserialize_Dictionary_ReturnsCorrectDictionary()
	{
		// Arrange
		var original = new Dictionary<string, int>
		{
			["one"] = 1,
			["two"] = 2,
		};
		var serialized = _sut.SerializeToBytes(original);

		// Act
		var result = _sut.Deserialize<Dictionary<string, int>>(serialized);

		// Assert
		_ = result.ShouldNotBeNull();
		result.Count.ShouldBe(2);
		result["one"].ShouldBe(1);
		result["two"].ShouldBe(2);
	}

	#endregion Deserialization Tests

	#region Round-Trip Tests

	[Fact]
	public void RoundTrip_WithDateTimeOffset_PreservesValue()
	{
		// Arrange
		var original = new MessageWithDateTime
		{
			Timestamp = new DateTimeOffset(2025, 1, 15, 10, 30, 0, TimeSpan.Zero),
		};
		var serialized = _sut.SerializeToBytes(original);

		// Act
		var result = _sut.Deserialize<MessageWithDateTime>(serialized);

		// Assert
		result.Timestamp.ShouldBe(original.Timestamp);
	}

	[Fact]
	public void RoundTrip_WithEnum_PreservesValue()
	{
		// Arrange
		var original = new MessageWithEnum { Status = MessageStatus.Processing };
		var serialized = _sut.SerializeToBytes(original);

		// Act
		var result = _sut.Deserialize<MessageWithEnum>(serialized);

		// Assert
		result.Status.ShouldBe(MessageStatus.Processing);
	}

	[Fact]
	public void RoundTrip_WithUnicode_PreservesValue()
	{
		// Arrange
		var original = new SimpleMessage { Id = 1, Name = "日本語テスト 🎉" };
		var serialized = _sut.SerializeToBytes(original);

		// Act
		var result = _sut.Deserialize<SimpleMessage>(serialized);

		// Assert
		result.Name.ShouldBe(original.Name);
	}

	[Fact]
	public void RoundTrip_WithDecimal_PreservesValue()
	{
		// Arrange
		var original = new MessageWithDecimal { Amount = 123.456789m };
		var serialized = _sut.SerializeToBytes(original);

		// Act
		var result = _sut.Deserialize<MessageWithDecimal>(serialized);

		// Assert
		result.Amount.ShouldBe(123.456789m);
	}

	#endregion Round-Trip Tests

	#region Error Handling Tests

	[Fact]
	public void Serialize_WithNull_ProducesValidOutput()
	{
		// Act - New consolidated serializer follows STJ pattern: null serializes to "null"
		var result = _sut.SerializeToBytes<SimpleMessage>(null!);

		// Assert
		_ = result.ShouldNotBeNull();
	}

	[Fact]
	public void Deserialize_WithNull_ThrowsArgumentNullException()
	{
		// Act & Assert - Must call extension method explicitly; instance method resolves
		// via implicit byte[]->ReadOnlySpan conversion, bypassing the null guard.
		_ = Should.Throw<ArgumentNullException>(() =>
			SerializerExtensions.Deserialize<SimpleMessage>(_sut, (byte[])null!));
	}

	[Fact]
	public void Deserialize_InvalidJson_ThrowsSerializationException()
	{
		// Arrange
		var invalidData = System.Text.Encoding.UTF8.GetBytes("not valid json {{{");

		// Act & Assert
		_ = Should.Throw<SerializationException>(() => _sut.Deserialize<SimpleMessage>(invalidData));
	}

	[Fact]
	public void Deserialize_EmptyArray_ThrowsSerializationException()
	{
		// Arrange
		var emptyData = Array.Empty<byte>();

		// Act & Assert
		_ = Should.Throw<SerializationException>(() => _sut.Deserialize<SimpleMessage>(emptyData));
	}

	#endregion Error Handling Tests

	#region Edge Cases

	[Fact]
	public void Serialize_EmptyCollection_ReturnsByteArray()
	{
		// Arrange
		var emptyList = new List<SimpleMessage>();

		// Act
		var result = _sut.SerializeToBytes(emptyList);

		// Assert
		_ = result.ShouldNotBeNull();
		result.Length.ShouldBeGreaterThan(0);
	}

	[Fact]
	public void RoundTrip_EmptyCollection_PreservesEmptyList()
	{
		// Arrange
		var original = new List<SimpleMessage>();
		var serialized = _sut.SerializeToBytes(original);

		// Act
		var result = _sut.Deserialize<List<SimpleMessage>>(serialized);

		// Assert
		_ = result.ShouldNotBeNull();
		result.ShouldBeEmpty();
	}

	#endregion Edge Cases

	#region Test Fixtures

	private enum MessageStatus
	{
		Pending,
		Processing,
		Completed,
	}

	private sealed class SimpleMessage
	{
		public int Id { get; set; }
		public string Name { get; set; } = string.Empty;
	}

	private sealed class ComplexMessage
	{
		public int Id { get; set; }
		public string Name { get; set; } = string.Empty;
		public ChildMessage? Child { get; set; }
	}

	private sealed class ChildMessage
	{
		public string Value { get; set; } = string.Empty;
	}

	private sealed class MessageWithDateTime
	{
		public DateTimeOffset Timestamp { get; set; }
	}

	private sealed class MessageWithEnum
	{
		public MessageStatus Status { get; set; }
	}

	private sealed class MessageWithDecimal
	{
		public decimal Amount { get; set; }
	}

	#endregion Test Fixtures
}
