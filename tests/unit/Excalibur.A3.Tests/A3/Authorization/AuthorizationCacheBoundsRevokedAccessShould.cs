// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.A3.Authentication;
using Excalibur.A3.Authorization;
using Excalibur.A3.Authorization.PolicyData;
using Excalibur.Domain;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Internal;
using Microsoft.Extensions.Options;

using MsOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.Tests.A3.Authorization;

/// <summary>
/// Locks that a cached authorization decision has an ABSOLUTE lifetime, so a revoked grant cannot keep
/// authorizing merely because it keeps being read.
/// </summary>
/// <remarks>
/// <para>
/// The sync invalidates the cache before and after it changes the store, but a reader can load the OLD state
/// before the change commits and write it to the cache after the second invalidation. With a sliding
/// expiration only, that entry is refreshed by every read, so an entry read more often than its window never
/// expires and the revoked access is served indefinitely. An absolute expiration caps it: whatever happens,
/// the entry is reloaded from the store once the bound has passed.
/// </para>
/// <para>
/// These arms drive the REAL in-memory distributed cache through a manual clock, so the expiry decision under
/// test is the cache's own, not a substitute's. Each read uses a fresh provider because the provider
/// memoizes its policy for the lifetime of one scope.
/// </para>
/// </remarks>
[Collection("ApplicationContext")]
[Trait("Category", "Unit")]
[Trait("Component", "A3")]
[Trait("Feature", "Authorization")]
public sealed class AuthorizationCacheBoundsRevokedAccessShould : IDisposable
{
	private const string UserId = "user-1";
	private const string TenantId = "tenant-1";

	private static readonly TimeSpan Bound = TimeSpan.FromMinutes(5);
	private static readonly TimeSpan ReadInterval = TimeSpan.FromMinutes(4);

	private readonly ManualClock _clock = new();
	private readonly IGrantStore _grantStore = A.Fake<IGrantStore>();
	private readonly IGrantQueryStore _grantQueries = A.Fake<IGrantQueryStore>();
	private readonly IActivityGroupStore _groupStore = A.Fake<IActivityGroupStore>();
	private readonly IDistributedCache _cache;

	public AuthorizationCacheBoundsRevokedAccessShould()
	{
		ApplicationContext.Init(new Dictionary<string, string?> { ["ApplicationName"] = "TestApp" });

		_cache = new MemoryDistributedCache(MsOptions.Create(new MemoryDistributedCacheOptions { Clock = _clock }));

		A.CallTo(() => _grantStore.GetService(typeof(IGrantQueryStore))).Returns(_grantQueries);
		A.CallTo(() => _grantQueries.FindUserGrantsAsync(UserId, A<CancellationToken>._))
			.Returns(new Dictionary<string, object>());
		A.CallTo(() => _groupStore.FindActivityGroupsAsync(TenantId, A<CancellationToken>._))
			.Returns(new Dictionary<string, IReadOnlyCollection<string>>());
	}

	public void Dispose() => ApplicationContext.Reset();

	/// <summary>
	/// SAFETY: cached grants are reloaded once the bound has passed, even though they were read throughout.
	/// </summary>
	[Fact]
	public async Task ReloadCachedGrantsOnceTheAbsoluteBoundHasPassed_EvenWhenReadContinuously()
	{
		await ReadThroughTheBoundAsync().ConfigureAwait(false);

		A.CallTo(() => _grantQueries.FindUserGrantsAsync(UserId, A<CancellationToken>._))
			.MustHaveHappenedTwiceExactly();
	}

	/// <summary>
	/// SAFETY: the tenant's cached activity-group catalogue is bounded the same way.
	/// </summary>
	/// <remarks>
	/// This entry previously had a one-hour sliding window, so under continuous reads it outlived any grant.
	/// </remarks>
	[Fact]
	public async Task ReloadTheCachedActivityGroupCatalogueOnceTheAbsoluteBoundHasPassed()
	{
		await ReadThroughTheBoundAsync().ConfigureAwait(false);

		A.CallTo(() => _groupStore.FindActivityGroupsAsync(TenantId, A<CancellationToken>._))
			.MustHaveHappenedTwiceExactly();
	}

