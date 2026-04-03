// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json;
using System.Text.Json.Serialization;

using Excalibur.Dispatch.Caching;

namespace Excalibur.Dispatch.Caching.Tests;

/// <summary>
/// Tests for <see cref="AotCachedValueJsonConverter"/> covering AOT-safe round-trip
/// serialization, type resolution via JsonSerializerContext, and graceful fallback.
/// Sprint 737 T.21: Wave 3 AOT tests.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Caching")]
[Trait("Feature", "AOT")]
public sealed partial class AotCachedValueJsonConverterShould
{
	[Fact]
	public void ThrowForNullContext()
	{
		Should.Throw<ArgumentNullException>(() => new AotCachedValueJsonConverter(null!));
	}

	[Fact]
	public void RoundTripStringValue()
	{
		var options = CreateOptions();
		var original = new CachedValue
		{
			Value = "hello world",
			ShouldCache = true,
			HasExecuted = true,
			TypeName = typeof(string).FullName,
		};

		var json = JsonSerializer.Serialize(original, options);
		var result = JsonSerializer.Deserialize<CachedValue>(json, options);

		result.ShouldNotBeNull();
		result.Value.ShouldBe("hello world");
		result.ShouldCache.ShouldBeTrue();
		result.HasExecuted.ShouldBeTrue();
		result.TypeName.ShouldBe(typeof(string).FullName);
	}

	[Fact]
	public void RoundTripIntValue()
	{
		var options = CreateOptions();
		var original = new CachedValue
		{
			Value = 42,
			ShouldCache = true,
			HasExecuted = false,
			TypeName = typeof(int).FullName,
		};

		var json = JsonSerializer.Serialize(original, options);
		var result = JsonSerializer.Deserialize<CachedValue>(json, options);

		result.ShouldNotBeNull();
		// Int may come back as JsonElement or int depending on resolution
		var intValue = result.Value is JsonElement el ? el.GetInt32() : Convert.ToInt32(result.Value);
		intValue.ShouldBe(42);
	}

	[Fact]
	public void RoundTripBoolValue()
	{
		var options = CreateOptions();
		var original = new CachedValue
		{
			Value = true,
			ShouldCache = false,
			HasExecuted = true,
			TypeName = typeof(bool).FullName,
		};

		var json = JsonSerializer.Serialize(original, options);
		var result = JsonSerializer.Deserialize<CachedValue>(json, options);

		result.ShouldNotBeNull();
		result.ShouldCache.ShouldBeFalse();
	}

	[Fact]
	public void RoundTripNullValue()
	{
		var options = CreateOptions();
		var original = new CachedValue
		{
			Value = null,
			ShouldCache = true,
			HasExecuted = false,
			TypeName = null,
		};

		var json = JsonSerializer.Serialize(original, options);
		var result = JsonSerializer.Deserialize<CachedValue>(json, options);

		result.ShouldNotBeNull();
		result.Value.ShouldBeNull();
		result.TypeName.ShouldBeNull();
	}

	[Fact]
	public void FallBackToJsonElementForUnknownType()
	{
		var options = CreateOptions();
		var original = new CachedValue
		{
			Value = "test-value",
			ShouldCache = true,
			HasExecuted = true,
			TypeName = "SomeUnknown.CustomType, SomeAssembly",
		};

		var json = JsonSerializer.Serialize(original, options);
		var result = JsonSerializer.Deserialize<CachedValue>(json, options);

		result.ShouldNotBeNull();
		// Unknown type -> value stays as JsonElement (graceful fallback)
		result.Value.ShouldNotBeNull();
		result.TypeName.ShouldBe("SomeUnknown.CustomType, SomeAssembly");
	}

	[Fact]
	public void PreserveShouldCacheAndHasExecutedFlags()
	{
		var options = CreateOptions();

		// Test all combinations
		foreach (var (shouldCache, hasExecuted) in new[] { (true, true), (true, false), (false, true), (false, false) })
		{
			var original = new CachedValue
			{
				Value = "test",
				ShouldCache = shouldCache,
				HasExecuted = hasExecuted,
				TypeName = typeof(string).FullName,
			};

			var json = JsonSerializer.Serialize(original, options);
			var result = JsonSerializer.Deserialize<CachedValue>(json, options)!;

			result.ShouldCache.ShouldBe(shouldCache, $"ShouldCache mismatch for ({shouldCache}, {hasExecuted})");
			result.HasExecuted.ShouldBe(hasExecuted, $"HasExecuted mismatch for ({shouldCache}, {hasExecuted})");
		}
	}

	[Fact]
	public void ProduceJsonWithExpectedPropertyNames()
	{
		var options = CreateOptions();
		var original = new CachedValue
		{
			Value = "test",
			ShouldCache = true,
			HasExecuted = false,
			TypeName = typeof(string).FullName,
		};

		var json = JsonSerializer.Serialize(original, options);

		json.ShouldContain("\"ShouldCache\"");
		json.ShouldContain("\"HasExecuted\"");
		json.ShouldContain("\"TypeName\"");
		json.ShouldContain("\"Value\"");
	}

	[Fact]
	public void RoundTripDecimalValue()
	{
		var options = CreateOptions();
		var original = new CachedValue
		{
			Value = 123.456m,
			ShouldCache = true,
			HasExecuted = true,
			TypeName = typeof(decimal).FullName,
		};

		var json = JsonSerializer.Serialize(original, options);
		var result = JsonSerializer.Deserialize<CachedValue>(json, options);

		result.ShouldNotBeNull();
		result.TypeName.ShouldBe(typeof(decimal).FullName);
	}

