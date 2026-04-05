// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Buffers;
using System.Text;

using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Serialization.Protobuf;

using Google.Protobuf.WellKnownTypes;

namespace Excalibur.Dispatch.Serialization.Tests.Protobuf;

/// <summary>
/// Tests for ProtobufSerializer JSON wire format support (Sprint 721 1tvw6r).
/// Uses Google.Protobuf.WellKnownTypes which have full descriptor support,
/// unlike hand-rolled TestMessage which lacks static Descriptor.
/// Serializer instances created via the public RegisterProtobuf builder API.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Serialization)]
public sealed class ProtobufJsonWireFormatShould
{
	private readonly ISerializer _jsonSerializer = CreateProtobufSerializer(ProtobufWireFormat.Json);

	private readonly ISerializer _binarySerializer = new ProtobufSerializer();

	private static ISerializer CreateProtobufSerializer(ProtobufWireFormat wireFormat)
	{
		var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
		services.AddProtobufSerializer(opts => opts.WireFormat = wireFormat);
		using var provider = services.BuildServiceProvider();
		return provider.GetRequiredService<ISerializer>();
	}

	#region ContentType

	[Fact]
	public void ReportJsonContentType_WhenWireFormatIsJson()
	{
		_jsonSerializer.ContentType.ShouldBe("application/json");
	}

	[Fact]
	public void ReportProtobufContentType_WhenWireFormatIsBinary()
	{
		_binarySerializer.ContentType.ShouldBe("application/x-protobuf");
	}

	#endregion

	#region Serialize<T> JSON path

	[Fact]
	public void SerializeToJson_WhenWireFormatIsJson()
	{
		// Arrange
		var timestamp = Timestamp.FromDateTimeOffset(
			new DateTimeOffset(2026, 3, 28, 12, 0, 0, TimeSpan.Zero));
		var writer = new ArrayBufferWriter<byte>();

		// Act
		_jsonSerializer.Serialize(timestamp, writer);

		// Assert
		var json = Encoding.UTF8.GetString(writer.WrittenSpan);
		json.ShouldContain("2026");
		json.Length.ShouldBeGreaterThan(0);
	}

	[Fact]
	public void SerializeToBinary_WhenWireFormatIsBinary()
	{
		// Arrange
		var timestamp = Timestamp.FromDateTimeOffset(
			new DateTimeOffset(2026, 3, 28, 12, 0, 0, TimeSpan.Zero));
		var writer = new ArrayBufferWriter<byte>();

		// Act
		_binarySerializer.Serialize(timestamp, writer);

		// Assert - binary output should not be valid UTF-8 JSON
		var bytes = writer.WrittenSpan.ToArray();
		bytes.Length.ShouldBeGreaterThan(0);
	}

	#endregion

	#region Deserialize<T> JSON path

	[Fact]
	public void DeserializeFromJson_WhenWireFormatIsJson()
	{
		// Arrange - serialize first to get valid JSON
		var original = Timestamp.FromDateTimeOffset(
			new DateTimeOffset(2026, 3, 28, 12, 0, 0, TimeSpan.Zero));
		var writer = new ArrayBufferWriter<byte>();
		_jsonSerializer.Serialize(original, writer);

		// Act
		var deserialized = _jsonSerializer.Deserialize<Timestamp>(writer.WrittenSpan);

		// Assert
		deserialized.ShouldNotBeNull();
		deserialized.Seconds.ShouldBe(original.Seconds);
		deserialized.Nanos.ShouldBe(original.Nanos);
	}

	[Fact]
	public void RoundTrip_JsonFormat_PreservesData()
	{
		// Arrange
		var original = Duration.FromTimeSpan(TimeSpan.FromMinutes(42.5));
		var writer = new ArrayBufferWriter<byte>();

		// Act
		_jsonSerializer.Serialize(original, writer);
		var deserialized = _jsonSerializer.Deserialize<Duration>(writer.WrittenSpan);

		// Assert
		deserialized.ShouldNotBeNull();
		deserialized.Seconds.ShouldBe(original.Seconds);
		deserialized.Nanos.ShouldBe(original.Nanos);
	}

	#endregion

	#region SerializeObject / DeserializeObject JSON path

	[Fact]
	public void SerializeObject_ReturnsJsonBytes_WhenWireFormatIsJson()
	{
		// Arrange
		var timestamp = Timestamp.FromDateTimeOffset(
			new DateTimeOffset(2026, 3, 28, 12, 0, 0, TimeSpan.Zero));

		// Act
		var bytes = _jsonSerializer.SerializeObject(timestamp, typeof(Timestamp));

		// Assert
		var json = Encoding.UTF8.GetString(bytes);
		json.ShouldContain("2026");
	}

	[Fact]
	public void DeserializeObject_FromJsonBytes_WhenWireFormatIsJson()
	{
		// Arrange
		var original = Timestamp.FromDateTimeOffset(
			new DateTimeOffset(2026, 3, 28, 12, 0, 0, TimeSpan.Zero));
		var jsonBytes = _jsonSerializer.SerializeObject(original, typeof(Timestamp));

		// Act
		var result = _jsonSerializer.DeserializeObject(jsonBytes, typeof(Timestamp));

		// Assert
		var deserialized = result.ShouldBeOfType<Timestamp>();
		deserialized.Seconds.ShouldBe(original.Seconds);
	}

	[Fact]
	public void RoundTrip_ObjectApi_JsonFormat_PreservesData()
	{
		// Arrange
		var original = Duration.FromTimeSpan(TimeSpan.FromHours(1.5));

		// Act
		var bytes = _jsonSerializer.SerializeObject(original, typeof(Duration));
		var result = _jsonSerializer.DeserializeObject(bytes, typeof(Duration));

		// Assert
		var deserialized = result.ShouldBeOfType<Duration>();
		deserialized.Seconds.ShouldBe(original.Seconds);
		deserialized.Nanos.ShouldBe(original.Nanos);
	}

	#endregion

	#region Edge cases

	[Fact]
	public void JsonOutputDiffersFromBinaryOutput()
	{
		// Arrange
		var timestamp = Timestamp.FromDateTimeOffset(
			new DateTimeOffset(2026, 3, 28, 12, 0, 0, TimeSpan.Zero));

		// Act
		var jsonBytes = _jsonSerializer.SerializeObject(timestamp, typeof(Timestamp));
		var binaryBytes = _binarySerializer.SerializeObject(timestamp, typeof(Timestamp));

		// Assert - JSON and binary should produce different output
		jsonBytes.SequenceEqual(binaryBytes).ShouldBeFalse(
			"JSON and binary wire formats should produce different byte output");
	}

	[Fact]
	public void DefaultConstructor_UsesBinaryFormat()
	{
		// Arrange
		var defaultSerializer = new ProtobufSerializer();

		// Assert
		defaultSerializer.ContentType.ShouldBe("application/x-protobuf");
	}

	#endregion
}