	/// <summary>
	/// LIVENESS: within the bound the cache is still used.
	/// </summary>
	/// <remarks>
	/// Without this, a provider that never cached anything would satisfy both arms above.
	/// </remarks>
	[Fact]
	public async Task StillServeFromTheCacheWithinTheBound()
	{
		await ReadPolicyAsync().ConfigureAwait(false);
		_clock.Advance(TimeSpan.FromMinutes(1));
		await ReadPolicyAsync().ConfigureAwait(false);

		A.CallTo(() => _grantQueries.FindUserGrantsAsync(UserId, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => _groupStore.FindActivityGroupsAsync(TenantId, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	/// <summary>
	/// A bound that is zero or negative is refused at startup rather than silently disabling the cache.
	/// </summary>
	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	public void RefuseANonPositiveBound(int seconds)
	{
		var result = new AuthorizationCacheOptionsValidator().Validate(
			null,
			new AuthorizationCacheOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(seconds) });

		result.Failed.ShouldBeTrue();
	}

	/// <summary>
	/// The default bound is accepted, and is no longer than five minutes.
	/// </summary>
	[Fact]
	public void AcceptTheDefaultBound()
	{
		var options = new AuthorizationCacheOptions();

		new AuthorizationCacheOptionsValidator().Validate(null, options).Succeeded.ShouldBeTrue();
		options.AbsoluteExpirationRelativeToNow.ShouldBeLessThanOrEqualTo(TimeSpan.FromMinutes(5));
	}

	/// <summary>
	/// A zero bound registered through the real A3 registration is refused when the host starts.
	/// </summary>
	/// <remarks>
	/// The arms above call the validator directly, so they would still pass if the registration never wired
	/// it. This one goes through <c>AddExcaliburA3()</c> and the startup validator that <c>ValidateOnStart</c>
	/// feeds, which is the path a consumer's host takes.
	/// </remarks>
	[Fact]
	public void RefuseToStartWithANonPositiveBound()
	{
		var failures = StartupFailures(o => o.AbsoluteExpirationRelativeToNow = TimeSpan.Zero);

		failures.ShouldContain(
			f => f.Contains(nameof(AuthorizationCacheOptions.AbsoluteExpirationRelativeToNow), StringComparison.Ordinal),
			$"startup reported: {string.Join(" | ", failures)}");
	}

	/// <summary>
	/// LIVENESS: the default bound does not stop the host from starting.
	/// </summary>
	/// <remarks>
	/// Without this, a registration that refused every value would satisfy the arm above.
	/// </remarks>
	[Fact]
	public void StartWithTheDefaultBound()
	{
		var failures = StartupFailures(static _ => { });

		failures.ShouldNotContain(
			f => f.Contains(nameof(AuthorizationCacheOptions.AbsoluteExpirationRelativeToNow), StringComparison.Ordinal));
	}

	// Every startup validation failure message, from the real A3 registration. Other options may also be
	// refused under a bare registration, so the arms look for THIS option's failure rather than for any.
	private static List<string> StartupFailures(Action<AuthorizationCacheOptions> configure)
	{
		var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddExcaliburA3();
		_ = services.Configure(configure);

		using var provider = services.BuildServiceProvider();

		try
		{
			provider.GetRequiredService<IStartupValidator>().Validate();
			return [];
		}
		catch (OptionsValidationException ex)
		{
			return [.. ex.Failures];
		}
		catch (AggregateException ex)
		{
			return [.. ex.Flatten().InnerExceptions.OfType<OptionsValidationException>().SelectMany(static e => e.Failures)];
		}
	}

	// Read at t=0, t=4 min and t=8 min. A sliding-only entry is refreshed by the t=4 read and is still alive at
	// t=8; an entry bounded at 5 minutes from when it was written has expired by t=8 and is reloaded.
	private async Task ReadThroughTheBoundAsync()
	{
		await ReadPolicyAsync().ConfigureAwait(false);
		_clock.Advance(ReadInterval);
		await ReadPolicyAsync().ConfigureAwait(false);
		_clock.Advance(ReadInterval);
		await ReadPolicyAsync().ConfigureAwait(false);
	}

	private async Task ReadPolicyAsync()
	{
		var currentUser = A.Fake<IAuthenticationToken>();
		A.CallTo(() => currentUser.UserId).Returns(UserId);
		var tenant = A.Fake<ITenantContext>();
		A.CallTo(() => tenant.TenantId).Returns(TenantId);

		var provider = new AuthorizationPolicyProvider(
			activityGroups: new ActivityGroups(_groupStore),
			userGrants: new UserGrants(_grantStore),
			currentUser: currentUser,
			cache: _cache,
			tenantContext: tenant,
			cacheOptions: MsOptions.Create(new AuthorizationCacheOptions { AbsoluteExpirationRelativeToNow = Bound }));

		_ = await provider.GetPolicyAsync().ConfigureAwait(false);
	}

	private sealed class ManualClock : ISystemClock
	{
		public DateTimeOffset UtcNow { get; private set; } = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

		public void Advance(TimeSpan by) => UtcNow += by;
	}
}