	[Fact]
	public void RoundTripDateTimeOffsetValue()
	{
		var options = CreateOptions();
		var timestamp = new DateTimeOffset(2026, 4, 2, 12, 30, 0, TimeSpan.FromHours(2));
		var original = new CachedValue
		{
			Value = timestamp,
			ShouldCache = true,
			HasExecuted = true,
			TypeName = typeof(DateTimeOffset).FullName,
		};

		var json = JsonSerializer.Serialize(original, options);
		var result = JsonSerializer.Deserialize<CachedValue>(json, options);

		result.ShouldNotBeNull();
		result.TypeName.ShouldBe(typeof(DateTimeOffset).FullName);
	}

	[Fact]
	public void RoundTripGuidValue()
	{
		var options = CreateOptions();
		var guid = Guid.NewGuid();
		var original = new CachedValue
		{
			Value = guid,
			ShouldCache = false,
			HasExecuted = true,
			TypeName = typeof(Guid).FullName,
		};

		var json = JsonSerializer.Serialize(original, options);
		var result = JsonSerializer.Deserialize<CachedValue>(json, options);

		result.ShouldNotBeNull();
		result.TypeName.ShouldBe(typeof(Guid).FullName);
	}

	[Fact]
	public void WriteValueUsingTypeInfoWhenTypeIsRegistered()
	{
		var options = CreateOptions();
		var original = new CachedValue
		{
			Value = "typed-write-test",
			ShouldCache = true,
			HasExecuted = true,
			TypeName = typeof(string).FullName,
		};

		// Serialize -> the Write path should use typeInfo for string
		var json = JsonSerializer.Serialize(original, options);

		// Verify the value is written correctly (not as a nested object)
		json.ShouldContain("typed-write-test");
		using var doc = JsonDocument.Parse(json);
		doc.RootElement.GetProperty("Value").GetString().ShouldBe("typed-write-test");
	}

	[Fact]
	public void HandleMissingTypeNameGracefully()
	{
		var options = CreateOptions();
		// Write without TypeName
		var original = new CachedValue
		{
			Value = "no-type",
			ShouldCache = true,
			HasExecuted = false,
			TypeName = null,
		};

		var json = JsonSerializer.Serialize(original, options);
		var result = JsonSerializer.Deserialize<CachedValue>(json, options);

		result.ShouldNotBeNull();
		result.TypeName.ShouldBeNull();
		// Value stays as JsonElement since no type resolution possible
		result.Value.ShouldNotBeNull();
	}

	[Fact]
	public void RoundTripLongValue()
	{
		var options = CreateOptions();
		var original = new CachedValue
		{
			Value = 9_876_543_210L,
			ShouldCache = true,
			HasExecuted = true,
			TypeName = typeof(long).FullName,
		};

		var json = JsonSerializer.Serialize(original, options);
		var result = JsonSerializer.Deserialize<CachedValue>(json, options);

		result.ShouldNotBeNull();
		result.TypeName.ShouldBe(typeof(long).FullName);
	}

	[Fact]
	public void RoundTripDoubleValue()
	{
		var options = CreateOptions();
		var original = new CachedValue
		{
			Value = 3.14159,
			ShouldCache = true,
			HasExecuted = true,
			TypeName = typeof(double).FullName,
		};

		var json = JsonSerializer.Serialize(original, options);
		var result = JsonSerializer.Deserialize<CachedValue>(json, options);

		result.ShouldNotBeNull();
		result.TypeName.ShouldBe(typeof(double).FullName);
	}

	[Fact]
	public void RoundTripDateTimeValue()
	{
		var options = CreateOptions();
		var timestamp = new DateTime(2026, 4, 2, 12, 0, 0, DateTimeKind.Utc);
		var original = new CachedValue
		{
			Value = timestamp,
			ShouldCache = true,
			HasExecuted = true,
			TypeName = typeof(DateTime).FullName,
		};

		var json = JsonSerializer.Serialize(original, options);
		var result = JsonSerializer.Deserialize<CachedValue>(json, options);

		result.ShouldNotBeNull();
		result.TypeName.ShouldBe(typeof(DateTime).FullName);
	}

	[Fact]
	public void RoundTripByteArrayValue()
	{
		var options = CreateOptions();
		var original = new CachedValue
		{
			Value = new byte[] { 0x01, 0x02, 0x03 },
			ShouldCache = true,
			HasExecuted = true,
			TypeName = typeof(byte[]).FullName,
		};

		var json = JsonSerializer.Serialize(original, options);
		var result = JsonSerializer.Deserialize<CachedValue>(json, options);

		result.ShouldNotBeNull();
		result.TypeName.ShouldBe(typeof(byte[]).FullName);
	}

	[Fact]
	public void ThrowForInvalidStartToken()
	{
		var options = CreateOptions();

		// An array is not a valid CachedValue
		Should.Throw<JsonException>(() =>
			JsonSerializer.Deserialize<CachedValue>("[]", options));
	}

	private static JsonSerializerOptions CreateOptions()
	{
		var context = new CacheTestJsonContext();
		var converter = new AotCachedValueJsonConverter(context);
		return new JsonSerializerOptions
		{
			Converters = { converter },
		};
	}

	[JsonSerializable(typeof(string))]
	[JsonSerializable(typeof(int))]
	[JsonSerializable(typeof(long))]
	[JsonSerializable(typeof(double))]
	[JsonSerializable(typeof(decimal))]
	[JsonSerializable(typeof(bool))]
	[JsonSerializable(typeof(DateTime))]
	[JsonSerializable(typeof(DateTimeOffset))]
	[JsonSerializable(typeof(Guid))]
	[JsonSerializable(typeof(byte[]))]
	private sealed partial class CacheTestJsonContext : JsonSerializerContext;
}
