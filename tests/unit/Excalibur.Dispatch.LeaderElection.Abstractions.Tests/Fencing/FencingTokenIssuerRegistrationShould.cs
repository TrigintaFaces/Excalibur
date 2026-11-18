// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.LeaderElection.Fencing;
using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.LeaderElection.Abstractions.Tests.Fencing;

[Trait("Category", "Unit")]
[Trait("Component", "LeaderElection")]
[Trait("Pattern", "Regression")]
public sealed class FencingTokenIssuerRegistrationShould
{
    [Fact]
    public async Task ForwardAllMethodsToTheSelectedProviderWithoutDuplicatingDisposal()
    {
        var services = Services();
        services.AddFencingTokenIssuerSupport<ProbeIssuer>();
        using var provider = services.BuildServiceProvider(validateScopes: true);
        var selected = provider.GetRequiredService<IFencingTokenProvider>().ShouldBeOfType<ProbeIssuer>();
        var issuer = provider.GetRequiredService<IFencingTokenIssuer>();
        issuer.ShouldNotBeSameAs(selected);
        selected.UsedLongestConstructor.ShouldBeTrue();
        using var stop = new CancellationTokenSource();
        (await issuer.IssueTokenAsync("resource", stop.Token)).ShouldBe(1);
        (await issuer.GetTokenAsync("resource", stop.Token)).ShouldBe(1);
        (await issuer.ValidateTokenAsync("resource", 1, stop.Token)).ShouldBeTrue();
        selected.Calls.ShouldBe(3);
        selected.LastResource.ShouldBe("resource");
        selected.LastToken.ShouldBe(stop.Token);
        provider.Dispose();
        selected.DisposeCalls.ShouldBe(1);
    }

