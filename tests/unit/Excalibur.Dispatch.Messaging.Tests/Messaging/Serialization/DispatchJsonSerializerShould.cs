// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable IL2026 // RequiresUnreferencedCode — test code, not AOT published
#pragma warning disable IL3050 // RequiresDynamicCode — test code, not AOT published

using System.Text;

using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Serialization;

namespace Excalibur.Dispatch.Tests.Messaging.Serialization;

/// <summary>
///     Tests for the <see cref="DispatchJsonSerializer" /> class.
/// </summary>
[Trait("Category", "Unit")]
public sealed class DispatchJsonSerializerShould : IDisposable
{
	private readonly DispatchJsonSerializer _sut = new();

	[Fact]
	public void CreateWithDefaultOptions()
	{
		_sut.ShouldNotBeNull();
	}

	[Fact]
	public void CreateWithCustomConfiguration()
	{
		using var serializer = new DispatchJsonSerializer(configure: options =>
		{
			options.WriteIndented = true;
		});

		serializer.ShouldNotBeNull();
	}

	[Fact]
	public void HaveCorrectSerializerName()
	{
		_sut.SerializerName.ShouldBe("DispatchJsonSerializer");
	}

	[Fact]
	public void HaveCorrectSerializerVersion()
	{
		_sut.SerializerVersion.ShouldBe("2.0.0");
	}

	[Fact]
	public void ImplementIMessageSerializer()
	{
		_sut.ShouldBeAssignableTo<IMessageSerializer>();
	}

	[Fact]
	public void ImplementIUtf8JsonSerializer()
	{
		_sut.ShouldBeAssignableTo<IUtf8JsonSerializer>();
	}

	[Fact]
	public void ImplementIDisposable()
	{
		_sut.ShouldBeAssignableTo<IDisposable>();
	}

	[Fact]
	public void SerializeStringValue()
	{
		var result = _sut.Serialize("hello");
		result.ShouldNotBeNullOrWhiteSpace();
		result.ShouldContain("hello");
	}

	[Fact]
	public void DeserializeStringValue()
	{
		var json = "\"hello\"";
		var result = _sut.Deserialize<string>(json);
		result.ShouldBe("hello");
	}

	[Fact]
	public void SerializeToUtf8Bytes()
	{
		var value = "hello world";
		var bytes = _sut.SerializeToUtf8Bytes(value, typeof(string));

		bytes.ShouldNotBeNull();
		bytes.Length.ShouldBeGreaterThan(0);

		var json = Encoding.UTF8.GetString(bytes);
		json.ShouldContain("hello");
	}

	[Fact]
	public void DeserializeFromUtf8Bytes()
	{
		var json = "\"test-value\""u8.ToArray();
		var result = _sut.DeserializeFromUtf8(json.AsSpan(), typeof(string));

		result.ShouldNotBeNull();
		result.ShouldBe("test-value");
	}

	[Fact]
	public void HaveTelemetryInstance()
	{
		_sut.Telemetry.ShouldNotBeNull();
	}

	[Fact]
	public void DisposeWithoutErrors()
	{
		// Create a separate instance for disposal testing
		var serializer = new DispatchJsonSerializer();
		Should.NotThrow(() => serializer.Dispose());
	}

	[Fact]
	public void SerializeAndDeserializeRoundTrip()
	{
		var original = "round-trip-test";
		var json = _sut.Serialize(original);
		json.ShouldNotBeNullOrWhiteSpace();

		var deserialized = _sut.Deserialize<string>(json);
		deserialized.ShouldBe(original);
	}

	[Fact]
	public void SerializeIntValue()
	{
		var result = _sut.Serialize(42);
		result.ShouldBe("42");
	}

	[Fact]
	public void DeserializeIntValue()
	{
		var result = _sut.Deserialize<int>("42");
		result.ShouldBe(42);
	}

	public void Dispose() => _sut.Dispose();
}
