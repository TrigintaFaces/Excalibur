// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Serialization.MemoryPack;
using Excalibur.Dispatch.Tests.Serialization.TestData;

namespace Excalibur.Dispatch.Tests.Serialization;

/// <summary>
/// Unit tests for <see cref="MemoryPackPluggableSerializer"/> validating serialization,
/// deserialization, and error handling behavior.
/// </summary>
[Trait("Category", "Unit")]
public sealed class MemoryPackPluggableSerializerShould
{
	private readonly MemoryPackPluggableSerializer _sut = new();

	#region Property Tests

	[Fact]
	public void Name_ReturnsExpectedName()
	{
		// Assert
		_sut.Name.ShouldBe("MemoryPack");
	}

	[Fact]
	public void Version_ReturnsNonNullVersion()
	{
		// Assert
		_sut.Version.ShouldNotBeNullOrEmpty();
	}

	#endregion Property Tests

	#region Serialize Tests

	[Fact]
	public void Serialize_WithValidObject_ReturnsNonEmptyArray()
	{
		// Arrange
		var message = new TestMessage { Name = "Test", Value = 42 };

		// Act
		var result = _sut.Serialize(message);

		// Assert
		result.ShouldNotBeEmpty();
	}

	[Fact]
	public void Serialize_WithNull_ThrowsArgumentNullException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			_sut.Serialize<TestMessage>(null!));
	}

	[Fact]
	public void Serialize_WithComplexObject_ReturnsNonEmptyArray()
	{
		// Arrange
		var message = new ComplexTestMessage
		{
			Id = "complex-1",
			Nested = new TestMessage { Name = "Nested", Value = 100 },
			Tags = ["tag1", "tag2", "tag3"],
			Metadata = new Dictionary<string, int>
			{
				["key1"] = 1,
				["key2"] = 2
			}
		};

		// Act
		var result = _sut.Serialize(message);

		// Assert
		result.ShouldNotBeEmpty();
		result.Length.ShouldBeGreaterThan(10); // Should have reasonable size
	}

	#endregion Serialize Tests

	#region Deserialize Tests

	[Fact]
	public void Deserialize_WithValidData_ReturnsOriginalObject()
	{
		// Arrange
		var original = new TestMessage
		{
			Name = "TestMessage",
			Value = 12345,
			Timestamp = new DateTime(2025, 1, 15, 10, 30, 0, DateTimeKind.Utc)
		};
		var serialized = _sut.Serialize(original);

		// Act
		var result = _sut.Deserialize<TestMessage>(serialized);

		// Assert
		result.Name.ShouldBe(original.Name);
		result.Value.ShouldBe(original.Value);
		result.Timestamp.ShouldBe(original.Timestamp);
	}

	[Fact]
	public void Deserialize_WithCorruptedData_ThrowsSerializationException()
	{
		// Arrange
		var corruptedData = new byte[] { 0xFF, 0xFF, 0xFF, 0xFF };

		// Act & Assert
		var ex = Should.Throw<SerializationException>(() =>
			_sut.Deserialize<TestMessage>(corruptedData.AsSpan()));
		// Message may be "Deserialization returned null" or "Failed to deserialize"
		(ex.Message.Contains("Deserialization", StringComparison.OrdinalIgnoreCase) ||
		 ex.Message.Contains("deserialize", StringComparison.OrdinalIgnoreCase)).ShouldBeTrue();
	}

	[Fact]
	public void Deserialize_WithEmptyData_ThrowsSerializationException()
	{
		// Arrange
		var emptyData = Array.Empty<byte>();

		// Act & Assert
		var ex = Should.Throw<SerializationException>(() =>
			_sut.Deserialize<TestMessage>(emptyData.AsSpan()));
	}

	[Fact]
	public void Deserialize_WithComplexObject_ReturnsOriginalValue()
	{
		// Arrange
		var original = new ComplexTestMessage
		{
			Id = "complex-round-trip",
			Nested = new TestMessage { Name = "Nested", Value = 999 },
			Tags = ["alpha", "beta", "gamma"],
			Metadata = new Dictionary<string, int>
			{
				["count"] = 42,
				["priority"] = 1
			}
		};
		var serialized = _sut.Serialize(original);

		// Act
		var result = _sut.Deserialize<ComplexTestMessage>(serialized);

		// Assert
		result.Id.ShouldBe(original.Id);
		_ = result.Nested.ShouldNotBeNull();
		result.Nested.Name.ShouldBe("Nested");
		result.Nested.Value.ShouldBe(999);
		result.Tags.ShouldBe(original.Tags);
		result.Metadata.ShouldBe(original.Metadata);
	}

	#endregion Deserialize Tests

	#region Round-Trip Tests

	[Fact]
	public void RoundTrip_PreservesSimpleValues()
	{
		// Arrange
		var original = new TestMessage
		{
			Name = "Simple",
			Value = int.MaxValue,
			Timestamp = DateTime.UtcNow
		};

		// Act
		var serialized = _sut.Serialize(original);
		var result = _sut.Deserialize<TestMessage>(serialized);

		// Assert
		result.Name.ShouldBe(original.Name);
		result.Value.ShouldBe(original.Value);
		result.Timestamp.ShouldBe(original.Timestamp);
	}

	[Fact]
	public void RoundTrip_PreservesEmptyStrings()
	{
		// Arrange
		var original = new TestMessage
		{
			Name = string.Empty,
			Value = 0,
			Timestamp = DateTime.MinValue
		};

		// Act
		var serialized = _sut.Serialize(original);
		var result = _sut.Deserialize<TestMessage>(serialized);

		// Assert
		result.Name.ShouldBe(string.Empty);
		result.Value.ShouldBe(0);
	}

	[Fact]
	public void RoundTrip_PreservesEmptyCollections()
	{
		// Arrange
		var original = new ComplexTestMessage
		{
			Id = "empty-collections",
			Nested = null,
			Tags = [],
			Metadata = []
		};

		// Act
		var serialized = _sut.Serialize(original);
		var result = _sut.Deserialize<ComplexTestMessage>(serialized);

		// Assert
		result.Id.ShouldBe(original.Id);
		result.Nested.ShouldBeNull();
		result.Tags.ShouldBeEmpty();
		result.Metadata.ShouldBeEmpty();
	}

	[Fact]
	public void RoundTrip_PreservesSpecialCharacters()
	{
		// Arrange
		var original = new TestMessage
		{
			Name = "Test with émojis 🎉 and spëcial çharacters",
			Value = 42
		};

		// Act
		var serialized = _sut.Serialize(original);
		var result = _sut.Deserialize<TestMessage>(serialized);

		// Assert
		result.Name.ShouldBe(original.Name);
	}

	#endregion Round-Trip Tests

	#region Thread-Safety Tests

	[Fact]
	public async Task Serialize_ConcurrentCalls_AllSucceed()
	{
		// Arrange
		var tasks = new List<Task<byte[]>>();
		var messages = Enumerable.Range(0, 100)
			.Select(i => new TestMessage { Name = $"Message{i}", Value = i })
			.ToList();

		// Act
		foreach (var message in messages)
		{
			tasks.Add(Task.Run(() => _sut.Serialize(message)));
		}

		var results = await Task.WhenAll(tasks);

		// Assert
		results.Length.ShouldBe(100);
		results.All(r => r.Length > 0).ShouldBeTrue();
	}

	[Fact]
	public async Task Deserialize_ConcurrentCalls_AllSucceed()
	{
		// Arrange
		var messages = Enumerable.Range(0, 100)
			.Select(i => new TestMessage { Name = $"Message{i}", Value = i })
			.ToList();
		var serialized = messages.Select(m => _sut.Serialize(m)).ToList();
		var tasks = new List<Task<TestMessage>>();

		// Act
		foreach (var data in serialized)
		{
			var dataCopy = data; // Capture for closure
			tasks.Add(Task.Run(() => _sut.Deserialize<TestMessage>(dataCopy)));
		}

		var results = await Task.WhenAll(tasks);

		// Assert
		results.Length.ShouldBe(100);
		for (var i = 0; i < results.Length; i++)
		{
			results[i].Name.ShouldBe($"Message{i}");
			results[i].Value.ShouldBe(i);
		}
	}

	#endregion Thread-Safety Tests
}
