// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Data.Sharding;
using Excalibur.EventSourcing.Sharding;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.Tests.Sharding;

[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
[Trait("Pattern", "Regression")]
public sealed class TenantShardHealthCheckShould
{
	private static readonly string[] OrderedShardIds = ["broken", "healthy"];
	[Fact]
	public async Task ReadPhysicalShardIdsWithoutTenantOrDefaultRoutingAndPreserveFailures()
	{
		var map = new PhysicalMap("healthy", "broken");
		var calls = new System.Collections.Concurrent.ConcurrentBag<string>();
		var original = new InvalidOperationException("missing Timestamp");
		var resolver = new ProbeResolver((id, _) =>
		{
			calls.Add(map.GetShardById(id).ShardId);
			return id == "broken" ? Task.FromException(original) : Task.CompletedTask;
		});
		var result = await new TenantShardHealthCheck(map, resolver).CheckHealthAsync(new(), TestContext.Current.CancellationToken);
		result.Status.ShouldBe(HealthStatus.Degraded);
		result.Data["healthyShards"].ShouldBe(1);
		result.Data["unhealthyShards"].ShouldBe(1);
		result.Data["failedShardIds"].ShouldBe("broken");
		calls.OrderBy(id => id).ShouldBe(OrderedShardIds);
		map.TenantLookups.ShouldBe(0);
		resolver.TenantResolutions.ShouldBe(0);
		var error = result.Exception.ShouldBeOfType<AggregateException>();
		error.InnerExceptions.Single().InnerException.ShouldBeSameAs(original);
	}

	[Fact]
	public async Task ReportHealthyOnlyAfterEveryProbeServedARead()
	{
		var calls = 0;
		var resolver = new ProbeResolver((_, _) => { Interlocked.Increment(ref calls); return Task.CompletedTask; });
		var result = await new TenantShardHealthCheck(new PhysicalMap("one", "two"), resolver).CheckHealthAsync(new(), TestContext.Current.CancellationToken);
		result.Status.ShouldBe(HealthStatus.Healthy);
		calls.ShouldBe(2);
	}

	[Fact]
	public async Task ReportUnhealthyWhenAllPhysicalReadsFail()
	{
		var resolver = new ProbeResolver((_, _) => Task.FromException(new IOException("unavailable")));
		var result = await new TenantShardHealthCheck(new PhysicalMap("one", "two"), resolver).CheckHealthAsync(new(), TestContext.Current.CancellationToken);
		result.Status.ShouldBe(HealthStatus.Unhealthy);
		result.Data["failedShardIds"].ShouldBe("one, two");
	}

	[Fact]
	public async Task ReportUnhealthyWithoutAReadinessCapability()
	{
		var result = await new TenantShardHealthCheck(new PhysicalMap("one")).CheckHealthAsync(new(), TestContext.Current.CancellationToken);
		result.Status.ShouldBe(HealthStatus.Unhealthy);
		result.Description.ShouldContain("not verified");
	}

	[Fact]
	public async Task ReportUnhealthyWhenNoShardsAreRegistered()
	{
		var result = await new TenantShardHealthCheck(new PhysicalMap()).CheckHealthAsync(new(), TestContext.Current.CancellationToken);
		result.Status.ShouldBe(HealthStatus.Unhealthy);
		result.Description.ShouldContain("No physical shards");
	}

	[Fact]
	public async Task PropagateCallerCancellationBeforeResolvingAProbe()
	{
		using var canceled = new CancellationTokenSource(); canceled.Cancel();
		var calls = 0;
		var resolver = new ProbeResolver((_, _) => { calls++; return Task.CompletedTask; });
		await Should.ThrowAsync<OperationCanceledException>(() => new TenantShardHealthCheck(new PhysicalMap("one"), resolver).CheckHealthAsync(new(), canceled.Token));
		calls.ShouldBe(0);
	}

	[Fact]
	public async Task BoundFanOutAndContinueAllShardsAfterTheFirstWave()
	{
		var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var firstWave = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var active = 0; var peak = 0; var calls = 0;
		var resolver = new ProbeResolver(async (_, token) =>
		{
			Interlocked.Increment(ref calls);
			var now = Interlocked.Increment(ref active);
			InterlockedExtensionsMax(ref peak, now);
			if (now == 2) { firstWave.TrySetResult(); }
			try { await release.Task.WaitAsync(token); }
			finally { Interlocked.Decrement(ref active); }
		});
		var check = new TenantShardHealthCheck(new PhysicalMap("a", "b", "c", "d"), resolver, 2);
		var resultTask = check.CheckHealthAsync(new(), TestContext.Current.CancellationToken);
		try { await firstWave.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken); peak.ShouldBe(2); }
		finally { release.TrySetResult(); }
		(await resultTask).Status.ShouldBe(HealthStatus.Healthy);
		calls.ShouldBe(4); peak.ShouldBe(2);
	}

	[Fact]
	public void RegisterFiniteTimeoutAndSnapshotSettingsWithoutReflectionActivation()
	{
		var options = new TenantShardHealthCheckOptions { MaxConcurrentProbes = 2, Timeout = TimeSpan.FromSeconds(7) };
		var services = new ServiceCollection();
		services.AddSingleton<ITenantShardMap>(new PhysicalMap("one"));
		services.AddSingleton<ITenantStoreResolver<IEventStore>>(new ProbeResolver((_, _) => Task.CompletedTask));
		services.AddHealthChecks().AddTenantShardHealthCheck(options);
		options.MaxConcurrentProbes = 0; options.Timeout = TimeSpan.Zero;
		using var provider = services.BuildServiceProvider();
		var registration = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations.Single();
		registration.Timeout.ShouldBe(TimeSpan.FromSeconds(7));
		registration.Factory(provider).ShouldBeOfType<TenantShardHealthCheck>();
	}

	[Fact]
	public void PhysicalLookupNeverFallsBackToAHealthyDefault()
	{
		var healthy = new ShardInfo("healthy", "healthy-connection");
		var map = new InMemoryTenantShardMap(new() { ["healthy"] = healthy }, new(), new() { DefaultShardId = "healthy" });
		map.GetShardInfo("unmapped-tenant").ShouldBeSameAs(healthy);
		Should.Throw<KeyNotFoundException>(() => map.GetShardById("not-a-shard"));
	}


    [Fact]
    public async Task CancelAnAlreadyActiveProbeWaveAndJoinItsCancellation()
    {
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var wave = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var active = 0;
        var canceled = 0;
        var resolver = new ProbeResolver(async (_, token) =>
        {
            if (Interlocked.Increment(ref active) == 2) { wave.TrySetResult(); }
            try { await Task.Delay(Timeout.InfiniteTimeSpan, token); }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            { Interlocked.Increment(ref canceled); throw; }
            finally { Interlocked.Decrement(ref active); }
        });
        var check = new TenantShardHealthCheck(new PhysicalMap("a", "b", "c", "d"), resolver, 2);
        var running = check.CheckHealthAsync(new(), caller.Token);
        try { await wave.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken); }
        finally { caller.Cancel(); }
        await Should.ThrowAsync<OperationCanceledException>(() => running);
        active.ShouldBe(0);
        canceled.ShouldBe(2);
    }

    [Fact]
    public async Task EnforceRegistrationTimeoutThroughTheActualHealthCheckService()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var canceled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resolver = new ProbeResolver(async (_, token) =>
        {
            started.TrySetResult();
            try { await Task.Delay(Timeout.InfiniteTimeSpan, token); }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            { canceled.TrySetResult(); throw; }
        });
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ITenantShardMap>(new PhysicalMap("one"));
        services.AddSingleton<ITenantStoreResolver<IEventStore>>(resolver);
        services.AddHealthChecks().AddTenantShardHealthCheck(new TenantShardHealthCheckOptions
        { Timeout = TimeSpan.FromMilliseconds(250), MaxConcurrentProbes = 1 });
        await using var provider = services.BuildServiceProvider();
        var run = provider.GetRequiredService<HealthCheckService>().CheckHealthAsync(TestContext.Current.CancellationToken);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        var report = await run.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        report.Status.ShouldBe(HealthStatus.Unhealthy);
        report.Entries["tenant-shard"].Exception.ShouldBeAssignableTo<OperationCanceledException>();
        await canceled.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        TestContext.Current.CancellationToken.IsCancellationRequested.ShouldBeFalse();
    }

	private static void InterlockedExtensionsMax(ref int target, int value)
	{
		int current;
		do { current = Volatile.Read(ref target); if (current >= value) { return; } }
		while (Interlocked.CompareExchange(ref target, value, current) != current);
	}

	private sealed class ProbeResolver(Func<string, CancellationToken, Task> read) : ITenantStoreResolver<IEventStore>, ITenantShardReadinessProbe
	{
		internal int TenantResolutions;
		public IEventStore Resolve(string tenantId) { Interlocked.Increment(ref TenantResolutions); throw new InvalidOperationException("Health must not route a tenant."); }
		public Task ProbeShardAsync(string shardId, CancellationToken token) => read(shardId, token);
	}
	private sealed class PhysicalMap(params string[] ids) : ITenantShardMap
	{
		internal int TenantLookups;
		public ShardInfo GetShardInfo(string tenantId) { Interlocked.Increment(ref TenantLookups); return new(ids[0], "healthy-default"); }
		public ShardInfo GetShardById(string shardId) => ids.Contains(shardId) ? new(shardId, shardId + "-physical") : throw new KeyNotFoundException(shardId);
		public IReadOnlyCollection<string> GetRegisteredShardIds() => ids;
	}
}
