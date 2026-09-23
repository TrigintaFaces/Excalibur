// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Caching;

namespace Excalibur.Dispatch.Caching.Tests;

/// <summary>
/// Direct, middleware-free lock on <see cref="ICacheable.CreateCachedResult"/> and its
/// <see cref="ICacheable{T}"/> override -- the default interface method that lets a cache hit build its
/// typed result without <see cref="System.Type.MakeGenericType(System.Type[])"/> or constructor
/// reflection (Excalibur_Dispatch-9tr6r5).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Feature", "Caching")]
public sealed class ICacheableCreateCachedResultShould
{
	[Fact]
	public void ReturnATypedResultWhenTheValueMatchesTheDeclaredType()
	{
		ICacheable cacheable = new StringQuery();

		var result = cacheable.CreateCachedResult("hello");

		result.ShouldNotBeNull();
		result.Succeeded.ShouldBeTrue();
		result.UntypedReturnValue.ShouldBe("hello");
	}

	[Fact]
	public void ReturnNullWhenTheValueDoesNotMatchTheDeclaredType()
	{
		// The stored value belongs to a different action that shares this one's cache key -- the same
		// fail-open signal the middleware already relies on to avoid returning another action's data.
		ICacheable cacheable = new StringQuery();

		var result = cacheable.CreateCachedResult(42);

		result.ShouldBeNull();
	}

	[Fact]
	public void ReturnNullFromTheNonGenericInterfaceWithNoTypeToConstruct()
	{
		// A message cacheable only via [CacheResult] has no ICacheable<T> to hang a typed override on,
		// so the base ICacheable member is the one that runs, and its contract is "nothing to build".
		ICacheable cacheable = new AttributeOnlyCacheable();

		var result = cacheable.CreateCachedResult("anything");

		result.ShouldBeNull();
	}

	private sealed class StringQuery : ICacheable<string>
	{
		public string GetCacheKey() => "string-query";
	}

	private sealed class AttributeOnlyCacheable : ICacheable
	{
		public string GetCacheKey() => "attribute-only";
	}
}
