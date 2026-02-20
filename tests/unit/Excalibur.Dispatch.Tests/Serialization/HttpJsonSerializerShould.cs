// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text;
using System.Text.Json;

using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Tests.Serialization.TestData;
using Excalibur.Dispatch.Serialization;

namespace Excalibur.Dispatch.Tests.Serialization;

/// <summary>
/// Unit tests for <see cref="HttpJsonSerializer"/> validating HTTP JSON serialization behavior.
/// </summary>
[Trait("Category", "Unit")]
public sealed class HttpJsonSerializerShould
{
	#region Constructor Tests

	[Fact]
	public void CreateWithDefaultOptions()
	{
		// Act
		var serializer = new HttpJsonSerializer();

		// Assert - should not throw and should work
		var bytes = serializer.Serialize("test");
		bytes.ShouldNotBeEmpty();
	}

	[Fact]
	public void ThrowArgumentNullException_WhenOptionsIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(
			() => new HttpJsonSerializer(null!))
			.ParamName.ShouldBe("options");
	}

	[Fact]
	public void AcceptCustomOptions()
	{
		// Arrange
		var options = new JsonSerializerOptions
		{
			PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
			WriteIndented = true
		};

		// Act
		var serializer = new HttpJsonSerializer(options);
		var bytes = serializer.Serialize(new HttpTestMessage { UserName = "John" });
		var json = Encoding.UTF8.GetString(bytes);

		// Assert - snake_case should be used
		json.ShouldContain("user_name");
		json.ShouldNotContain("userName");
	}

	#endregion Constructor Tests

	#region Synchronous Serialize Tests

	[Fact]
	public void Serialize_GenericType_ReturnsValidJson()
	{
		// Arrange
		var serializer = new HttpJsonSerializer();
		var message = new HttpTestMessage { UserName = "Alice", Age = 30 };

		// Act
		var bytes = serializer.Serialize(message);
		var json = Encoding.UTF8.GetString(bytes);

		// Assert
		json.ShouldContain("\"userName\"");
		json.ShouldContain("\"Alice\"");
		json.ShouldContain("\"age\"");
		json.ShouldContain("30");
	}

	[Fact]
	public void Serialize_WithRuntimeType_ReturnsValidJson()
	{
		// Arrange
		var serializer = new HttpJsonSerializer();
		object message = new HttpTestMessage { UserName = "Bob", Age = 25 };

		// Act
		var bytes = serializer.Serialize(message, typeof(HttpTestMessage));
		var json = Encoding.UTF8.GetString(bytes);

		// Assert
		json.ShouldContain("\"userName\"");
		json.ShouldContain("\"Bob\"");
	}

	[Fact]
	public void Serialize_WithRuntimeType_ThrowsArgumentNullException_WhenTypeIsNull()
	{
		// Arrange
		var serializer = new HttpJsonSerializer();

		// Act & Assert
		Should.Throw<ArgumentNullException>(
			() => serializer.Serialize(new HttpTestMessage(), null!))
			.ParamName.ShouldBe("type");
	}

	[Fact]
	public void Serialize_WithRuntimeType_ThrowsArgumentNullException_WhenValueIsNull()
	{
		// Arrange
		var serializer = new HttpJsonSerializer();

		// Act & Assert
		Should.Throw<ArgumentNullException>(
			() => serializer.Serialize(null!, typeof(HttpTestMessage)))
			.ParamName.ShouldBe("value");
	}

	[Fact]
	public void Serialize_NullValue_ReturnsNullJson()
	{
		// Arrange
		var serializer = new HttpJsonSerializer();

		// Act
		var bytes = serializer.Serialize<HttpTestMessage?>(null);
		var json = Encoding.UTF8.GetString(bytes);

		// Assert
		json.ShouldBe("null");
	}

	[Fact]
	public void Serialize_UsesDefaultCamelCase()
	{
		// Arrange
		var serializer = new HttpJsonSerializer();
		var message = new HttpTestMessage { UserName = "Test" };

		// Act
		var bytes = serializer.Serialize(message);
		var json = Encoding.UTF8.GetString(bytes);

		// Assert - use case-sensitive comparison to verify camelCase
		json.ShouldContain("\"userName\""); // camelCase with quotes for exact match
		json.ShouldNotContain("\"UserName\"", Case.Sensitive); // Not PascalCase
	}

	[Fact]
	public void Serialize_OmitsNullProperties()
	{
		// Arrange
		var serializer = new HttpJsonSerializer();
		var message = new HttpTestMessage { UserName = "Test", NullableField = null };

		// Act
		var bytes = serializer.Serialize(message);
		var json = Encoding.UTF8.GetString(bytes);

		// Assert
		json.ShouldNotContain("nullableField");
	}

	#endregion Synchronous Serialize Tests

	#region Synchronous Deserialize Tests

	[Fact]
	public void Deserialize_ValidJson_ReturnsObject()
	{
		// Arrange
		var serializer = new HttpJsonSerializer();
		var json = "{\"userName\":\"Charlie\",\"age\":35}"u8;

		// Act
		var result = serializer.Deserialize<HttpTestMessage>(json);

		// Assert
		_ = result.ShouldNotBeNull();
		result.UserName.ShouldBe("Charlie");
		result.Age.ShouldBe(35);
	}

	[Fact]
	public void Deserialize_CaseInsensitive()
	{
		// Arrange
		var serializer = new HttpJsonSerializer();
		var json = "{\"USERNAME\":\"Test\",\"AGE\":40}"u8;

		// Act
		var result = serializer.Deserialize<HttpTestMessage>(json);

		// Assert
		_ = result.ShouldNotBeNull();
		result.UserName.ShouldBe("Test");
		result.Age.ShouldBe(40);
	}

	[Fact]
	public void Deserialize_WithRuntimeType_ReturnsObject()
	{
		// Arrange
		var serializer = new HttpJsonSerializer();
		var json = "{\"userName\":\"Dave\",\"age\":45}"u8;

		// Act
#pragma warning disable CA2263 // Prefer generic overload - testing runtime type overload
		var result = serializer.Deserialize(json, typeof(HttpTestMessage));
#pragma warning restore CA2263

		// Assert
		_ = result.ShouldNotBeNull();
		_ = result.ShouldBeOfType<HttpTestMessage>();
		var message = (HttpTestMessage)result;
		message.UserName.ShouldBe("Dave");
		message.Age.ShouldBe(45);
	}

	[Fact]
	public void Deserialize_WithRuntimeType_ThrowsArgumentNullException_WhenTypeIsNull()
	{
		// Arrange
		var serializer = new HttpJsonSerializer();
		var jsonBytes = "{}"u8.ToArray();

		// Act & Assert
		Should.Throw<ArgumentNullException>(
			() => serializer.Deserialize(jsonBytes, null!))
			.ParamName.ShouldBe("type");
	}

	[Fact]
	public void Deserialize_NullJson_ReturnsNull()
	{
		// Arrange
		var serializer = new HttpJsonSerializer();
		var json = "null"u8;

		// Act
		var result = serializer.Deserialize<HttpTestMessage?>(json);

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void Deserialize_InvalidJson_ThrowsSerializationException()
	{
		// Arrange
		var serializer = new HttpJsonSerializer();
		var invalidJson = Encoding.UTF8.GetBytes("not valid json");

		// Act & Assert
		var ex = Should.Throw<SerializationException>(
			() => serializer.Deserialize<HttpTestMessage>(invalidJson));
		ex.Operation.ShouldBe(SerializationOperation.Deserialize);
		ex.SerializerName.ShouldBe("System.Text.Json");
	}

	#endregion Synchronous Deserialize Tests

	#region Async Stream Serialize Tests

	[Fact]
	public async Task SerializeAsync_WritesToStream()
	{
		// Arrange
		var serializer = new HttpJsonSerializer();
		var message = new HttpTestMessage { UserName = "Eve", Age = 28 };
		using var stream = new MemoryStream();

		// Act
		await serializer.SerializeAsync(stream, message, CancellationToken.None);

		// Assert
		stream.Position = 0;
		using var reader = new StreamReader(stream);
		var json = await reader.ReadToEndAsync();
		json.ShouldContain("\"userName\"");
		json.ShouldContain("\"Eve\"");
	}

	[Fact]
	public async Task SerializeAsync_ThrowsArgumentNullException_WhenStreamIsNull()
	{
		// Arrange
		var serializer = new HttpJsonSerializer();

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(async () =>
			await serializer.SerializeAsync(null!, new HttpTestMessage(), CancellationToken.None));
	}

	[Fact]
	public async Task SerializeAsync_WithRuntimeType_WritesToStream()
	{
		// Arrange
		var serializer = new HttpJsonSerializer();
		object message = new HttpTestMessage { UserName = "Frank", Age = 32 };
		using var stream = new MemoryStream();

		// Act
		await serializer.SerializeAsync(stream, message, typeof(HttpTestMessage), CancellationToken.None);

		// Assert
		stream.Position = 0;
		using var reader = new StreamReader(stream);
		var json = await reader.ReadToEndAsync();
		json.ShouldContain("\"userName\"");
		json.ShouldContain("\"Frank\"");
	}

	[Fact]
	public async Task SerializeAsync_WithRuntimeType_ThrowsArgumentNullException_WhenStreamIsNull()
	{
		// Arrange
		var serializer = new HttpJsonSerializer();

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(async () =>
			await serializer.SerializeAsync(null!, new HttpTestMessage(), typeof(HttpTestMessage), CancellationToken.None));
	}

	[Fact]
	public async Task SerializeAsync_WithRuntimeType_ThrowsArgumentNullException_WhenTypeIsNull()
	{
		// Arrange
		var serializer = new HttpJsonSerializer();
		using var stream = new MemoryStream();

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(async () =>
			await serializer.SerializeAsync(stream, new HttpTestMessage(), null!, CancellationToken.None));
	}

	[Fact]
	public async Task SerializeAsync_WithRuntimeType_ThrowsArgumentNullException_WhenValueIsNull()
	{
		// Arrange
		var serializer = new HttpJsonSerializer();
		using var stream = new MemoryStream();

		// Act & Assert
		var ex = await Should.ThrowAsync<ArgumentNullException>(async () =>
			await serializer.SerializeAsync(stream, null!, typeof(HttpTestMessage), CancellationToken.None));
		ex.ParamName.ShouldBe("value");
	}

	[Fact]
	public async Task SerializeAsync_RespectsCancellationToken()
	{
		// Arrange
		var serializer = new HttpJsonSerializer();
		using var stream = new MemoryStream();
		using var cts = new CancellationTokenSource();
		cts.Cancel();

		// Act & Assert
		_ = await Should.ThrowAsync<OperationCanceledException>(async () =>
			await serializer.SerializeAsync(stream, new HttpTestMessage(), cts.Token));
	}

	#endregion Async Stream Serialize Tests

	#region Async Stream Deserialize Tests

	[Fact]
	public async Task DeserializeAsync_ReadsFromStream()
	{
		// Arrange
		var serializer = new HttpJsonSerializer();
		var json = "{\"userName\":\"Grace\",\"age\":29}";
		using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

		// Act
		var result = await serializer.DeserializeAsync<HttpTestMessage>(stream, CancellationToken.None);

		// Assert
		_ = result.ShouldNotBeNull();
		result.UserName.ShouldBe("Grace");
		result.Age.ShouldBe(29);
	}

	[Fact]
	public async Task DeserializeAsync_ThrowsArgumentNullException_WhenStreamIsNull()
	{
		// Arrange
		var serializer = new HttpJsonSerializer();

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(async () =>
			await serializer.DeserializeAsync<HttpTestMessage>(null!, CancellationToken.None));
	}

	[Fact]
	public async Task DeserializeAsync_WithRuntimeType_ReadsFromStream()
	{
		// Arrange
		var serializer = new HttpJsonSerializer();
		var json = "{\"userName\":\"Henry\",\"age\":42}";
		using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

		// Act
		var result = await serializer.DeserializeAsync(stream, typeof(HttpTestMessage), CancellationToken.None);

		// Assert
		_ = result.ShouldNotBeNull();
		_ = result.ShouldBeOfType<HttpTestMessage>();
		var message = (HttpTestMessage)result;
		message.UserName.ShouldBe("Henry");
		message.Age.ShouldBe(42);
	}

	[Fact]
	public async Task DeserializeAsync_WithRuntimeType_ThrowsArgumentNullException_WhenStreamIsNull()
	{
		// Arrange
		var serializer = new HttpJsonSerializer();

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(async () =>
			await serializer.DeserializeAsync(null!, typeof(HttpTestMessage), CancellationToken.None));
	}

	[Fact]
	public async Task DeserializeAsync_WithRuntimeType_ThrowsArgumentNullException_WhenTypeIsNull()
	{
		// Arrange
		var serializer = new HttpJsonSerializer();
		using var stream = new MemoryStream("{}"u8.ToArray());

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(async () =>
			await serializer.DeserializeAsync(stream, null!, CancellationToken.None));
	}

	[Fact]
	public async Task DeserializeAsync_RespectsCancellationToken()
	{
		// Arrange
		var serializer = new HttpJsonSerializer();
		var json = "{\"userName\":\"Test\"}";
		using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
		using var cts = new CancellationTokenSource();
		cts.Cancel();

		// Act & Assert
		_ = await Should.ThrowAsync<OperationCanceledException>(async () =>
			await serializer.DeserializeAsync<HttpTestMessage>(stream, cts.Token));
	}

	[Fact]
	public async Task DeserializeAsync_InvalidJson_ThrowsSerializationException()
	{
		// Arrange
		var serializer = new HttpJsonSerializer();
		var invalidJson = "invalid json content";
		using var stream = new MemoryStream(Encoding.UTF8.GetBytes(invalidJson));

		// Act & Assert
		var ex = await Should.ThrowAsync<SerializationException>(async () =>
			await serializer.DeserializeAsync<HttpTestMessage>(stream, CancellationToken.None));
		ex.Operation.ShouldBe(SerializationOperation.Deserialize);
		ex.SerializerName.ShouldBe("System.Text.Json");
	}

	#endregion Async Stream Deserialize Tests

	#region Round-Trip Tests

	[Fact]
	public void RoundTrip_Synchronous_PreservesData()
	{
		// Arrange
		var serializer = new HttpJsonSerializer();
		var original = new HttpTestMessage
		{
			UserName = "RoundTrip",
			Age = 100,
			NullableField = "HasValue"
		};

		// Act
		var bytes = serializer.Serialize(original);
		var deserialized = serializer.Deserialize<HttpTestMessage>(bytes);

		// Assert
		_ = deserialized.ShouldNotBeNull();
		deserialized.UserName.ShouldBe(original.UserName);
		deserialized.Age.ShouldBe(original.Age);
		deserialized.NullableField.ShouldBe(original.NullableField);
	}

	[Fact]
	public async Task RoundTrip_Async_PreservesData()
	{
		// Arrange
		var serializer = new HttpJsonSerializer();
		var original = new HttpTestMessage
		{
			UserName = "AsyncRoundTrip",
			Age = 200,
			NullableField = "AsyncValue"
		};
		using var writeStream = new MemoryStream();

		// Act - serialize
		await serializer.SerializeAsync(writeStream, original, CancellationToken.None);
		writeStream.Position = 0;

		// Act - deserialize
		var deserialized = await serializer.DeserializeAsync<HttpTestMessage>(writeStream, CancellationToken.None);

		// Assert
		_ = deserialized.ShouldNotBeNull();
		deserialized.UserName.ShouldBe(original.UserName);
		deserialized.Age.ShouldBe(original.Age);
		deserialized.NullableField.ShouldBe(original.NullableField);
	}

	#endregion Round-Trip Tests

	#region Exception Wrapping Tests

	[Fact]
	public void Serialize_WrapsJsonExceptionInSerializationException()
	{
		// Arrange - create options that will fail
		var options = new JsonSerializerOptions();
		options.Converters.Add(new FailingJsonConverter());
		var serializer = new HttpJsonSerializer(options);

		// Act & Assert
		var ex = Should.Throw<SerializationException>(
			() => serializer.Serialize(new HttpTestMessage()));
		ex.Operation.ShouldBe(SerializationOperation.Serialize);
		ex.TargetType.ShouldBe(typeof(HttpTestMessage));
		ex.SerializerId.ShouldBe(SerializerIds.SystemTextJson);
		ex.SerializerName.ShouldBe("System.Text.Json");
		_ = ex.InnerException.ShouldNotBeNull();
	}

	/// <summary>
	/// Converter that always throws to test exception wrapping.
	/// </summary>
	private sealed class FailingJsonConverter : System.Text.Json.Serialization.JsonConverter<HttpTestMessage>
	{
		public override HttpTestMessage? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
			=> throw new JsonException("Intentional failure");

		public override void Write(Utf8JsonWriter writer, HttpTestMessage value, JsonSerializerOptions options)
			=> throw new JsonException("Intentional failure");
	}

	#endregion Exception Wrapping Tests
}
