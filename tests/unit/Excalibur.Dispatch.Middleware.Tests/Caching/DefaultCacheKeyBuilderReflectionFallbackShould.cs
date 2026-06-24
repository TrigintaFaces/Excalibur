// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Features;
using Excalibur.Dispatch.Caching;

namespace Excalibur.Dispatch.Middleware.Tests.Caching;

/// <summary>
/// Tests for DefaultCacheKeyBuilder reflection-failure handling.
/// <para>
/// Updated Sprint 843 (bd-5n1v5n, Option X): when ICacheable&lt;T&gt; reflection fails the builder now
/// returns <see langword="null"/> (skip caching) instead of fabricating an identity-hash fallback key —
/// a fabricated key risked a false cross-request cache hit. (Originally Sprint 567 S567.6, which produced
/// a type-name + hash-code fallback; that contract was replaced.)
/// </para>
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Caching)]
[Trait("Priority", "2")]
public sealed class DefaultCacheKeyBuilderReflectionFallbackShould : UnitTestBase
{
	private readonly DispatchJsonSerializer _serializer;
	private readonly IMessageContext _context;
	private readonly DefaultCacheKeyBuilder _sut;

	public DefaultCacheKeyBuilderReflectionFallbackShould()
	{
		_serializer = new DispatchJsonSerializer();
		_context = A.Fake<IMessageContext>();
		_sut = new DefaultCacheKeyBuilder(_serializer);

		var features = new Dictionary<Type, object>();
		var items = new Dictionary<string, object>(StringComparer.Ordinal);
		A.CallTo(() => _context.Features).Returns(features);
		A.CallTo(() => _context.Items).Returns(items);

		var identity = new MessageIdentityFeature { TenantId = "test-tenant", UserId = "test-user" };
		features[typeof(IMessageIdentityFeature)] = identity;
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			_serializer.Dispose();
		}

		base.Dispose(disposing);
	}

	[Fact]
	public void CreateKey_WhenICacheableThrows_ReturnsNullToSkipCaching()
	{
		// Arrange - action that implements ICacheable but whose GetCacheKey throws
		var action = new ThrowingCacheableAction();

		// Act - should NOT throw; bd-5n1v5n Option X: reflection failure → null (skip caching)
		var key = _sut.CreateKey(action, _context);

		// Assert - no fabricated key (no identity hash) → caching is skipped
		key.ShouldBeNull();
	}

	[Fact]
	public void CreateKey_WhenICacheableThrows_ReturnsNullConsistently()
	{
		// Arrange
		var action = new ThrowingCacheableAction();

		// Act
		var key1 = _sut.CreateKey(action, _context);
		var key2 = _sut.CreateKey(action, _context);

		// Assert - reflection failure always yields null (skip), never a fabricated key
		key1.ShouldBeNull();
		key2.ShouldBeNull();
	}

	[Fact]
	public void CreateKey_WhenNotICacheable_UsesSerializationNormally()
	{
		// Arrange — real serializer will serialize the action to JSON
		var action = new NonCacheableAction();

		// Act
		var key = _sut.CreateKey(action, _context);

		// Assert
		key.ShouldNotBeNullOrEmpty();
	}

	[Fact]
	public void CreateKey_WhenICacheableWorks_UsesInterfaceKey()
	{
		// Arrange
		var action = new WorkingCacheableAction("my-key-123");

		// Act
		var key = _sut.CreateKey(action, _context);

		// Assert
		key.ShouldNotBeNullOrEmpty();
	}

	[Fact]
	public void CreateKey_ThrowingReturnsNull_WorkingReturnsKey()
	{
		// Arrange
		var throwingAction = new ThrowingCacheableAction();
		var workingAction = new WorkingCacheableAction("stable-key");

		// Act
		var throwingKey = _sut.CreateKey(throwingAction, _context);
		var workingKey = _sut.CreateKey(workingAction, _context);

		// Assert - a reflection-failing action skips (null); a resolvable one yields a real key
		throwingKey.ShouldBeNull();
		workingKey.ShouldNotBeNullOrEmpty();
	}

	[Fact]
	public void CreateKey_ResolvedKey_IsUrlSafeBase64()
	{
		// Arrange - a resolvable key is hashed; verify the hash is URL-safe (reflection-fail now skips,
		// so the URL-safe property is asserted on a real key path).
		var action = new WorkingCacheableAction("url-safe-check");

		// Act
		var key = _sut.CreateKey(action, _context);

		// Assert
		key.ShouldNotBeNull();
		key.ShouldNotContain("=");
		key.ShouldNotContain("/");
		key.ShouldNotContain("+");
	}

	[Fact]
	public void CreateKey_WhenICacheableReturnsNull_FallsBackToSerialization()
	{
		// Arrange - action whose GetCacheKey returns null — real serializer handles fallback
		var action = new NullReturningCacheableAction();

		// Act - should use serialization fallback since ICacheable returned null
		var key = _sut.CreateKey(action, _context);

		// Assert
		key.ShouldNotBeNullOrEmpty();
	}

	// -- Test helper types --

	/// <summary>
	/// ICacheable implementation whose GetCacheKey() throws to simulate reflection failure.
	/// </summary>
	private sealed class ThrowingCacheableAction : ICacheable<string>
	{
		public string GetCacheKey() => throw new InvalidOperationException("Simulated reflection failure");
	}

	private sealed class NonCacheableAction : IDispatchAction
	{
	}

	private sealed class WorkingCacheableAction(string cacheKey) : ICacheable<string>
	{
		public string GetCacheKey() => cacheKey;
	}

	/// <summary>
	/// ICacheable implementation whose GetCacheKey returns null (edge case).
	/// The reflection path will get null from Invoke, fail the "is string" check,
	/// and return false from TryGetCacheKeyFromInterface.
	/// </summary>
	private sealed class NullReturningCacheableAction : ICacheable<string>
	{
		public string GetCacheKey() => null!;
	}
}
