// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.A3.Authorization;
using Excalibur.A3.Authentication;
using Excalibur.A3.Authorization.Grants;
using Excalibur.Dispatch.Caching;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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
[Trait("Pattern", "Regression")]
public sealed class AuthorizationCachePrerequisiteValidatorShould
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StartTheActualHostWithCacheBeforeOrAfterA3(bool cacheFirst)
    {
        using var host = new HostBuilder().ConfigureServices(services =>
        {
            var composed = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
            if (cacheFirst) AddCache(composed);
            Compose(composed);
            if (!cacheFirst) AddCache(composed);
            foreach (var descriptor in composed) services.Add(descriptor);
            services.Configure<GrantDurabilityOptions>(o => o.AllowVolatileGrantStore = true);
        }).UseDefaultServiceProvider(o => { o.ValidateScopes = true; o.ValidateOnBuild = true; }).Build();
        await host.StartAsync(TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(10));
        await host.StopAsync(TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task GiveTheRemedyWhenTheActualHostStartsWithoutTheCache()
    {
        using var host = new HostBuilder().ConfigureServices(services =>
        {
            foreach (var descriptor in Compose()) services.Add(descriptor);
            services.Configure<GrantDurabilityOptions>(o => o.AllowVolatileGrantStore = true);
        }).UseDefaultServiceProvider(o => { o.ValidateScopes = true; o.ValidateOnBuild = true; }).Build();
        var error = await Should.ThrowAsync<InvalidOperationException>(() => host.StartAsync(TestContext.Current.CancellationToken));
        error.Message.ShouldContain("AddApplicationScopedDistributedCache");
        error.Message.ShouldContain("AddDistributedMemoryCache");
    }

    [Fact]
    public void GiveTheRemedyWhenTheActualGrantHandlerIsResolvedWithoutACache()
    {
        var services = Compose();
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        using var scope = provider.CreateScope();
        var error = Should.Throw<InvalidOperationException>(() => scope.ServiceProvider.GetRequiredService<IAuthorizationPolicyProvider>());
        error.Message.ShouldContain("AddApplicationScopedDistributedCache");
        error.Message.ShouldContain("AddDistributedMemoryCache");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ResolveTheActualHandlerWithCacheRegisteredBeforeOrAfterA3(bool cacheFirst)
    {
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        if (cacheFirst) AddCache(services);
        Compose(services);
        if (!cacheFirst) AddCache(services);
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        using var scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<IAuthorizationPolicyProvider>().ShouldNotBeNull();
    }

    private static Microsoft.Extensions.DependencyInjection.ServiceCollection Compose(Microsoft.Extensions.DependencyInjection.ServiceCollection? services = null)
    {
        services ??= new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        services.AddLogging();
        services.AddSingleton(A.Fake<IAuthenticationToken>());
        services.AddSingleton(A.Fake<IGrantRepository>());
        services.AddExcaliburA3();
        services.AddTransient<AddGrantCommandHandler>();
        return services;
    }

    private static void AddCache(IServiceCollection services)
    {
        services.AddDistributedMemoryCache();
        services.AddApplicationScopedDistributedCache(o => o.Scope = "actual-a3-control");
    }

    [Fact]
    public void NameTheBaseCacheRemedyWhenOnlyTheStandardWrapperIsRegistered()
    {
        var services = Compose();
        services.AddApplicationScopedDistributedCache(o => o.Scope = "wrapper-without-base");
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var error = Should.Throw<InvalidOperationException>(() => scope.ServiceProvider.GetRequiredService<IAuthorizationPolicyProvider>());
        error.Message.ShouldContain("AddDistributedMemoryCache");
        error.Message.ShouldContain("AddApplicationScopedDistributedCache");
    }

    [Fact]
    public void AcceptACustomSelfContainedKeyedCacheWithoutAnUnkeyedBase()
    {
        var services = Compose();
        services.AddKeyedSingleton<IDistributedCache>(DistributedCacheServiceKeys.ApplicationScoped, A.Fake<IDistributedCache>());
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        Should.NotThrow(new AuthorizationCachePrerequisiteValidator(provider).Validate);
        using var scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<IAuthorizationPolicyProvider>().ShouldNotBeNull();
    }

    [Fact]
    public void NotAcceptACacheAddedOnlyToTheSourceCollectionAfterBuild()
    {
        var services = Compose();
        using var provider = services.BuildServiceProvider();
        AddCache(services);
        var validator = new AuthorizationCachePrerequisiteValidator(provider);
        Should.Throw<InvalidOperationException>(validator.Validate).Message.ShouldContain("AddApplicationScopedDistributedCache");
    }

    [Fact]
    public void RetainTheBuiltCacheWhenTheSourceCollectionIsCleared()
    {
        var services = Compose();
        AddCache(services);
        using var provider = services.BuildServiceProvider();
        services.Clear();
        var validator = new AuthorizationCachePrerequisiteValidator(provider);
        Should.NotThrow(validator.Validate);
        using var scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<IAuthorizationPolicyProvider>().ShouldNotBeNull();
    }

	private static IDistributedCache InMemoryCache() =>
		new MemoryDistributedCache(Microsoft.Extensions.Options.Options.Create(new MemoryDistributedCacheOptions()));

	[Fact]
	public void ThrowWhenNoApplicationScopedCacheIsRegistered()
	{
		// An unkeyed cache IS registered -- this is the dangerous shape, not an empty container. Everything
		// resolves; nothing is partitioned.
		var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
		_ = services.AddSingleton(InMemoryCache());

		using var provider = services.BuildServiceProvider();
		var validator = new AuthorizationCachePrerequisiteValidator(provider);

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

		using var provider = services.BuildServiceProvider();
		var validator = new AuthorizationCachePrerequisiteValidator(provider);

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

		using var provider = services.BuildServiceProvider();
		var validator = new AuthorizationCachePrerequisiteValidator(provider);

		_ = Should.Throw<InvalidOperationException>(validator.Validate);
	}

	[Fact]
	public async Task RunTheCheckAtStartUp()
	{
		// Registered as a hosted service so the failure lands at start-up rather than at the first
		// authorization -- which, on a cache-backed path, could be long after deployment.
		var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
		_ = services.AddSingleton(InMemoryCache());

		using var provider = services.BuildServiceProvider();
		var validator = new AuthorizationCachePrerequisiteValidator(provider);

		_ = await Should.ThrowAsync<InvalidOperationException>(
			() => validator.StartAsync(TestContext.Current.CancellationToken));
	}
}
