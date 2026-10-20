// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.A3.Authentication;
using Excalibur.A3.Authorization;
using Excalibur.A3.Authorization.Grants;
using Excalibur.A3.Authorization.PolicyData;
using Excalibur.A3.Authorization.Stores.InMemory;
using Excalibur.Dispatch;
using StoreGrant = Excalibur.A3.Authorization.Grant;

namespace Excalibur.Tests.A3.Authorization;

[Trait("Category", "Unit")]
[Trait("Component", "A3")]
[Trait("Pattern", "Authorization")]
public sealed class CoreAuthorizationPolicyProviderShould
{
	private sealed class Clock : TimeProvider
	{
		internal DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
		public override DateTimeOffset GetUtcNow() => Now;
	}

	private static (CoreAuthorizationPolicyProvider Provider, IAuthenticationToken User, ITenantContext Tenant) Create(
		IGrantStore store, IActivityGroupStore? groups = null, TimeProvider? clock = null)
	{
		var user = A.Fake<IAuthenticationToken>();
		A.CallTo(() => user.UserId).Returns("u");
		var tenant = A.Fake<ITenantContext>();
		A.CallTo(() => tenant.TenantId).Returns("a");
		groups ??= A.Fake<IActivityGroupStore>();
		A.CallTo(() => groups.FindActivityGroupsAsync(A<string>._, A<CancellationToken>._))
			.Returns(new Dictionary<string, IReadOnlyCollection<string>>());
		return (new(new ActivityGroups(groups), new UserGrants(store), user, tenant, clock), user, tenant);
	}

	private static StoreGrant Grant(string user = "u", string tenant = "a", DateTimeOffset? expiry = null) =>
		new(user, null, tenant, GrantType.Activity, "Read", expiry, "issuer", DateTimeOffset.UtcNow);

	[Fact]
	public async Task RefreshWarmedScopeAfterAtomicRevocation()
	{
		var store = new InMemoryGrantStore();
		await store.SaveGrantAsync(Grant(), CancellationToken.None);
		await store.SaveGrantAsync(Grant(tenant: "b"), CancellationToken.None);
		var (provider, _, _) = Create(store);
		(await provider.GetPolicyAsync()).HasGrant("Read").ShouldBeTrue();
		await store.RevokeAllAsync(new GrantRevocationRequest(Guid.NewGuid(), "u", GrantRevocationScope.ForTenant("a"), "admin"), CancellationToken.None);
		(await provider.GetPolicyAsync()).HasGrant("Read").ShouldBeFalse();
		(await store.GetAllGrantsAsync("u", true, CancellationToken.None)).ShouldHaveSingleItem().TenantId.ShouldBe("b");
	}

	[Fact]
	public async Task RejectExpiredSnapshotAtCurrentClockWithoutRevisionChange()
	{
		var clock = new Clock();
		var store = A.Fake<IGrantStore>();
		var atomic = A.Fake<IAtomicGrantRevocationStore>();
		A.CallTo(() => store.GetService(typeof(IGrantQueryStore))).Returns(A.Fake<IGrantQueryStore>());
		A.CallTo(() => store.GetService(typeof(IAtomicGrantRevocationStore))).Returns(atomic);
		A.CallTo(() => atomic.ReadUserRevisionAsync("u", A<CancellationToken>._)).Returns(1L);
		A.CallTo(() => atomic.ReadUserSnapshotAsync("u", false, A<CancellationToken>._))
			.Returns(new UserGrantSnapshot("u", 1, [Grant(expiry: clock.Now.AddMinutes(1))]));
		var (provider, _, _) = Create(store, clock: clock);
		(await provider.GetPolicyAsync()).HasGrant("Read").ShouldBeTrue();
		clock.Now = clock.Now.AddMinutes(2);
		(await provider.GetPolicyAsync()).HasGrant("Read").ShouldBeFalse();
	}

