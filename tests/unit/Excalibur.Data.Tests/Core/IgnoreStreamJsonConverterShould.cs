// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text;
using System.Text.Json;

using Excalibur.Data.Serialization;

namespace Excalibur.Data.Tests.Core;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class IgnoreStreamJsonConverterShould
{
	private readonly JsonSerializerOptions _options;

	public IgnoreStreamJsonConverterShould()
	{
		_options = new JsonSerializerOptions();
		_options.Converters.Add(new IgnoreStreamJsonConverter());
	}

	[Fact]
	public void CanConvert_ReturnsTrueForStream()
	{
		var converter = new IgnoreStreamJsonConverter();
		converter.CanConvert(typeof(Stream)).ShouldBeTrue();
	}

	[Fact]
	public void CanConvert_ReturnsTrueForMemoryStream()
	{
		var converter = new IgnoreStreamJsonConverter();
		converter.CanConvert(typeof(MemoryStream)).ShouldBeTrue();
	}

	[Fact]
	public void CanConvert_ReturnsFalseForString()
	{
		var converter = new IgnoreStreamJsonConverter();
		converter.CanConvert(typeof(string)).ShouldBeFalse();
	}

	[Fact]
	public void Write_WritesNullForStream()
	{
		using var ms = new MemoryStream();
		using var writer = new Utf8JsonWriter(ms);

		var converter = new IgnoreStreamJsonConverter();
		writer.WriteStartObject();
		writer.WritePropertyName("stream");
		converter.Write(writer, new MemoryStream(), _options);
		writer.WriteEndObject();
		writer.Flush();

		var json = Encoding.UTF8.GetString(ms.ToArray());
		json.ShouldContain("null");
	}

	[Fact]
	public void Read_ReturnsNullFromJson()
	{
		var json = "null"u8.ToArray();
		var reader = new Utf8JsonReader(json);
		reader.Read();

		var converter = new IgnoreStreamJsonConverter();
		var result = converter.Read(ref reader, typeof(Stream), _options);
		result.ShouldBeNull();
	}
}
