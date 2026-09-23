// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.A3.Authorization;
using Excalibur.Dispatch.Caching;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Tests.A3.Authorization;

/// <summary>
/// Unit tests for the authorization-cache start-up prerequisite.
/// </summary>
/// <remarks>
/// The guard exists because the scoping lives in a cache registration that can be absent three ways: never
/// registered, registered after the composition that wanted it, or skipped by a decoration path that
/// exempts in-memory caches. The guard does not care which happened -- it checks the outcome.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "A3")]
[Trait("Feature", "Authorization")]
public sealed class AuthorizationCachePrerequisiteValidatorShould
{
	private static IDistributedCache InMemoryCache() =>
		new MemoryDistributedCache(Microsoft.Extensions.Options.Options.Create(new MemoryDistributedCacheOptions()));

	[Fact]
	public void ThrowWhenNoApplicationScopedCacheIsRegistered()
	{
		// An unkeyed cache IS registered -- this is the dangerous shape, not an empty container. Everything
		// resolves; nothing is partitioned.
		var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
		_ = services.AddSingleton(InMemoryCache());

		var validator = new AuthorizationCachePrerequisiteValidator(services);

		var error = Should.Throw<InvalidOperationException>(validator.Validate);

		// The message must name the call to add. A bare DI resolution failure is structurally correct and
		// tells the consumer nothing about what to do next.
		error.Message.ShouldContain("AddApplicationScopedDistributedCache");
	}

	[Fact]
	public void NotThrowWhenTheApplicationScopedCacheIsRegistered()
	{
		// Liveness. A guard that threw unconditionally would satisfy the arm above while making every
		// correct composition fail -- and that is the cheaper mistake to make, because the arm above would
		// still be green.
		var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
		_ = services.AddSingleton(InMemoryCache());
		_ = services.AddApplicationScopedDistributedCache(o => o.Scope = "app-a");

		var validator = new AuthorizationCachePrerequisiteValidator(services);

		Should.NotThrow(validator.Validate);
	}

	[Fact]
	public void NotBeSatisfiedByAnUnkeyedCacheAloneEvenWhenScopeOptionsAreConfigured()
	{
		// Configuring the options without registering the cache is the near-miss: the consumer has said
		// which application they are and still has no partition. Options presence must not read as
		// scoping presence.
		var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
		_ = services.AddSingleton(InMemoryCache());
		_ = services.Configure<CacheKeyScopeOptions>(o => o.Scope = "app-a");

		var validator = new AuthorizationCachePrerequisiteValidator(services);

		_ = Should.Throw<InvalidOperationException>(validator.Validate);
	}

	[Fact]
	public async Task RunTheCheckAtStartUp()
	{
		// Registered as a hosted service so the failure lands at start-up rather than at the first
		// authorization -- which, on a cache-backed path, could be long after deployment.
		var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
		_ = services.AddSingleton(InMemoryCache());

		var validator = new AuthorizationCachePrerequisiteValidator(services);

		_ = await Should.ThrowAsync<InvalidOperationException>(
			() => validator.StartAsync(TestContext.Current.CancellationToken));
	}
}