    [Fact]
    public void ActivateIndependentGenericProvidersFromOneCollection()
    {
        var services = Services();
        services.AddFencingTokenIssuerSupport<ProbeIssuer>();
        using var first = services.BuildServiceProvider();
        using var second = services.BuildServiceProvider();
        var left = first.GetRequiredService<IFencingTokenProvider>().ShouldBeOfType<ProbeIssuer>();
        var right = second.GetRequiredService<IFencingTokenProvider>().ShouldBeOfType<ProbeIssuer>();
        left.ShouldNotBeSameAs(right);
        first.GetRequiredService<IFencingTokenIssuer>().ShouldNotBeSameAs(second.GetRequiredService<IFencingTokenIssuer>());
        first.Dispose();
        second.Dispose();
        left.DisposeCalls.ShouldBe(1);
        right.DisposeCalls.ShouldBe(1);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ProjectAnExplicitSelectedIssuerBeforeOrAfterDefaultRegistration(bool before)
    {
        var services = Services();
        using var custom = new ProbeIssuer(new ConstructionCount());
        if (before) { services.AddSingleton<IFencingTokenProvider>(custom); }
        services.AddFencingTokenIssuerSupport<ProbeIssuer>();
        if (!before) { services.AddSingleton<IFencingTokenProvider>(custom); }
        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IFencingTokenProvider>().ShouldBeSameAs(custom);
        (await provider.GetRequiredService<IFencingTokenIssuer>().IssueTokenAsync("custom", CancellationToken.None)).ShouldBe(1);
        custom.Calls.ShouldBe(1);
        provider.GetRequiredService<ConstructionCount>().Value.ShouldBe(0);
        provider.Dispose();
        custom.DisposeCalls.ShouldBe(0); // The supplied instance is caller owned.
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RefuseReadOnlySelectedReplacementWithoutConstructingHiddenCounter(bool before)
    {
        var services = Services();
        var readOnly = new ReadOnlyProvider();
        if (before) { services.AddSingleton<IFencingTokenProvider>(readOnly); }
        services.AddFencingTokenIssuerSupport<ProbeIssuer>();
        if (!before) { services.AddSingleton<IFencingTokenProvider>(readOnly); }
        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IFencingTokenProvider>().ShouldBeSameAs(readOnly);
        var error = Should.Throw<InvalidOperationException>(() => provider.GetRequiredService<IFencingTokenIssuer>());
        error.Message.ShouldContain("selected IFencingTokenProvider cannot mint");
        provider.GetRequiredService<ConstructionCount>().Value.ShouldBe(0);
    }

    [Fact]
    public void PreserveReadOnlySupportWithoutAdvertisingAnIssuer()
    {
        var services = Services();
        services.AddFencingTokenSupport<ReadOnlyProvider>();
        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IFencingTokenProvider>().ShouldBeOfType<ReadOnlyProvider>();
        provider.GetService<IFencingTokenIssuer>().ShouldBeNull();
    }

    [Fact]
    public async Task IntentionallyShareTheExternallyOwnedInstanceAcrossContainers()
    {
        using var shared = new ProbeIssuer(new ConstructionCount());
        var services = Services();
        services.AddFencingTokenIssuerSupport(shared);
        using var first = services.BuildServiceProvider();
        using var second = services.BuildServiceProvider();
        first.GetRequiredService<IFencingTokenProvider>().ShouldBeSameAs(shared);
        second.GetRequiredService<IFencingTokenProvider>().ShouldBeSameAs(shared);
        (await first.GetRequiredService<IFencingTokenIssuer>().IssueTokenAsync("shared", CancellationToken.None)).ShouldBe(1);
        (await second.GetRequiredService<IFencingTokenIssuer>().GetTokenAsync("shared", CancellationToken.None)).ShouldBe(1);
        first.Dispose();
        second.Dispose();
        shared.DisposeCalls.ShouldBe(0);
    }

    [Fact]
    public void PreserveAnIndependentIssuerOverrideWithoutClaimingItIsSelectedReaderBound()
    {
        using var independent = new ProbeIssuer(new ConstructionCount());
        var services = Services();
        services.AddSingleton<IFencingTokenIssuer>(independent);
        services.AddFencingTokenIssuerSupport<ProbeIssuer>();
        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IFencingTokenIssuer>().ShouldBeSameAs(independent);
        provider.GetRequiredService<IFencingTokenProvider>().ShouldNotBeSameAs(independent);
        // Explicit overrides bypass the projection; callers must configure one authority themselves.
    }

    [Fact]
    public async Task PreserveTheExactFaultAndCancellationFromTheSelectedIssuer()
    {
        using var selected = new ProbeIssuer(new ConstructionCount());
        var services = Services();
        services.AddFencingTokenIssuerSupport(selected);
        using var provider = services.BuildServiceProvider();
        var issuer = provider.GetRequiredService<IFencingTokenIssuer>();
        var primary = new IOException("selected primary");
        selected.Failure = primary;
        (await Should.ThrowAsync<IOException>(async () => await issuer.IssueTokenAsync("r", CancellationToken.None))).ShouldBeSameAs(primary);
        selected.Failure = null;
        using var stop = new CancellationTokenSource();
        await stop.CancelAsync();
        var error = await Should.ThrowAsync<OperationCanceledException>(async () => await issuer.GetTokenAsync("r", stop.Token));
        error.CancellationToken.ShouldBe(stop.Token);
    }

    private static ServiceCollection Services()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ConstructionCount>();
        services.AddSingleton<Dependency>();
        return services;
    }

    private sealed class Dependency { public Dependency() { } }
    private sealed class ConstructionCount { public ConstructionCount() { } public int Value { get; set; } }
    private sealed class ReadOnlyProvider : IFencingTokenProvider
    {
        public ReadOnlyProvider() { }
        public ValueTask<long?> GetTokenAsync(string resourceId, CancellationToken cancellationToken) => ValueTask.FromResult<long?>(null);
        public ValueTask<bool> ValidateTokenAsync(string resourceId, long token, CancellationToken cancellationToken) => ValueTask.FromResult(false);
    }
    private sealed class ProbeIssuer : IFencingTokenIssuer, IDisposable
    {
        private long _mark;
        [ActivatorUtilitiesConstructor]
        public ProbeIssuer(ConstructionCount count) { count.Value++; }
        public ProbeIssuer(ConstructionCount count, Dependency dependency) : this(count)
        { _ = dependency; UsedLongestConstructor = true; }
        public bool UsedLongestConstructor { get; }
        public int Calls { get; private set; }
        public int DisposeCalls { get; private set; }
        public string? LastResource { get; private set; }
        public CancellationToken LastToken { get; private set; }
        public Exception? Failure { get; set; }
        public ValueTask<long> IssueTokenAsync(string resourceId, CancellationToken cancellationToken)
        { Observe(resourceId, cancellationToken); return ValueTask.FromResult(++_mark); }
        public ValueTask<long?> GetTokenAsync(string resourceId, CancellationToken cancellationToken)
        { Observe(resourceId, cancellationToken); return ValueTask.FromResult<long?>(_mark == 0 ? null : _mark); }
        public ValueTask<bool> ValidateTokenAsync(string resourceId, long token, CancellationToken cancellationToken)
        { Observe(resourceId, cancellationToken); return ValueTask.FromResult(_mark > 0 && token >= _mark); }
        public void Dispose() => DisposeCalls++;
        private void Observe(string resourceId, CancellationToken cancellationToken)
        {
            Calls++; LastResource = resourceId; LastToken = cancellationToken;
            cancellationToken.ThrowIfCancellationRequested();
            if (Failure is not null) { throw Failure; }
        }
    }
}
