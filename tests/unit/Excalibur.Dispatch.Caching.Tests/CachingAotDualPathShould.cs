// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Caching;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Caching.Tests;

/// <summary>
/// End-to-end dual-path AOT verification tests for Caching middleware infrastructure.
/// Validates that <c>AddCachePolicy&lt;TMessage, TPolicy&gt;()</c> populates the
/// <see cref="CachePolicyRegistry"/> so the AOT code path does not need
/// <see cref="Type.MakeGenericType"/> at runtime.
/// Sprint 756 task q0atue (R-B6).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Feature", "AOT")]
public sealed class CachingAotDualPathShould : IDisposable
{
	public CachingAotDualPathShould()
	{
		// Clear static state before each test to prevent cross-test contamination
		CachingServiceCollectionExtensions.CachePolicyPendingRegistrations.Clear();
	}

	public void Dispose()
	{
		CachingServiceCollectionExtensions.CachePolicyPendingRegistrations.Clear();
	}

	// -- CachePolicyRegistry Population Tests --

	[Fact]
	public void PopulateRegistryViaAddCachePolicy()
	{
		// Arrange — full DI registration
		var services = new ServiceCollection();
		services.AddDispatchCaching();
		services.AddCachePolicy<TestCacheableQuery, TestCachePolicy>();
		services.AddLogging();
		var sp = services.BuildServiceProvider();

		// Force options resolution to trigger CachePolicyRegistryPopulator
		_ = sp.GetRequiredService<IOptions<CacheOptions>>().Value;

		// Act
		var registry = sp.GetRequiredService<CachePolicyRegistry>();
		var policyDelegate = registry.GetPolicy(typeof(TestCacheableQuery));

		// Assert — registry is populated, NOT null
		policyDelegate.ShouldNotBeNull(
			"CachePolicyRegistry should be populated by AddCachePolicy<TMessage, TPolicy>(). " +
			"A null delegate means the AOT path would fall back to global policy.");
	}

	[Fact]
	public void ResolvePolicyDelegateAndReturnCorrectShouldCacheResult()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddDispatchCaching();
		services.AddCachePolicy<TestCacheableQuery, TestCachePolicy>();
		services.AddLogging();
		var sp = services.BuildServiceProvider();

		_ = sp.GetRequiredService<IOptions<CacheOptions>>().Value;

		var registry = sp.GetRequiredService<CachePolicyRegistry>();
		var policyDelegate = registry.GetPolicy(typeof(TestCacheableQuery))!;

		// Act — invoke the delegate with a message that should be cached
		var message = new TestCacheableQuery { QueryId = "q-1" };
		var shouldCache = policyDelegate(sp, message, "some-result");

