// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text;

using Excalibur.Dispatch.Caching;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Caching.Tests;

/// <summary>
/// Unit tests for the application-scoped distributed cache.
/// </summary>
/// <remarks>
/// <para>
/// Every arm here shares ONE backing cache between two scopes on purpose. A fixture giving each scope its
/// own cache instance cannot construct the condition under test -- the keys would never have shared a
/// keyspace -- so it would pass against completely unscoped code and prove nothing.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ScopedDistributedCacheShould
{
	private static readonly DistributedCacheEntryOptions Forever = new();

	private static IDistributedCache SharedBackingCache() =>
		new MemoryDistributedCache(Microsoft.Extensions.Options.Options.Create(new MemoryDistributedCacheOptions()));

	[Fact]
	public void NotLetOneScopeReadAnotherScopesEntryForTheSameKey()
	{
		// One cache server, two applications, one user id -- the deployment this exists to make safe.
		var shared = SharedBackingCache();
		IDistributedCache appA = new ScopedDistributedCache(shared, "app-a");
		IDistributedCache appB = new ScopedDistributedCache(shared, "app-b");

		appA.Set("authorization/user-1/grants", Encoding.UTF8.GetBytes("A's grants"), Forever);

		// Safety: B must not observe A's entry.
		appB.Get("authorization/user-1/grants").ShouldBeNull();

		// Liveness, and it is the arm that matters most here: a cache that returned null to EVERYONE would
		// satisfy the assertion above while being completely broken. A must still read its own entry.
		Encoding.UTF8.GetString(appA.Get("authorization/user-1/grants")!).ShouldBe("A's grants");
	}

	[Fact]
	public void NotLetOneScopeDeleteAnotherScopesEntry()
	{
		// Remove and Refresh route through the same partitioning as Get and Set. A member that forgot to
		// would not throw -- it would operate on the unpartitioned key, so a delete could reach across.
		var shared = SharedBackingCache();
		IDistributedCache appA = new ScopedDistributedCache(shared, "app-a");
		IDistributedCache appB = new ScopedDistributedCache(shared, "app-b");

		appA.Set("authorization/user-1/grants", Encoding.UTF8.GetBytes("A's grants"), Forever);
		appB.Set("authorization/user-1/grants", Encoding.UTF8.GetBytes("B's grants"), Forever);

		appB.Remove("authorization/user-1/grants");

		Encoding.UTF8.GetString(appA.Get("authorization/user-1/grants")!).ShouldBe("A's grants");
		appB.Get("authorization/user-1/grants").ShouldBeNull();
	}

	[Fact]
	public async Task PartitionTheAsynchronousSurfaceToo()
	{
		var shared = SharedBackingCache();
		IDistributedCache appA = new ScopedDistributedCache(shared, "app-a");
		IDistributedCache appB = new ScopedDistributedCache(shared, "app-b");

		await appA.SetAsync("k", Encoding.UTF8.GetBytes("A"), Forever, TestContext.Current.CancellationToken);

		(await appB.GetAsync("k", TestContext.Current.CancellationToken)).ShouldBeNull();
		Encoding.UTF8.GetString((await appA.GetAsync("k", TestContext.Current.CancellationToken))!)
			.ShouldBe("A");
	}

	[Fact]
	public void RefuseToConstructWithoutAScope() =>
		// Fails closed. An empty scope forwards keys unchanged, which is indistinguishable from having no
		// scoped cache -- except that the caller asked for one and would believe it had it.
		Should.Throw<ArgumentException>(() => new ScopedDistributedCache(SharedBackingCache(), "   "));

	[Fact]
	public void RegisterUnderTheKeyedServiceThatComponentsDependOn()
	{
		var services = new ServiceCollection();
		_ = services.AddSingleton<IDistributedCache>(SharedBackingCache());
		_ = services.AddApplicationScopedDistributedCache(o => o.Scope = "app-a");

		using var provider = services.BuildServiceProvider();

		var scoped = provider.GetRequiredKeyedService<IDistributedCache>(
			DistributedCacheServiceKeys.ApplicationScoped);
		var unkeyed = provider.GetRequiredService<IDistributedCache>();

		// The keyed registration is a SEPARATE service, not a replacement: the unkeyed cache is untouched
		// for everything whose entries are harmless to share.
		scoped.ShouldNotBeSameAs(unkeyed);

		scoped.Set("k", Encoding.UTF8.GetBytes("scoped"), Forever);
		unkeyed.Get("k").ShouldBeNull("the scoped write must not be readable at the unpartitioned key");
		unkeyed.Get("app-a:k").ShouldNotBeNull();
	}

	[Fact]
	public void PartitionEvenAnInMemoryBackingCache()
	{
		// The general cache-decoration path deliberately SKIPS decorating MemoryDistributedCache, because
		// an in-memory cache cannot stall on I/O so bounding its latency buys nothing. That reasoning is
		// about latency and says nothing about key isolation: an in-memory distributed cache can serve two
		// applications in one process -- a test fixture is exactly that. Inheriting the exemption here
		// would remove the partition in the configuration where two applications most easily share a cache,
		// and would make every arm above pass on nothing.
		var shared = new MemoryDistributedCache(Microsoft.Extensions.Options.Options.Create(new MemoryDistributedCacheOptions()));
		shared.ShouldBeOfType<MemoryDistributedCache>();

		IDistributedCache appA = new ScopedDistributedCache(shared, "app-a");
		IDistributedCache appB = new ScopedDistributedCache(shared, "app-b");

		appA.Set("k", Encoding.UTF8.GetBytes("A"), Forever);

		appB.Get("k").ShouldBeNull();
		Encoding.UTF8.GetString(appA.Get("k")!).ShouldBe("A");
	}
}
