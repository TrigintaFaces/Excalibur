// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Caching;

namespace Excalibur.Dispatch.Tests.Messaging.Caching;

/// <summary>
/// Unit tests for <see cref="GlobalStringCache"/>.
/// </summary>
/// <remarks>
/// Tests the global string encoding cache singleton.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Caching")]
[Trait("Priority", "0")]
public sealed class GlobalStringCacheShould
{
	#region Instance Tests

	[Fact]
	public void Instance_ReturnsNonNullInstance()
	{
		// Arrange & Act
		var instance = GlobalStringCache.Instance;

		// Assert
		_ = instance.ShouldNotBeNull();
	}

	[Fact]
	public void Instance_ReturnsSameInstance()
	{
		// Arrange & Act
		var instance1 = GlobalStringCache.Instance;
		var instance2 = GlobalStringCache.Instance;

		// Assert
		instance1.ShouldBeSameAs(instance2);
	}

	[Fact]
	public void Instance_IsStringEncodingCache()
	{
		// Arrange & Act
		var instance = GlobalStringCache.Instance;

		// Assert
		_ = instance.ShouldBeOfType<StringEncodingCache>();
	}

	#endregion

	#region Preloaded Values Tests

	[Theory]
	[InlineData("id")]
	[InlineData("MessageId")]
	[InlineData("ID")]
	[InlineData("type")]
	[InlineData("Type")]
	[InlineData("TYPE")]
	[InlineData("timestamp")]
	[InlineData("Timestamp")]
	[InlineData("TIMESTAMP")]
	[InlineData("version")]
	[InlineData("Version")]
	[InlineData("VERSION")]
	[InlineData("data")]
	[InlineData("Data")]
	[InlineData("DATA")]
	[InlineData("message")]
	[InlineData("Message")]
	[InlineData("MESSAGE")]
	[InlineData("error")]
	[InlineData("Error")]
	[InlineData("ERROR")]
	[InlineData("status")]
	[InlineData("Status")]
	[InlineData("STATUS")]
	[InlineData("name")]
	[InlineData("Name")]
	[InlineData("NAME")]
	[InlineData("value")]
	[InlineData("Value")]
	[InlineData("VALUE")]
	[InlineData("true")]
	[InlineData("false")]
	[InlineData("null")]
	public void PreloadedValue_ReturnsUtf8Bytes(string preloadedValue)
	{
		// Arrange
		var cache = GlobalStringCache.Instance;

		// Act
		var bytes = cache.GetUtf8Bytes(preloadedValue);

		// Assert
		bytes.Length.ShouldBeGreaterThan(0);
		System.Text.Encoding.UTF8.GetString(bytes.ToArray()).ShouldBe(preloadedValue);
	}

	[Fact]
	public void PreloadedValues_AreAllCached()
	{
		// Arrange - List of all preloaded values from the static constructor
		var preloadedValues = new[]
		{
			"id", "MessageId", "ID",
			"type", "Type", "TYPE",
			"timestamp", "Timestamp", "TIMESTAMP",
			"version", "Version", "VERSION",
			"data", "Data", "DATA",
			"message", "Message", "MESSAGE",
			"error", "Error", "ERROR",
			"status", "Status", "STATUS",
			"name", "Name", "NAME",
			"value", "Value", "VALUE",
			"true", "false", "null",
		};

		var cache = GlobalStringCache.Instance;

		// Act & Assert - All preloaded values should be retrievable
		foreach (var value in preloadedValues)
		{
			var bytes = cache.GetUtf8Bytes(value);
			bytes.Length.ShouldBeGreaterThan(0, $"Preloaded value '{value}' should be cached");
		}
	}

	#endregion

	#region Non-Preloaded Values Tests

	[Fact]
	public void NonPreloadedValue_CanBeRetrieved()
	{
		// Arrange
		var cache = GlobalStringCache.Instance;
		var customValue = "CustomFieldName_" + Guid.NewGuid().ToString("N")[..8];

		// Act
		var bytes = cache.GetUtf8Bytes(customValue);

		// Assert
		bytes.Length.ShouldBeGreaterThan(0);
		System.Text.Encoding.UTF8.GetString(bytes.ToArray()).ShouldBe(customValue);
	}

