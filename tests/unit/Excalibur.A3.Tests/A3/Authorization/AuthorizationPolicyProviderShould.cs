// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.A3.Authentication;
using Excalibur.A3.Authorization;
using Excalibur.A3.Authorization.PolicyData;
using Excalibur.Domain;

using Microsoft.Extensions.Caching.Distributed;

namespace Excalibur.Tests.A3.Authorization;

/// <summary>
/// Unit tests for <see cref="AuthorizationPolicyProvider"/>.
/// </summary>
[Collection("ApplicationContext")]
[Trait("Category", "Unit")]
[Trait("Component", "A3")]
[Trait("Feature", "Authorization")]
public sealed class AuthorizationPolicyProviderShould : IDisposable
{
	public AuthorizationPolicyProviderShould()
	{
		// Grant construction reads ApplicationContext.ApplicationName. The cache key no longer reads
		// anything from this context -- it is a pure function of its inputs.
		ApplicationContext.Init(new Dictionary<string, string?>
		{
			["ApplicationName"] = "TestApp",
		});
	}

	public void Dispose() => ApplicationContext.Reset();

	[Fact]
	public async Task ThrowWhenUserIdIsNull()
	{
		// Arrange
		var currentUser = A.Fake<IAuthenticationToken>();
		A.CallTo(() => currentUser.UserId).Returns(null);

		var tenantId = A.Fake<ITenantContext>();
		A.CallTo(() => tenantId.TenantId).Returns("tenant-1");

		var sut = new AuthorizationPolicyProvider(
			activityGroups: null!,
			userGrants: null!,
			currentUser: currentUser,
			cache: A.Fake<IDistributedCache>(),
			tenantContext: tenantId,
			cacheOptions: Microsoft.Extensions.Options.Options.Create(new AuthorizationCacheOptions()));

		// Act & Assert
		var exception = await Should.ThrowAsync<InvalidOperationException>(sut.GetPolicyAsync());
		exception.Message.ShouldContain("User ID is required");
	}

	[Fact]
	public async Task ThrowWhenTenantIdIsEmpty()
	{
		// Arrange
		var currentUser = A.Fake<IAuthenticationToken>();
		A.CallTo(() => currentUser.UserId).Returns("user-1");

		var tenantId = A.Fake<ITenantContext>();
		A.CallTo(() => tenantId.TenantId).Returns(string.Empty);

		var sut = new AuthorizationPolicyProvider(
			activityGroups: null!,
			userGrants: null!,
			currentUser: currentUser,
			cache: A.Fake<IDistributedCache>(),
			tenantContext: tenantId,
			cacheOptions: Microsoft.Extensions.Options.Options.Create(new AuthorizationCacheOptions()));

		// Act & Assert
		var exception = await Should.ThrowAsync<InvalidOperationException>(sut.GetPolicyAsync());
		exception.Message.ShouldContain("Tenant ID is required");
	}

	[Fact]
	public async Task ThrowWhenTenantIdValueIsNull()
	{
		// Arrange
		var currentUser = A.Fake<IAuthenticationToken>();
		A.CallTo(() => currentUser.UserId).Returns("user-1");

		var tenantId = A.Fake<ITenantContext>();
		A.CallTo(() => tenantId.TenantId).Returns(null!);

		var sut = new AuthorizationPolicyProvider(
			activityGroups: null!,
			userGrants: null!,
			currentUser: currentUser,
			cache: A.Fake<IDistributedCache>(),
			tenantContext: tenantId,
			cacheOptions: Microsoft.Extensions.Options.Options.Create(new AuthorizationCacheOptions()));

		// Act & Assert
		var exception = await Should.ThrowAsync<InvalidOperationException>(sut.GetPolicyAsync());
		exception.Message.ShouldContain("Tenant ID is required");
	}

	/// <summary>
	/// Regression lock for Excalibur.Dispatch-irgznt: ASP.NET Core evaluates <c>HandleRequirementAsync</c>
	/// once per requirement, so a request checking more than one activity/resource previously reloaded
	/// grants and activity groups from the distributed cache and re-deserialized them on every call within
	/// the same scope -- even though the answer cannot change within one request. The provider is
	/// registered scoped, so memoizing on the instance is per-request memoization.
	/// </summary>
	[Fact]
	public async Task MemoizePolicyWithinOneScope_CallingTheStoresOnlyOnce()
	{
		// Arrange -- distributed cache always misses, forcing every call through to the stores, so a
		// second GetPolicyAsync() call is only cheap if the provider itself memoized the first result.
		var currentUser = A.Fake<IAuthenticationToken>();
		A.CallTo(() => currentUser.UserId).Returns("user-1");

		var tenantContext = A.Fake<ITenantContext>();
		A.CallTo(() => tenantContext.TenantId).Returns("tenant-1");

		var cache = A.Fake<IDistributedCache>();
		A.CallTo(() => cache.GetAsync(A<string>._, A<CancellationToken>._)).Returns((byte[]?)null);

		var queryStore = A.Fake<IGrantQueryStore>();
		A.CallTo(() => queryStore.FindUserGrantsAsync("user-1", A<CancellationToken>._))
			.Returns(new Dictionary<string, object>());

		var grantStore = A.Fake<IGrantStore>();
		A.CallTo(() => grantStore.GetService(typeof(IGrantQueryStore))).Returns(queryStore);

		var activityGroupStore = A.Fake<IActivityGroupStore>();
		A.CallTo(() => activityGroupStore.FindActivityGroupsAsync(A<string>._, A<CancellationToken>._))
			.Returns(new Dictionary<string, IReadOnlyCollection<string>>());

		var sut = new AuthorizationPolicyProvider(
			activityGroups: new ActivityGroups(activityGroupStore),
			userGrants: new UserGrants(grantStore),
			currentUser: currentUser,
			cache: cache,
			tenantContext: tenantContext,
			cacheOptions: Microsoft.Extensions.Options.Options.Create(new AuthorizationCacheOptions()));

		// Act -- two calls, as two requirements evaluated in one ASP.NET Core authorization check would.
		var first = await sut.GetPolicyAsync();
		var second = await sut.GetPolicyAsync();

		// Assert -- SAFETY: no second round-trip to either store.
		A.CallTo(() => queryStore.FindUserGrantsAsync("user-1", A<CancellationToken>._)).MustHaveHappenedOnceExactly();
		A.CallTo(() => activityGroupStore.FindActivityGroupsAsync(A<string>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();

		// LIVENESS -- the memoized result is still the SAME, correctly-built policy, not a stale/empty stand-in.
		second.ShouldBeSameAs(first);
	}
}
