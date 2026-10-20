using Excalibur.A3.Authentication;
using Excalibur.A3.Authorization;
using Excalibur.A3.Authorization.Grants;
using Excalibur.A3.Authorization.PolicyData;
using Excalibur.A3.Authorization.Stores.InMemory;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.Tests.A3.Authorization;

[Trait("Category", "Unit")]
[Trait("Component", "A3")]
[Trait("Pattern", "AtomicRevocation")]
public sealed class AuthorizationPolicyRevisionShould
{
	private static AuthorizationPolicyProvider Provider(IGrantStore store, IDistributedCache cache, TimeProvider? clock = null,
		IAuthenticationToken? currentToken = null)
	{
		var groups = A.Fake<IActivityGroupStore>();
		A.CallTo(() => groups.FindActivityGroupsAsync("tenant", A<CancellationToken>._))
			.Returns(new Dictionary<string, IReadOnlyCollection<string>>());
		var token = currentToken ?? A.Fake<IAuthenticationToken>();
		A.CallTo(() => token.UserId).Returns("user");
		var tenant = A.Fake<ITenantContext>();
		A.CallTo(() => tenant.TenantId).Returns("tenant");
		return new AuthorizationPolicyProvider(new ActivityGroups(groups), new UserGrants(store), token, tenant,
			Options.Create(new AuthorizationCacheOptions()), cache, clock);
	}
	private static Task Add(InMemoryGrantStore store) => store.SaveGrantAsync(
		new Excalibur.A3.Authorization.Grant("user", null, "tenant", GrantType.Activity, "Read", null, "admin", DateTimeOffset.UtcNow), CancellationToken.None);
	private static Task<GrantRevocationResult> Revoke(InMemoryGrantStore store) => store.RevokeAllAsync(
		new GrantRevocationRequest(Guid.NewGuid(), "user", GrantRevocationScope.ForTenant("tenant"), "admin"), CancellationToken.None);
	private static IGrantStore CacheableAuthority(InMemoryGrantStore authority)
	{
		// The in-memory authority supplies deterministic atomic interleavings; this adapter advertises
		// durable lifetime solely to exercise the distributed-cache protocol within one test lifetime.
		var store = A.Fake<IGrantStore>();
		A.CallTo(() => store.GetService(typeof(IAtomicGrantRevocationStore))).Returns(authority);
		A.CallTo(() => store.GetService(typeof(IGrantQueryStore))).Returns(authority);
		A.CallTo(() => store.GetService(typeof(IDurableGrantStore))).Returns(A.Fake<IDurableGrantStore>());
		return store;
	}
	[Fact]
	public async Task Reject_identity_changes_during_the_final_revision_read()
	{
		var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var reads = 0;
		var atomic = A.Fake<IAtomicGrantRevocationStore>();
		A.CallTo(() => atomic.ReadUserRevisionAsync("user", A<CancellationToken>._)).ReturnsLazily(async () =>
		{
			if (Interlocked.Increment(ref reads) == 2)
			{
				entered.TrySetResult();
				await release.Task;
			}
			return 1L;
		});
		A.CallTo(() => atomic.ReadUserSnapshotAsync("user", false, A<CancellationToken>._))
			.Returns(new UserGrantSnapshot("user", 1, []));
		var store = A.Fake<IGrantStore>();
		A.CallTo(() => store.GetService(typeof(IAtomicGrantRevocationStore))).Returns(atomic);
		A.CallTo(() => store.GetService(typeof(IGrantQueryStore))).Returns(A.Fake<IGrantQueryStore>());
		var token = A.Fake<IAuthenticationToken>();
		var acquisition = Provider(store, A.Fake<IDistributedCache>(), currentToken: token).GetPolicyAsync();
		try
		{
			await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
			A.CallTo(() => token.UserId).Returns("different-user");
			release.TrySetResult();
			await Should.ThrowAsync<InvalidOperationException>(() => acquisition.WaitAsync(TimeSpan.FromSeconds(10)));
		}
		finally
		{
			release.TrySetResult();
			try { await acquisition.WaitAsync(TimeSpan.FromSeconds(10)); }
			catch (InvalidOperationException) { }
		}
	}
	[Fact]
	public async Task Never_reuse_a_volatile_revision_after_store_replacement()
	{
		using var cacheOwner = new Microsoft.Extensions.DependencyInjection.ServiceCollection().AddDistributedMemoryCache().BuildServiceProvider();
		var cache = cacheOwner.GetRequiredService<IDistributedCache>();
		var first = new InMemoryGrantStore();
		await Add(first);
		(await Provider(first, cache).GetPolicyAsync()).HasGrant("Read").ShouldBeTrue();
		var replacement = new InMemoryGrantStore();
		await replacement.SaveGrantAsync(new Excalibur.A3.Authorization.Grant("user", null, "tenant", GrantType.Activity, "Write", null, "admin", DateTimeOffset.UtcNow), CancellationToken.None);
		(await replacement.ReadUserRevisionAsync("user", CancellationToken.None)).ShouldBe(await first.ReadUserRevisionAsync("user", CancellationToken.None));
		var policy = await Provider(replacement, cache).GetPolicyAsync();
		policy.HasGrant("Read").ShouldBeFalse();
		policy.HasGrant("Write").ShouldBeTrue();
	}
	[Fact]
	public async Task Reject_expired_grant_on_cache_hit_without_a_revision_change()
	{
		var clock = new ControlledClock();
		var store = new InMemoryGrantStore(clock);
		using var cacheOwner = new Microsoft.Extensions.DependencyInjection.ServiceCollection().AddDistributedMemoryCache().BuildServiceProvider();
		var cache = cacheOwner.GetRequiredService<IDistributedCache>();
		await store.SaveGrantAsync(new Excalibur.A3.Authorization.Grant("user", null, "tenant", GrantType.Activity, "Read",
			clock.GetUtcNow().AddMinutes(1), "admin", clock.GetUtcNow()), CancellationToken.None);
		var provider = Provider(CacheableAuthority(store), cache, clock);
		(await provider.GetPolicyAsync()).HasGrant("Read").ShouldBeTrue();
		var revision = await store.ReadUserRevisionAsync("user", CancellationToken.None);
		clock.Now = clock.Now.AddMinutes(1);
		(await provider.GetPolicyAsync()).HasGrant("Read").ShouldBeFalse();
		(await store.ReadUserRevisionAsync("user", CancellationToken.None)).ShouldBe(revision);
	}
	[Fact]
	public async Task Refresh_an_already_acquired_scope_after_revocation()
	{
		var store = new InMemoryGrantStore();
		using var cacheOwner = new Microsoft.Extensions.DependencyInjection.ServiceCollection().AddDistributedMemoryCache().BuildServiceProvider();
		var cache = cacheOwner.GetRequiredService<IDistributedCache>();
		await Add(store);
		var provider = Provider(store, cache);
		(await provider.GetPolicyAsync()).HasGrant("Read").ShouldBeTrue();
		await Revoke(store);
		(await provider.GetPolicyAsync()).HasGrant("Read").ShouldBeFalse();
	}
	[Fact]
	public async Task Fence_a_late_old_fill_and_refresh_its_original_acquisition()
	{
		var store = new InMemoryGrantStore();
		using var cacheOwner = new Microsoft.Extensions.DependencyInjection.ServiceCollection().AddDistributedMemoryCache().BuildServiceProvider();
		var backing = cacheOwner.GetRequiredService<IDistributedCache>();
		var cache = new PausedGrantCache(backing);
		await Add(store);
		var cacheable = CacheableAuthority(store);
		var old = Provider(cacheable, cache).GetPolicyAsync();
		try
		{
			await cache.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
			await Revoke(store);
			(await Provider(cacheable, cache).GetPolicyAsync()).HasGrant("Read").ShouldBeFalse();
			cache.Release.TrySetResult();
			(await old.WaitAsync(TimeSpan.FromSeconds(10))).HasGrant("Read").ShouldBeFalse();
			(await Provider(cacheable, cache).GetPolicyAsync()).HasGrant("Read").ShouldBeFalse();
		}
		finally
		{
			cache.Release.TrySetResult();
			await old.WaitAsync(TimeSpan.FromSeconds(10));
		}
	}
	private sealed class PausedGrantCache(IDistributedCache inner) : IDistributedCache
	{
		private int _paused;
		internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
		internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
		public byte[]? Get(string key) => inner.Get(key);
		public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => inner.GetAsync(key, token);
		public void Refresh(string key) => inner.Refresh(key);
		public Task RefreshAsync(string key, CancellationToken token = default) => inner.RefreshAsync(key, token);
		public void Remove(string key) => inner.Remove(key);
		public Task RemoveAsync(string key, CancellationToken token = default) => inner.RemoveAsync(key, token);
		public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => inner.Set(key, value, options);
		public async Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
		{
			if (key.Contains("grants", StringComparison.OrdinalIgnoreCase) && Interlocked.CompareExchange(ref _paused, 1, 0) == 0)
			{
				Entered.TrySetResult();
				await Release.Task.WaitAsync(token);
			}
			await inner.SetAsync(key, value, options, token);
		}
	}
	private sealed class ControlledClock : TimeProvider
	{
		internal DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
		public override DateTimeOffset GetUtcNow() => Now;
	}
}