	[Fact]
	public async Task RefreshChangedUserAndTenantWithinOneScope()
	{
		var store = new InMemoryGrantStore();
		await store.SaveGrantAsync(Grant(), CancellationToken.None);
		await store.SaveGrantAsync(Grant(user: "other", tenant: "b"), CancellationToken.None);
		var (provider, user, tenant) = Create(store);
		(await provider.GetPolicyAsync()).HasGrant("Read").ShouldBeTrue();
		A.CallTo(() => tenant.TenantId).Returns("b");
		(await provider.GetPolicyAsync()).HasGrant("Read").ShouldBeFalse();
		A.CallTo(() => user.UserId).Returns("other");
		(await provider.GetPolicyAsync()).HasGrant("Read").ShouldBeTrue();
	}

	[Fact]
	public async Task RefreshStoresWithoutRevisionCapabilityOnEveryAcquisition()
	{
		var store = A.Fake<IGrantStore>();
		var query = A.Fake<IGrantQueryStore>();
		A.CallTo(() => store.GetService(typeof(IGrantQueryStore))).Returns(query);
		A.CallTo(() => store.GetService(typeof(IAtomicGrantRevocationStore))).Returns(null);
		var grants = new Dictionary<string, object> { [SegmentedKey.Compose("a", GrantType.Activity, "Read")] = Grant() };
		A.CallTo(() => query.FindUserGrantsAsync("u", A<CancellationToken>._)).ReturnsLazily(() => grants);
		var (provider, _, _) = Create(store);
		(await provider.GetPolicyAsync()).HasGrant("Read").ShouldBeTrue();
		grants.Clear();
		(await provider.GetPolicyAsync()).HasGrant("Read").ShouldBeFalse();
		A.CallTo(() => query.FindUserGrantsAsync("u", A<CancellationToken>._)).MustHaveHappenedTwiceExactly();
	}

	[Fact]
	public async Task RefuseSnapshotWithForeignIdentity()
	{
		var store = A.Fake<IGrantStore>();
		var atomic = A.Fake<IAtomicGrantRevocationStore>();
		A.CallTo(() => store.GetService(typeof(IGrantQueryStore))).Returns(A.Fake<IGrantQueryStore>());
		A.CallTo(() => store.GetService(typeof(IAtomicGrantRevocationStore))).Returns(atomic);
		A.CallTo(() => atomic.ReadUserRevisionAsync("u", A<CancellationToken>._)).Returns(1L);
		A.CallTo(() => atomic.ReadUserSnapshotAsync("u", false, A<CancellationToken>._))
			.Returns(new UserGrantSnapshot("other", 1, [Grant(user: "other")]));
		var (provider, _, _) = Create(store);
		await Should.ThrowAsync<InvalidOperationException>(provider.GetPolicyAsync);
	}

	[Fact]
	public async Task BoundRetriesWhenSnapshotRevisionNeverMatches()
	{
		var store = A.Fake<IGrantStore>();
		var atomic = A.Fake<IAtomicGrantRevocationStore>();
		A.CallTo(() => store.GetService(typeof(IGrantQueryStore))).Returns(A.Fake<IGrantQueryStore>());
		A.CallTo(() => store.GetService(typeof(IAtomicGrantRevocationStore))).Returns(atomic);
		A.CallTo(() => atomic.ReadUserRevisionAsync("u", A<CancellationToken>._)).Returns(1L);
		A.CallTo(() => atomic.ReadUserSnapshotAsync("u", false, A<CancellationToken>._))
			.Returns(new UserGrantSnapshot("u", 2, [Grant()]));
		var (provider, _, _) = Create(store);
		await Should.ThrowAsync<InvalidOperationException>(provider.GetPolicyAsync);
		A.CallTo(() => atomic.ReadUserSnapshotAsync("u", false, A<CancellationToken>._)).MustHaveHappened(3, Times.Exactly);
	}
}
