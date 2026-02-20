// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Buffers;
using System.Text;
using System.Text.Json;

using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Patterns.ClaimCheck;

namespace Excalibur.Dispatch.Patterns.Tests.ClaimCheck;

/// <summary>
/// Unit tests for <see cref="JsonClaimCheckSerializer"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Patterns")]
[Trait("Feature", "ClaimCheck")]
public sealed class JsonClaimCheckSerializerShould
{
	private readonly JsonClaimCheckSerializer _serializer;

	public JsonClaimCheckSerializerShould()
	{
		_serializer = new JsonClaimCheckSerializer();
	}

	#region Property Tests

	[Fact]
	public void HaveCorrectSerializerName()
	{
		// Act & Assert
		_serializer.SerializerName.ShouldBe("Json-Abstraction");
	}

	[Fact]
	public void HaveCorrectSerializerVersion()
	{
		// Act & Assert
		_serializer.SerializerVersion.ShouldBe("1.0.0");
	}

	[Fact]
	public void HaveCorrectContentType()
	{
		// Act & Assert
		_serializer.ContentType.ShouldBe("application/json");
	}

	[Fact]
	public void HaveFalseSupportsCompression()
	{
		// Act & Assert
		_serializer.SupportsCompression.ShouldBeFalse();
	}

	[Fact]
	public void HaveCorrectFormat()
	{
		// Act & Assert
		_serializer.Format.ShouldBe("JSON");
	}

	#endregion

	#region Serialize Tests

	[Fact]
	public void SerializeMessage_ToUtf8JsonBytes()
	{
		// Arrange
		var message = new TestMessage { Id = 1, Name = "Test" };

		// Act
		var result = _serializer.Serialize(message);

		// Assert
		var json = Encoding.UTF8.GetString(result);
		json.ShouldContain("\"Id\":1");
		json.ShouldContain("\"Name\":\"Test\"");
	}

	[Fact]
	public void SerializeToBufferWriter_WritesCorrectBytes()
	{
		// Arrange
		var message = new TestMessage { Id = 3, Name = "Buffer" };
		var bufferWriter = new ArrayBufferWriter<byte>();

		// Act
		_serializer.Serialize(message, bufferWriter);

		// Assert
		bufferWriter.WrittenCount.ShouldBeGreaterThan(0);
		var written = Encoding.UTF8.GetString(bufferWriter.WrittenSpan);
		written.ShouldContain("\"Id\":3");
		written.ShouldContain("\"Name\":\"Buffer\"");
	}

	[Fact]
	public async Task SerializeToStreamAsync_WritesToStream()
	{
		// Arrange
		var message = new TestMessage { Id = 5, Name = "Stream" };
		using var stream = new MemoryStream();

		// Act
		await _serializer.SerializeAsync(message, stream, CancellationToken.None);

		// Assert
		stream.Position = 0;
		var reader = new StreamReader(stream);
		var content = await reader.ReadToEndAsync();
		content.ShouldContain("\"Id\":5");
		content.ShouldContain("\"Name\":\"Stream\"");
	}

	#endregion

	#region Deserialize Tests

	[Fact]
	public void DeserializeBytes_ReturnsMessage()
	{
		// Arrange
		var json = "{\"Id\":10,\"Name\":\"Deserialized\"}";
		var bytes = Encoding.UTF8.GetBytes(json);

		// Act
		var result = _serializer.Deserialize<TestMessage>(bytes);

		// Assert
		result.ShouldNotBeNull();
		result.Id.ShouldBe(10);
		result.Name.ShouldBe("Deserialized");
	}

	[Fact]
	public void DeserializeSpan_ReturnsMessage()
	{
		// Arrange
		var json = "{\"Id\":20,\"Name\":\"SpanTest\"}";
		var bytes = Encoding.UTF8.GetBytes(json);

		// Act
		var result = _serializer.Deserialize<TestMessage>((ReadOnlySpan<byte>)bytes);

		// Assert
		result.ShouldNotBeNull();
		result.Id.ShouldBe(20);
		result.Name.ShouldBe("SpanTest");
	}

