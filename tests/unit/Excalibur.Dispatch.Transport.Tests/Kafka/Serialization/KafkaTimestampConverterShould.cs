// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text;
using System.Text.Json;

using Confluent.Kafka;

using Excalibur.Dispatch.Transport.Kafka;

namespace Excalibur.Dispatch.Transport.Tests.Kafka.Serialization;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class KafkaTimestampConverterShould
{
	private static readonly JsonSerializerOptions Options = new()
	{
		Converters = { new KafkaTimestampConverter() },
	};

	[Fact]
	public void SerializeTimestampAsUnixMilliseconds()
	{
		// Arrange
		var timestamp = new Timestamp(new DateTime(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc));

		// Act
		var json = JsonSerializer.Serialize(timestamp, Options);

		// Assert
		json.ShouldNotBeEmpty();
		long.TryParse(json, out _).ShouldBeTrue();
	}

	[Fact]
	public void DeserializeTimestampFromNumber()
	{
		// Arrange
		var now = DateTimeOffset.UtcNow;
		var unixMs = now.ToUnixTimeMilliseconds();
		var json = unixMs.ToString();

		// Act
		var timestamp = JsonSerializer.Deserialize<Timestamp>(json, Options);

		// Assert
		timestamp.UnixTimestampMs.ShouldBe(unixMs);
	}

	[Fact]
	public void DeserializeTimestampFromNumericString()
	{
		// Arrange
		var unixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
		var json = $"\"{unixMs}\"";

		// Act
		var timestamp = JsonSerializer.Deserialize<Timestamp>(json, Options);

		// Assert
		timestamp.UnixTimestampMs.ShouldBe(unixMs);
	}

	[Fact]
	public void DeserializeTimestampFromIsoString()
	{
		// Arrange
		var json = "\"2026-01-15T12:00:00Z\"";

		// Act
		var timestamp = JsonSerializer.Deserialize<Timestamp>(json, Options);

		// Assert
		timestamp.UnixTimestampMs.ShouldBeGreaterThan(0);
	}

	[Fact]
	public void RoundTripTimestamp()
	{
		// Arrange
		var original = new Timestamp(new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));

		// Act
		var json = JsonSerializer.Serialize(original, Options);
		var deserialized = JsonSerializer.Deserialize<Timestamp>(json, Options);

		// Assert
		deserialized.UnixTimestampMs.ShouldBe(original.UnixTimestampMs);
	}

	[Fact]
	public void ThrowOnInvalidTokenType()
	{
		// Arrange
		var json = "true";

		// Act & Assert
		Should.Throw<JsonException>(() => JsonSerializer.Deserialize<Timestamp>(json, Options));
	}
}
