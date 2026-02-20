// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.Dispatch.Caching;

namespace Excalibur.Dispatch.Tests.Messaging.Caching;

/// <summary>
/// Tests for Sprint 567 S567.3: MessageTypeCache null filtering in Initialize().
/// Validates that null types in the input collection are filtered out with a
/// warning log, and valid types are preserved.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Priority", "2")]
public sealed class MessageTypeCacheNullFilteringShould : IDisposable
{
	private readonly ILogger _logger;

	public MessageTypeCacheNullFilteringShould()
	{
		_logger = A.Fake<ILogger>();
		A.CallTo(() => _logger.IsEnabled(A<LogLevel>._)).Returns(true);

		// Reset cache state before each test using reflection
		ResetCache();
	}

	public void Dispose()
	{
		ResetCache();
	}

	/// <summary>
	/// Resets the static MessageTypeCache state via reflection so each test starts fresh.
	/// </summary>
	private static void ResetCache()
	{
		var flags = BindingFlags.NonPublic | BindingFlags.Static;

		var initializedField = typeof(MessageTypeCache).GetField("_initialized", flags);
		initializedField?.SetValue(null, false);

		var typeCacheField = typeof(MessageTypeCache).GetField("_typeCache", flags);
		typeCacheField?.SetValue(null, System.Collections.Frozen.FrozenDictionary<Type, MessageTypeMetadata>.Empty);

		var nameCacheField = typeof(MessageTypeCache).GetField("_nameToTypeCache", flags);
		nameCacheField?.SetValue(null, System.Collections.Frozen.FrozenDictionary<string, Type>.Empty);
	}

	[Fact]
	public void Initialize_WithNullTypes_FiltersThemOut()
	{
		// Arrange - mix of valid types and nulls
		var types = new Type?[] { typeof(string), null, typeof(int), null };

		// Act - should not throw
		MessageTypeCache.Initialize(types!);

		// Assert - valid types are cached, nulls are filtered
		MessageTypeCache.IsCached(typeof(string)).ShouldBeTrue("Valid type 'string' should be cached");
		MessageTypeCache.IsCached(typeof(int)).ShouldBeTrue("Valid type 'int' should be cached");
	}

	[Fact]
	public void Initialize_WithAllNulls_ProducesEmptyCache()
	{
		// Arrange
		var types = new Type?[] { null, null, null };

		// Act
		MessageTypeCache.Initialize(types!);

		// Assert
		MessageTypeCache.GetCachedTypes().ShouldBeEmpty("All-null input should produce empty cache");
	}

	[Fact]
	public void Initialize_WithNoNulls_CachesAllTypes()
	{
		// Arrange
		var types = new[] { typeof(string), typeof(int), typeof(double) };

		// Act
		MessageTypeCache.Initialize(types);

		// Assert
		MessageTypeCache.IsCached(typeof(string)).ShouldBeTrue();
		MessageTypeCache.IsCached(typeof(int)).ShouldBeTrue();
		MessageTypeCache.IsCached(typeof(double)).ShouldBeTrue();
		MessageTypeCache.GetCachedTypes().Count.ShouldBe(3);
	}

	[Fact]
	public void Initialize_WithEmptyCollection_ProducesEmptyCache()
	{
		// Arrange
		var types = Array.Empty<Type>();

		// Act
		MessageTypeCache.Initialize(types);

		// Assert
		MessageTypeCache.GetCachedTypes().ShouldBeEmpty();
	}

	[Fact]
	public void Initialize_ThrowsOnNullCollection()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => MessageTypeCache.Initialize(null!));
	}

	[Fact]
	public void Initialize_WithSingleNull_FiltersItAndCachesNothing()
	{
		// Arrange
		var types = new Type?[] { null };

		// Act
		MessageTypeCache.Initialize(types!);

		// Assert
		MessageTypeCache.GetCachedTypes().ShouldBeEmpty();
	}

	[Fact]
	public void Initialize_WithMixedNulls_PreservesTypeMetadata()
	{
		// Arrange
		var types = new Type?[] { null, typeof(string), null };

		// Act
		MessageTypeCache.Initialize(types!);

		// Assert
		var metadata = MessageTypeCache.GetMetadata(typeof(string));
		metadata.ShouldNotBeNull("Metadata for valid type should be available");
		metadata.FullName.ShouldBe(typeof(string).FullName);
	}

	[Fact]
	public void Initialize_WithMixedNulls_PreservesTypeNameResolution()
	{
		// Arrange
		var types = new Type?[] { null, typeof(string), null };

		// Act
		MessageTypeCache.Initialize(types!);

		// Assert - type should be resolvable by name
		var resolved = MessageTypeCache.ResolveType(typeof(string).FullName!);
		resolved.ShouldBe(typeof(string));
	}

	[Fact]
	public void Initialize_WithNullTypes_LogsWarning()
	{
		// Arrange
		var types = new Type?[] { typeof(string), null, typeof(int), null };

		// Act
		MessageTypeCache.Initialize(types!, _logger);

		// Assert - logger should have received a warning about null types
		A.CallTo(_logger)
			.Where(call => call.Method.Name == "Log")
			.MustHaveHappened();
	}

	[Fact]
	public void Initialize_WithNoNulls_DoesNotLogWarning()
	{
		// Arrange
		var logger = A.Fake<ILogger>();
		A.CallTo(() => logger.IsEnabled(A<LogLevel>._)).Returns(true);
		var types = new[] { typeof(string), typeof(int) };

		// Act
		MessageTypeCache.Initialize(types, logger);

		// Assert - no warning logged when no nulls
		A.CallTo(logger)
			.Where(call => call.Method.Name == "Log" &&
				call.Arguments.Count > 0 &&
				(LogLevel)call.Arguments[0]! == LogLevel.Warning)
			.MustNotHaveHappened();
	}
}