	[Fact]
	public async Task DeserializeFromStreamAsync_ReturnsMessage()
	{
		// Arrange
		var json = "{\"Id\":50,\"Name\":\"FromStream\"}";
		var bytes = Encoding.UTF8.GetBytes(json);
		using var stream = new MemoryStream(bytes);

		// Act
		var result = await _serializer.DeserializeAsync<TestMessage>(stream, CancellationToken.None);

		// Assert
		result.ShouldNotBeNull();
		result.Id.ShouldBe(50);
		result.Name.ShouldBe("FromStream");
	}

	#endregion

	#region Roundtrip Tests

	[Fact]
	public void Roundtrip_SerializeDeserialize_PreservesMessage()
	{
		// Arrange
		var original = new TestMessage { Id = 42, Name = "Roundtrip" };

		// Act
		var bytes = _serializer.Serialize(original);
		var result = _serializer.Deserialize<TestMessage>(bytes);

		// Assert
		result.ShouldNotBeNull();
		result.Id.ShouldBe(42);
		result.Name.ShouldBe("Roundtrip");
	}

	[Fact]
	public async Task Roundtrip_StreamSerializeDeserialize_PreservesMessage()
	{
		// Arrange
		var original = new TestMessage { Id = 99, Name = "StreamRoundtrip" };
		using var stream = new MemoryStream();

		// Act - serialize to stream, then deserialize from stream
		await _serializer.SerializeAsync(original, stream, CancellationToken.None);
		stream.Position = 0;
		var result = await _serializer.DeserializeAsync<TestMessage>(stream, CancellationToken.None);

		// Assert
		result.ShouldNotBeNull();
		result.Id.ShouldBe(99);
		result.Name.ShouldBe("StreamRoundtrip");
	}

	#endregion

	#region Custom Options Tests

	[Fact]
	public void RespectCustomJsonSerializerOptions()
	{
		// Arrange - use camelCase naming
		var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
		var serializer = new JsonClaimCheckSerializer(options);
		var message = new TestMessage { Id = 7, Name = "CamelCase" };

		// Act
		var bytes = serializer.Serialize(message);
		var json = Encoding.UTF8.GetString(bytes);

		// Assert - property names should be camelCase
		json.ShouldContain("\"id\":7");
		json.ShouldContain("\"name\":\"CamelCase\"");
	}

	[Fact]
	public void DeserializeWithCustomOptions_RespectsCaseInsensitive()
	{
		// Arrange - case-insensitive property matching
		var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
		var serializer = new JsonClaimCheckSerializer(options);
		var json = "{\"id\":8,\"name\":\"CaseInsensitive\"}";
		var bytes = Encoding.UTF8.GetBytes(json);

		// Act
		var result = serializer.Deserialize<TestMessage>(bytes);

		// Assert
		result.ShouldNotBeNull();
		result.Id.ShouldBe(8);
		result.Name.ShouldBe("CaseInsensitive");
	}

	#endregion

	#region Null Guard Tests

	[Fact]
	public async Task SerializeAsync_ThrowsArgumentNullException_WhenStreamIsNull()
	{
		// Arrange
		var message = new TestMessage { Id = 1 };

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(() =>
			_serializer.SerializeAsync(message, null!, CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task DeserializeAsync_ThrowsArgumentNullException_WhenStreamIsNull()
	{
		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(() =>
			_serializer.DeserializeAsync<TestMessage>(null!, CancellationToken.None).AsTask());
	}

	#endregion

	#region Interface Implementation Tests

	[Fact]
	public void ImplementIBinaryMessageSerializer()
	{
		// Assert
		_serializer.ShouldBeAssignableTo<IBinaryMessageSerializer>();
	}

	[Fact]
	public void ImplementIMessageSerializer()
	{
		// Assert
		_serializer.ShouldBeAssignableTo<IMessageSerializer>();
	}

	#endregion

	#region Test Helpers

	private sealed class TestMessage
	{
		public int Id { get; set; }
		public string? Name { get; set; }
	}

	#endregion
}