	[Fact]
	public void EmptyString_ReturnsEmptySpan()
	{
		// Arrange
		var cache = GlobalStringCache.Instance;

		// Act
		var bytes = cache.GetUtf8Bytes(string.Empty);

		// Assert
		bytes.Length.ShouldBe(0);
	}

	#endregion

	#region Thread Safety Tests

	[Fact]
	public void Instance_IsThreadSafe()
	{
		// Arrange
		var instances = new StringEncodingCache[100];
		var exceptions = new List<Exception>();

		// Act - Access instance from multiple threads
		_ = Parallel.For(0, 100, i =>
		{
			try
			{
				instances[i] = GlobalStringCache.Instance;
			}
			catch (Exception ex)
			{
				lock (exceptions)
				{
					exceptions.Add(ex);
				}
			}
		});

		// Assert
		exceptions.ShouldBeEmpty();
		instances.All(i => i != null).ShouldBeTrue();
		instances.Distinct().Count().ShouldBe(1); // All same instance
	}

	[Fact]
	public void ConcurrentAccess_ToPreloadedValues_Works()
	{
		// Arrange
		var preloadedValues = new[] { "id", "type", "timestamp", "data", "message" };
		var exceptions = new List<Exception>();

		// Act - Concurrent access to preloaded values
		_ = Parallel.For(0, 1000, i =>
		{
			try
			{
				var value = preloadedValues[i % preloadedValues.Length];
				var bytes = GlobalStringCache.Instance.GetUtf8Bytes(value);
				_ = System.Text.Encoding.UTF8.GetString(bytes.ToArray());
			}
			catch (Exception ex)
			{
				lock (exceptions)
				{
					exceptions.Add(ex);
				}
			}
		});

		// Assert
		exceptions.ShouldBeEmpty();
	}

	#endregion

	#region Typical Usage Scenarios

	[Fact]
	public void JsonFieldNameEncoding_Scenario()
	{
		// Arrange - Common JSON field names
		var cache = GlobalStringCache.Instance;

		// Act
		var idBytes = cache.GetUtf8Bytes("id").ToArray();
		var typeBytes = cache.GetUtf8Bytes("type").ToArray();
		var timestampBytes = cache.GetUtf8Bytes("timestamp");

		// Assert - Should return UTF-8 encoded bytes
		idBytes.ShouldBe(new byte[] { 0x69, 0x64 }); // "id"
		typeBytes.ShouldBe(new byte[] { 0x74, 0x79, 0x70, 0x65 }); // "type"
		timestampBytes.Length.ShouldBe(9); // "timestamp"
	}

	[Fact]
	public void JsonBooleanAndNullValues_Scenario()
	{
		// Arrange
		var cache = GlobalStringCache.Instance;

		// Act
		var trueBytes = cache.GetUtf8Bytes("true").ToArray();
		var falseBytes = cache.GetUtf8Bytes("false").ToArray();
		var nullBytes = cache.GetUtf8Bytes("null").ToArray();

		// Assert
		trueBytes.ShouldBe(new byte[] { 0x74, 0x72, 0x75, 0x65 }); // "true"
		falseBytes.ShouldBe(new byte[] { 0x66, 0x61, 0x6C, 0x73, 0x65 }); // "false"
		nullBytes.ShouldBe(new byte[] { 0x6E, 0x75, 0x6C, 0x6C }); // "null"
	}

	[Fact]
	public void CaseVariations_AreAllPreloaded()
	{
		// Arrange
		var cache = GlobalStringCache.Instance;
		var caseVariations = new[] { "error", "Error", "ERROR" };

		// Act & Assert
		foreach (var variation in caseVariations)
		{
			var bytes = cache.GetUtf8Bytes(variation);
			System.Text.Encoding.UTF8.GetString(bytes.ToArray()).ShouldBe(variation);
		}
	}

	#endregion
}
