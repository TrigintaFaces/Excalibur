// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;
using System.Runtime.CompilerServices;

using Excalibur.Dispatch.Caching;

namespace Excalibur.Dispatch.Tests.Messaging.Caching;

/// <summary>
/// Tests for Sprint 542 P0 fix S542.8 (bd-dcolc):
/// MessageTypeCache._initialized must be volatile to prevent init race on multi-core CPUs.
/// </summary>
[Collection("MessageTypeCacheTests")]
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class MessageTypeCacheVolatileShould : IDisposable
{
	public MessageTypeCacheVolatileShould()
	{
		ResetCache();
	}

	public void Dispose()
	{
		ResetCache();
	}

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
	public void HaveVolatileInitializedField()
	{
		// Arrange & Act
		var field = typeof(MessageTypeCache)
			.GetField("_initialized", BindingFlags.NonPublic | BindingFlags.Static);

		// Assert
		field.ShouldNotBeNull("MessageTypeCache should have _initialized static field");
		field.FieldType.ShouldBe(typeof(bool));

		var modifiers = field.GetRequiredCustomModifiers();
		modifiers.ShouldContain(typeof(IsVolatile),
			"_initialized should be volatile to prevent initialization race on multi-core CPUs");
	}

	[Fact]
	public void PreventDoubleInitialization()
	{
		// Arrange
		var before = MessageTypeCache.GetCachedTypes().ToHashSet();

		// Act
		MessageTypeCache.Initialize([typeof(string)]);
		var afterFirst = MessageTypeCache.GetCachedTypes().ToHashSet();

		MessageTypeCache.Initialize([typeof(int)]);
		var afterSecond = MessageTypeCache.GetCachedTypes().ToHashSet();

		// Assert: once initialized, subsequent Initialize calls are no-ops.
		afterSecond.SetEquals(afterFirst).ShouldBeTrue("Second initialization should not mutate the cache");

		if (before.Count == 0)
		{
			afterFirst.Contains(typeof(string)).ShouldBeTrue("First initialization should include provided type");
		}
	}
}
