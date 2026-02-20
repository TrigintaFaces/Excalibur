// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Reflection;

using Excalibur.Dispatch.Caching;

namespace Excalibur.Dispatch.Caching.Tests;

/// <summary>
/// Tests for Sprint 542 P0 fix S542.12 (bd-j9cxb):
/// CacheInvalidationMiddleware uncached reflection -> ConcurrentDictionary cache.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class CacheInvalidationMiddlewareReflectionCacheShould
{
	[Fact]
	public void HaveStaticAttributeCacheDictionary()
	{
		// Arrange & Act
		var field = typeof(CacheInvalidationMiddleware)
			.GetField("_attributeCache", BindingFlags.NonPublic | BindingFlags.Static);

		// Assert
		field.ShouldNotBeNull("CacheInvalidationMiddleware should have _attributeCache static field");
		field.IsStatic.ShouldBeTrue("_attributeCache should be static");

		// Verify it's a ConcurrentDictionary<Type, InvalidateCacheAttribute?>
		field.FieldType.IsGenericType.ShouldBeTrue();
		field.FieldType.GetGenericTypeDefinition().ShouldBe(typeof(ConcurrentDictionary<,>));
		field.FieldType.GetGenericArguments()[0].ShouldBe(typeof(Type));
	}

	[Fact]
	public void HaveReadOnlyAttributeCache()
	{
		var field = typeof(CacheInvalidationMiddleware)
			.GetField("_attributeCache", BindingFlags.NonPublic | BindingFlags.Static);

		field.ShouldNotBeNull();
		field.IsInitOnly.ShouldBeTrue("_attributeCache should be readonly (init-only)");
	}
}
