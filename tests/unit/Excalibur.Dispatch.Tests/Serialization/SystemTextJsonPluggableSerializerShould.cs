// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text;
using System.Text.Json;

using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Tests.Serialization.TestData;
using Excalibur.Dispatch.Serialization;

namespace Excalibur.Dispatch.Tests.Serialization;

/// <summary>
/// Unit tests for <see cref="SystemTextJsonPluggableSerializer"/> validating serialization,
/// deserialization, and error handling behavior.
/// </summary>
[Trait("Category", "Unit")]
public sealed class SystemTextJsonPluggableSerializerShould
{
	private readonly SystemTextJsonPluggableSerializer _sut = new();

	#region Property Tests

	[Fact]
	public void Name_ReturnsExpectedName()
	{
		// Assert
		_sut.Name.ShouldBe("System.Text.Json");
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
	public void Serialize_WithComplexObject_ReturnsValidJson()
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
		var json = Encoding.UTF8.GetString(result);

		// Assert
		result.ShouldNotBeEmpty();
		json.ShouldContain("complex-1");
		json.ShouldContain("nested");
		json.ShouldContain("tags");
	}

	[Fact]
	public void Serialize_UsesCamelCaseNamingByDefault()
	{
		// Arrange
		var message = new TestMessage { Name = "Test", Value = 42 };

		// Act
		var result = _sut.Serialize(message);
		var json = Encoding.UTF8.GetString(result);

		// Assert - Should use camelCase
		json.ShouldContain("\"name\"");
		json.ShouldContain("\"value\"");
		json.ShouldContain("\"timestamp\"");
	}

	[Fact]
	public void Serialize_ProducesCompactJsonByDefault()
	{
		// Arrange
		var message = new TestMessage { Name = "Test", Value = 42 };

		// Act
		var result = _sut.Serialize(message);
		var json = Encoding.UTF8.GetString(result);

		// Assert - Should not have newlines (compact)
		json.ShouldNotContain("\n");
		json.ShouldNotContain("\r");
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
		var corruptedData = Encoding.UTF8.GetBytes("{invalid json}");

		// Act & Assert
		var ex = Should.Throw<SerializationException>(() =>
			_sut.Deserialize<TestMessage>(corruptedData.AsSpan()));
		ex.Message.ShouldContain("deserialize");
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

	#region Custom Options Tests

	[Fact]
	public void Constructor_WithCustomOptions_UsesProvidedOptions()
	{
		// Arrange
		var customOptions = new JsonSerializerOptions
		{
			PropertyNamingPolicy = null, // PascalCase
			WriteIndented = true
		};
		var sut = new SystemTextJsonPluggableSerializer(customOptions);
		var message = new TestMessage { Name = "Test", Value = 42 };

		// Act
		var result = sut.Serialize(message);
		var json = Encoding.UTF8.GetString(result);

		// Assert - Should use PascalCase and be indented
		json.ShouldContain("\"Name\"");
		json.ShouldContain("\"Value\"");
		json.ShouldContain("\n"); // Indented
	}

	[Fact]
	public void Constructor_WithNullOptions_UsesDefaultOptions()
	{
		// Arrange
		var sut = new SystemTextJsonPluggableSerializer(null);
		var message = new TestMessage { Name = "Test", Value = 42 };

		// Act
		var result = sut.Serialize(message);
		var json = Encoding.UTF8.GetString(result);

		// Assert - Should use defaults (camelCase, compact)
		json.ShouldContain("\"name\"");
		json.ShouldNotContain("\n");
	}

	#endregion Custom Options Tests

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
		// DateTime comparison with tolerance for JSON round-trip
		(result.Timestamp - original.Timestamp).Duration().ShouldBeLessThan(TimeSpan.FromMilliseconds(1));
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

	[Fact]
	public void RoundTrip_PreservesUnicodeCharacters()
	{
		// Arrange
		var original = new TestMessage
		{
			Name = "日本語テスト 中文测试 한국어테스트",
			Value = 12345
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

	#region SerializeObject Tests

	[Fact]
	public void SerializeObject_WithValidObject_ReturnsNonEmptyArray()
	{
		// Arrange
		var message = new TestMessage { Name = "Test", Value = 42 };

		// Act
		var result = _sut.SerializeObject(message, typeof(TestMessage));

		// Assert
		result.ShouldNotBeEmpty();
	}

	[Fact]
	public void SerializeObject_WithNullValue_ThrowsArgumentNullException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			_sut.SerializeObject(null!, typeof(TestMessage)));
	}

	[Fact]
	public void SerializeObject_WithNullType_ThrowsArgumentNullException()
	{
		// Arrange
		var message = new TestMessage { Name = "Test", Value = 42 };

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			_sut.SerializeObject(message, null!));
	}