		// Assert — TestCachePolicy.ShouldCache returns true when result is not null
		shouldCache.ShouldBeTrue();
	}

	[Fact]
	public void PolicyDelegateRespectsNullResultBehavior()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddDispatchCaching();
		services.AddCachePolicy<TestCacheableQuery, TestCachePolicy>();
		services.AddLogging();
		var sp = services.BuildServiceProvider();

		_ = sp.GetRequiredService<IOptions<CacheOptions>>().Value;

		var registry = sp.GetRequiredService<CachePolicyRegistry>();
		var policyDelegate = registry.GetPolicy(typeof(TestCacheableQuery))!;

		// Act — invoke with null result (TestCachePolicy returns false for null)
		var message = new TestCacheableQuery { QueryId = "q-1" };
		var shouldCache = policyDelegate(sp, message, null);

		// Assert
		shouldCache.ShouldBeFalse("TestCachePolicy should return false when result is null.");
	}

	[Fact]
	public void ReturnNullPolicyForUnregisteredMessageType()
	{
		// Arrange — register one policy but query for a different message type
		var services = new ServiceCollection();
		services.AddDispatchCaching();
		services.AddCachePolicy<TestCacheableQuery, TestCachePolicy>();
		services.AddLogging();
		var sp = services.BuildServiceProvider();

		_ = sp.GetRequiredService<IOptions<CacheOptions>>().Value;

		// Act
		var registry = sp.GetRequiredService<CachePolicyRegistry>();
		var policyDelegate = registry.GetPolicy(typeof(TestOtherQuery));

		// Assert — unregistered types return null (fall back to global)
		policyDelegate.ShouldBeNull();
	}

	[Fact]
	public void FreezeRegistryAfterPopulation()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddDispatchCaching();
		services.AddCachePolicy<TestCacheableQuery, TestCachePolicy>();
		services.AddLogging();
		var sp = services.BuildServiceProvider();

		// Force populator to run — this should freeze the registry
		_ = sp.GetRequiredService<IOptions<CacheOptions>>().Value;

		// Act & Assert — registering after freeze should throw
		var registry = sp.GetRequiredService<CachePolicyRegistry>();
		Should.Throw<InvalidOperationException>(() =>
			registry.Register(typeof(TestOtherQuery), (_, _, _) => true));
	}

	[Fact]
	public void PopulateRegistryForMultiplePolicies()
	{
		// Arrange — register two different message type policies
		var services = new ServiceCollection();
		services.AddDispatchCaching();
		services.AddCachePolicy<TestCacheableQuery, TestCachePolicy>();
		services.AddCachePolicy<TestOtherQuery, TestOtherCachePolicy>();
		services.AddLogging();
		var sp = services.BuildServiceProvider();

		_ = sp.GetRequiredService<IOptions<CacheOptions>>().Value;

		// Act
		var registry = sp.GetRequiredService<CachePolicyRegistry>();

		// Assert — both policies registered
		registry.GetPolicy(typeof(TestCacheableQuery)).ShouldNotBeNull();
		registry.GetPolicy(typeof(TestOtherQuery)).ShouldNotBeNull();
	}

	[Fact]
	public void PopulatorRunsOnlyOnce()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddDispatchCaching();
		services.AddCachePolicy<TestCacheableQuery, TestCachePolicy>();
		services.AddLogging();
		var sp = services.BuildServiceProvider();

		// Act — resolve options twice (populator should be idempotent)
		_ = sp.GetRequiredService<IOptions<CacheOptions>>().Value;
		_ = sp.GetRequiredService<IOptions<CacheOptions>>().Value;

		// Assert — registry still has the policy (didn't error on double-populate)
		var registry = sp.GetRequiredService<CachePolicyRegistry>();
		registry.GetPolicy(typeof(TestCacheableQuery)).ShouldNotBeNull();
	}

	// -- CachePolicyRegistry Direct Tests --

	[Fact]
	public void RegistryRegisterAndGetPolicy()
	{
		// Arrange
		var registry = new CachePolicyRegistry();
		Func<IServiceProvider, IDispatchMessage, object?, bool> delegate1 = (_, _, _) => true;

		// Act
		registry.Register(typeof(TestCacheableQuery), delegate1);

		// Assert
		registry.GetPolicy(typeof(TestCacheableQuery)).ShouldBeSameAs(delegate1);
	}

	[Fact]
	public void RegistryReturnsNullForUnknownType()
	{
		// Arrange
		var registry = new CachePolicyRegistry();

		// Act & Assert
		registry.GetPolicy(typeof(TestCacheableQuery)).ShouldBeNull();
	}

	[Fact]
	public void RegistryFreezeBlocksRegistration()
	{
		// Arrange
		var registry = new CachePolicyRegistry();
		registry.Freeze();

		// Act & Assert
		Should.Throw<InvalidOperationException>(() =>
			registry.Register(typeof(TestCacheableQuery), (_, _, _) => true));
	}

	[Fact]
	public void RegistryOverwritesSameTypePolicy()
	{
		// Arrange
		var registry = new CachePolicyRegistry();
		Func<IServiceProvider, IDispatchMessage, object?, bool> first = (_, _, _) => true;
		Func<IServiceProvider, IDispatchMessage, object?, bool> second = (_, _, _) => false;

		// Act — register same type twice (last wins)
		registry.Register(typeof(TestCacheableQuery), first);
		registry.Register(typeof(TestCacheableQuery), second);

		// Assert
		registry.GetPolicy(typeof(TestCacheableQuery)).ShouldBeSameAs(second);
	}

	// -- AddCachePolicy DI Registration Tests --

	[Fact]
	public void AddCachePolicyRegistersClosedGenericInDi()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddDispatchCaching();
		services.AddCachePolicy<TestCacheableQuery, TestCachePolicy>();
		services.AddLogging();
		var sp = services.BuildServiceProvider();

		// Act — JIT path: resolve the closed-generic policy directly from DI
		var policy = sp.GetService<IResultCachePolicy<TestCacheableQuery>>();

		// Assert
		policy.ShouldNotBeNull();
		policy.ShouldBeOfType<TestCachePolicy>();
	}

	[Fact]
	public void AddCachePolicyAccumulatesPendingRegistrations()
	{
		// Arrange — clear to start fresh
		CachingServiceCollectionExtensions.CachePolicyPendingRegistrations.Clear();

		// Act
		var services = new ServiceCollection();
		services.AddDispatchCaching();
		services.AddCachePolicy<TestCacheableQuery, TestCachePolicy>();
		services.AddCachePolicy<TestOtherQuery, TestOtherCachePolicy>();

		// Assert — two pending registrations accumulated
		CachingServiceCollectionExtensions.CachePolicyPendingRegistrations.Count.ShouldBe(2);
	}

	// -- ExtractReturnValue Pattern Match Test --

	[Fact]
	public void ExtractReturnValueFromIMessageResultOfObject()
	{
		// Arrange — CachedMessageResult<T> implements IMessageResult<T> which is covariant (out T)
		// so IMessageResult<string> is assignable to IMessageResult<object>
		var result = new CachedMessageResult<string>("cached-value");

		// Act — the same pattern match used by CachingMiddleware.ExtractReturnValue
		var extracted = result is IMessageResult<object> typed ? typed.ReturnValue : null;

		// Assert
		extracted.ShouldBe("cached-value");
	}

	[Fact]
	public void ExtractReturnValueReturnsNullForNonGenericResult()
	{
		// Arrange — a non-generic IMessageResult that doesn't implement IMessageResult<T>
		var result = A.Fake<IMessageResult>();

		// Act — pattern match should fail, returning null
		var extracted = result is IMessageResult<object> typed ? typed.ReturnValue : null;

		// Assert
		extracted.ShouldBeNull();
	}

	[Fact]
	public void ExtractReturnValueFromCachedObjectMessageResult()
	{
		// Arrange — AOT path uses CachedObjectMessageResult (non-generic)
		var result = new CachedObjectMessageResult("aot-cached-value");

		// Act — CachedObjectMessageResult does NOT implement IMessageResult<object>.
		// The AOT path stores the value in context.Result via ReturnValue property directly.
		// Verify the result type exposes the value correctly for the AOT path.
		IMessageResult boxed = result;
		var hasReturnValue = boxed is IMessageResult<object>;

		// Assert — AOT result type uses ReturnValue property directly, not IMessageResult<object>
		hasReturnValue.ShouldBeFalse("CachedObjectMessageResult should NOT implement IMessageResult<object>");
		result.ReturnValue.ShouldBe("aot-cached-value");
		result.CacheHit.ShouldBeTrue();
		result.Succeeded.ShouldBeTrue();
	}

	// -- CreateCachedMessageResult Dual Path Test --

	[Fact]
	public void CachedMessageResultWrapsTypedValue()
	{
		// Arrange — JIT path creates CachedMessageResult<T>
		var result = new CachedMessageResult<int>(42);

		// Assert
		result.ReturnValue.ShouldBe(42);
		result.CacheHit.ShouldBeTrue();
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void CachedObjectMessageResultWrapsObjectValue()
	{
		// Arrange — AOT path creates CachedObjectMessageResult
		var result = new CachedObjectMessageResult(42);

		// Assert
		result.ReturnValue.ShouldBe(42);
		result.CacheHit.ShouldBeTrue();
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void BothResultTypesHandleNullValue()
	{
		// Arrange
		var typedResult = new CachedMessageResult<string>(null);
		var objectResult = new CachedObjectMessageResult(null);

		// Assert — both handle null gracefully
		typedResult.ReturnValue.ShouldBeNull();
		typedResult.Succeeded.ShouldBeTrue();
		objectResult.ReturnValue.ShouldBeNull();
		objectResult.Succeeded.ShouldBeTrue();
	}

	// -- Test Fixtures --

	/// <summary>
	/// Test message type for cache policy testing.
	/// </summary>
	internal sealed class TestCacheableQuery : IDispatchMessage
	{
		public string QueryId { get; init; } = "test-query";
	}

	/// <summary>
	/// Second test message type for multi-policy registration tests.
	/// </summary>
	internal sealed class TestOtherQuery : IDispatchMessage
	{
		public string QueryId { get; init; } = "other-query";
	}

	/// <summary>
	/// Test cache policy that caches when result is non-null.
	/// </summary>
	internal sealed class TestCachePolicy : IResultCachePolicy<TestCacheableQuery>
	{
		public bool ShouldCache(TestCacheableQuery message, object? result)
			=> result is not null;
	}

	/// <summary>
	/// Test cache policy for the second message type. Always caches.
	/// </summary>
	internal sealed class TestOtherCachePolicy : IResultCachePolicy<TestOtherQuery>
	{
		public bool ShouldCache(TestOtherQuery message, object? result)
			=> true;
	}
}
