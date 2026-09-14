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
	public void KeepTheActionInterfaceCacheReadOnly()
	{
		// The cacheable-interface cache that used to sit beside this one is gone: cacheability is now a
		// type test, so there is no reflection result left to memoize. This one remains because
		// resolving IDispatchAction<T> for the result type is still a reflective lookup.
		var actionField = typeof(CachingMiddleware)
			.GetField("_actionInterfaceCache", BindingFlags.NonPublic | BindingFlags.Static);

		actionField.ShouldNotBeNull();
		actionField.IsInitOnly.ShouldBeTrue("_actionInterfaceCache should be readonly");
	}

}
