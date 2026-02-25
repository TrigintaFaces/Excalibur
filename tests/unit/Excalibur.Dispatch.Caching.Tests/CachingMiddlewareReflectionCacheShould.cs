// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Reflection;

using Excalibur.Dispatch.Caching;

namespace Excalibur.Dispatch.Caching.Tests;

/// <summary>
/// Tests for Sprint 542 P0 fix S542.13 (bd-49lrg):
/// CachingMiddleware hot-path reflection -> ConcurrentDictionary caches.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class CachingMiddlewareReflectionCacheShould
{
	[Fact]
	public void HaveStaticCacheableInterfaceCache()
	{
		var field = typeof(CachingMiddleware)
			.GetField("_cacheableInterfaceCache", BindingFlags.NonPublic | BindingFlags.Static);

		field.ShouldNotBeNull("CachingMiddleware should have _cacheableInterfaceCache static field");
		field.IsStatic.ShouldBeTrue();

		// Should be ConcurrentDictionary<Type, Type?>
		field.FieldType.IsGenericType.ShouldBeTrue();
		field.FieldType.GetGenericTypeDefinition().ShouldBe(typeof(ConcurrentDictionary<,>));

		var genericArgs = field.FieldType.GetGenericArguments();
		genericArgs[0].ShouldBe(typeof(Type));
	}

	[Fact]
	public void HaveStaticActionInterfaceCache()
	{
		var field = typeof(CachingMiddleware)
			.GetField("_actionInterfaceCache", BindingFlags.NonPublic | BindingFlags.Static);

		field.ShouldNotBeNull("CachingMiddleware should have _actionInterfaceCache static field");
		field.IsStatic.ShouldBeTrue();

		field.FieldType.IsGenericType.ShouldBeTrue();
		field.FieldType.GetGenericTypeDefinition().ShouldBe(typeof(ConcurrentDictionary<,>));

		var genericArgs = field.FieldType.GetGenericArguments();
		genericArgs[0].ShouldBe(typeof(Type));
	}

	[Fact]
	public void HaveReadOnlyCaches()
	{
		var cacheableField = typeof(CachingMiddleware)
			.GetField("_cacheableInterfaceCache", BindingFlags.NonPublic | BindingFlags.Static);
		var actionField = typeof(CachingMiddleware)
			.GetField("_actionInterfaceCache", BindingFlags.NonPublic | BindingFlags.Static);

		cacheableField.ShouldNotBeNull();
		actionField.ShouldNotBeNull();

		cacheableField.IsInitOnly.ShouldBeTrue("_cacheableInterfaceCache should be readonly");
		actionField.IsInitOnly.ShouldBeTrue("_actionInterfaceCache should be readonly");
	}
}