	[Fact]
	public void SerializeObject_ProducesSameOutputAsGenericSerialize()
	{
		// Arrange
		var message = new TestMessage { Name = "Test", Value = 42 };

		// Act
		var genericResult = _sut.Serialize(message);
		var objectResult = _sut.SerializeObject(message, typeof(TestMessage));

		// Assert
		objectResult.ShouldBe(genericResult);
	}

	#endregion SerializeObject Tests

	#region DeserializeObject Tests

	[Fact]
	public void DeserializeObject_WithValidData_ReturnsOriginalObject()
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
		var result = _sut.DeserializeObject(serialized, typeof(TestMessage));

		// Assert
		_ = result.ShouldBeOfType<TestMessage>();
		var typedResult = (TestMessage)result;
		typedResult.Name.ShouldBe(original.Name);
		typedResult.Value.ShouldBe(original.Value);
	}

	[Fact]
	public void DeserializeObject_WithNullType_ThrowsArgumentNullException()
	{
		// Arrange
		var data = _sut.Serialize(new TestMessage { Name = "Test" });

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			_sut.DeserializeObject(data, null!));
	}

	[Fact]
	public void DeserializeObject_WithCorruptedData_ThrowsSerializationException()
	{
		// Arrange
		var corruptedData = Encoding.UTF8.GetBytes("{invalid json}");

		// Act & Assert
		var ex = Should.Throw<SerializationException>(() =>
			_sut.DeserializeObject(corruptedData.AsSpan(), typeof(TestMessage)));
		ex.Message.ShouldContain("deserialize");
	}

	[Fact]
	public void DeserializeObject_WithEmptyData_ThrowsSerializationException()
	{
		// Arrange
		var emptyData = Array.Empty<byte>();

		// Act & Assert
		_ = Should.Throw<SerializationException>(() =>
			_sut.DeserializeObject(emptyData.AsSpan(), typeof(TestMessage)));
	}

	[Fact]
	public void DeserializeObject_RoundTrip_PreservesComplexObject()
	{
		// Arrange
		var original = new ComplexTestMessage
		{
			Id = "object-round-trip",
			Nested = new TestMessage { Name = "Nested", Value = 999 },
			Tags = ["alpha", "beta"],
			Metadata = new Dictionary<string, int> { ["key"] = 42 }
		};
		var serialized = _sut.SerializeObject(original, typeof(ComplexTestMessage));

		// Act
		var result = _sut.DeserializeObject(serialized, typeof(ComplexTestMessage));

		// Assert
		_ = result.ShouldBeOfType<ComplexTestMessage>();
		var typedResult = (ComplexTestMessage)result;
		typedResult.Id.ShouldBe(original.Id);
		_ = typedResult.Nested.ShouldNotBeNull();
		typedResult.Nested.Value.ShouldBe(999);
	}

	#endregion DeserializeObject Tests

	#region DateTime Edge Cases

	[Fact]
	public void RoundTrip_PreservesDateTimeOffset()
	{
		// Arrange
		var original = new DateTimeOffsetMessage
		{
			Created = new DateTimeOffset(2025, 6, 15, 14, 30, 0, TimeSpan.FromHours(5)),
			Modified = DateTimeOffset.UtcNow
		};

		// Act
		var serialized = _sut.Serialize(original);
		var result = _sut.Deserialize<DateTimeOffsetMessage>(serialized);

		// Assert
		result.Created.ShouldBe(original.Created);
		(result.Modified - original.Modified).Duration().ShouldBeLessThan(TimeSpan.FromMilliseconds(1));
	}

	[Fact]
	public void RoundTrip_PreservesMinMaxDateTime()
	{
		// Arrange
		var original = new TestMessage
		{
			Name = "MinMaxTest",
			Value = 0,
			Timestamp = DateTime.MaxValue.AddTicks(-1) // Max minus 1 tick to avoid overflow
		};

		// Act
		var serialized = _sut.Serialize(original);
		var result = _sut.Deserialize<TestMessage>(serialized);

		// Assert
		result.Timestamp.Year.ShouldBe(original.Timestamp.Year);
	}

	#endregion DateTime Edge Cases

	#region Enum Serialization Tests

	[Fact]
	public void RoundTrip_PreservesEnumValues()
	{
		// Arrange
		var original = new EnumMessage
		{
			Status = TestStatus.Active,
			Priority = TestPriority.High
		};

		// Act
		var serialized = _sut.Serialize(original);
		var result = _sut.Deserialize<EnumMessage>(serialized);

		// Assert
		result.Status.ShouldBe(TestStatus.Active);
		result.Priority.ShouldBe(TestPriority.High);
	}

	[Fact]
	public void Serialize_EnumValues_SerializesAsNumbers()
	{
		// Arrange
		var message = new EnumMessage
		{
			Status = TestStatus.Inactive,
			Priority = TestPriority.Low
		};

		// Act
		var serialized = _sut.Serialize(message);
		var json = Encoding.UTF8.GetString(serialized);

		// Assert - Default STJ serializes enums as numbers
		json.ShouldContain("\"status\":0"); // Inactive = 0
		json.ShouldContain("\"priority\":0"); // Low = 0
	}

	#endregion Enum Serialization Tests

	#region Boundary Value Tests

	[Fact]
	public void RoundTrip_PreservesIntegerBoundaryValues()
	{
		// Arrange
		var original = new BoundaryValuesMessage
		{
			MinInt = int.MinValue,
			MaxInt = int.MaxValue,
			MinLong = long.MinValue,
			MaxLong = long.MaxValue
		};

		// Act
		var serialized = _sut.Serialize(original);
		var result = _sut.Deserialize<BoundaryValuesMessage>(serialized);

		// Assert
		result.MinInt.ShouldBe(int.MinValue);
		result.MaxInt.ShouldBe(int.MaxValue);
		result.MinLong.ShouldBe(long.MinValue);
		result.MaxLong.ShouldBe(long.MaxValue);
	}

	[Fact]
	public void RoundTrip_PreservesDecimalPrecision()
	{
		// Arrange
		var original = new DecimalMessage
		{
			Price = 123456789.123456789m,
			Quantity = decimal.MaxValue / 2
		};

		// Act
		var serialized = _sut.Serialize(original);
		var result = _sut.Deserialize<DecimalMessage>(serialized);

		// Assert
		result.Price.ShouldBe(original.Price);
		result.Quantity.ShouldBe(original.Quantity);
	}

	#endregion Boundary Value Tests

	#region Test Fixtures

	private enum TestStatus
	{
		Inactive = 0,
		Active = 1,
		Suspended = 2
	}

	private enum TestPriority
	{
		Low = 0,
		Medium = 1,
		High = 2
	}

	private sealed class DateTimeOffsetMessage
	{
		public DateTimeOffset Created { get; init; }
		public DateTimeOffset Modified { get; init; }
	}

	private sealed class EnumMessage
	{
		public TestStatus Status { get; init; }
		public TestPriority Priority { get; init; }
	}

	private sealed class BoundaryValuesMessage
	{
		public int MinInt { get; init; }
		public int MaxInt { get; init; }
		public long MinLong { get; init; }
		public long MaxLong { get; init; }
	}

	private sealed class DecimalMessage
	{
		public decimal Price { get; init; }
		public decimal Quantity { get; init; }
	}

	#endregion Test Fixtures
}
