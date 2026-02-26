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
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class MessageTypeCacheVolatileShould
{
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
