// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text;
using System.Text.Json;
using Excalibur.Dispatch.Transport.Azure;

namespace Excalibur.Dispatch.Transport.Tests.AzureServiceBus.Serialization;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class TimeSpanConverterShould
{
	private static readonly JsonSerializerOptions Options = new()
	{
		Converters = { new TimeSpanConverter() },
	};

	[Fact]
	public void ReadIso8601DurationFormat()
	{
		// Arrange
		var json = "\"PT1H30M\""u8.ToArray();

		// Act
		var result = JsonSerializer.Deserialize<TimeSpan>(json, Options);

		// Assert
		result.ShouldBe(TimeSpan.FromMinutes(90));
	}

	[Fact]
	public void ReadIso8601DurationWithDays()
	{
		// Arrange
		var json = "\"P1D\""u8.ToArray();

		// Act
		var result = JsonSerializer.Deserialize<TimeSpan>(json, Options);

		// Assert
		result.ShouldBe(TimeSpan.FromDays(1));
	}

	[Fact]
	public void ReadStandardDotNetFormat()
	{
		// Arrange
		var json = "\"01:30:00\""u8.ToArray();

		// Act
		var result = JsonSerializer.Deserialize<TimeSpan>(json, Options);

		// Assert
		result.ShouldBe(TimeSpan.FromMinutes(90));
	}

	[Fact]
	public void ReadEmptyStringAsZero()
	{
		// Arrange
		var json = "\"\""u8.ToArray();

		// Act
		var result = JsonSerializer.Deserialize<TimeSpan>(json, Options);

		// Assert
		result.ShouldBe(TimeSpan.Zero);
	}

	[Fact]
	public void ReadNumberAsMilliseconds()
	{
		// Arrange
		var json = "5000"u8.ToArray();

		// Act
		var result = JsonSerializer.Deserialize<TimeSpan>(json, Options);

		// Assert
		result.ShouldBe(TimeSpan.FromMilliseconds(5000));
	}

	[Fact]
	public void WriteAsIso8601Duration()
	{
		// Arrange
		var value = TimeSpan.FromMinutes(90);

		// Act
		var json = JsonSerializer.Serialize(value, Options);

		// Assert — XmlConvert.ToString produces ISO 8601 duration
		json.ShouldContain("PT");
	}

	[Fact]
	public void ThrowOnUnsupportedTokenType()
	{
		// Arrange — boolean token
		var json = "true"u8.ToArray();

		// Act & Assert
		Should.Throw<JsonException>(() => JsonSerializer.Deserialize<TimeSpan>(json, Options));
	}

	[Fact]
	public void RoundTripTimeSpanValue()
	{
		// Arrange
		var original = TimeSpan.FromHours(2) + TimeSpan.FromMinutes(30) + TimeSpan.FromSeconds(15);

		// Act
		var json = JsonSerializer.Serialize(original, Options);
		var deserialized = JsonSerializer.Deserialize<TimeSpan>(json, Options);

		// Assert
		deserialized.ShouldBe(original);
	}
}
