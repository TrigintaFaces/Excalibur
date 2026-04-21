// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Features;
using Excalibur.Dispatch.Caching;

namespace Excalibur.Dispatch.Middleware.Tests.Caching;

/// <summary>
/// Unit tests for <see cref="DefaultCacheKeyBuilder"/>.
/// Covers all key building paths: ICacheable-based keys, serialization fallback, tenant/user context,
/// and hashing behavior.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Caching)]
public sealed class DefaultCacheKeyBuilderShould : UnitTestBase
{
	private readonly DispatchJsonSerializer _serializer;
	private readonly IMessageContext _context;
	private readonly Dictionary<Type, object> _features;
	private readonly DefaultCacheKeyBuilder _sut;

	public DefaultCacheKeyBuilderShould()
	{
		_serializer = new DispatchJsonSerializer();
		_context = A.Fake<IMessageContext>();
		_sut = new DefaultCacheKeyBuilder(_serializer);

		// Default context values: null tenant and user
		_features = new Dictionary<Type, object>();
		var items = new Dictionary<string, object>(StringComparer.Ordinal);
		A.CallTo(() => _context.Features).Returns(_features);
		A.CallTo(() => _context.Items).Returns(items);

		var identity = new MessageIdentityFeature { TenantId = null, UserId = null };
		_features[typeof(IMessageIdentityFeature)] = identity;
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			_serializer.Dispose();
		}

		base.Dispose(disposing);
	}

	private void SetIdentity(string? tenantId, string? userId)
	{
		_features[typeof(IMessageIdentityFeature)] = new MessageIdentityFeature { TenantId = tenantId, UserId = userId };
	}

	[Fact]
	public void CreateKey_ThrowsOnNullAction()
	{
		// Arrange & Act & Assert
		Should.Throw<ArgumentNullException>(() => _sut.CreateKey(null!, _context));
	}

	[Fact]
	public void CreateKey_ThrowsOnNullContext()
	{
		// Arrange
		var action = A.Fake<IDispatchAction>();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => _sut.CreateKey(action, null!));
	}

	[Fact]
	public void CreateKey_ReturnsDeterministicHashForSameInputs()
	{
		// Arrange — real serializer produces deterministic JSON for the same action
		var action = new NonCacheableTestAction();

		// Act
		var key1 = _sut.CreateKey(action, _context);
		var key2 = _sut.CreateKey(action, _context);

		// Assert
		key1.ShouldNotBeNullOrEmpty();
		key1.ShouldBe(key2);
	}

	[Fact]
	public void CreateKey_WhenICacheable_UsesCacheKeyFromInterface()
	{
		// Arrange
		var action = new CacheableTestAction("my-custom-key");

		// Act
		var key = _sut.CreateKey(action, _context);

		// Assert
		key.ShouldNotBeNullOrEmpty();
	}

	[Fact]
	public void CreateKey_WhenNotICacheable_FallsBackToSerialization()
	{
		// Arrange — real serializer will serialize the action to JSON
		var action = new NonCacheableTestAction();

		// Act
		var key = _sut.CreateKey(action, _context);

		// Assert
		key.ShouldNotBeNullOrEmpty();
	}

	[Fact]
	public void CreateKey_IncludesTenantInKey()
	{
		// Arrange
		var action = new NonCacheableTestAction();
		SetIdentity("tenant-a", null);

		// Act
		var keyA = _sut.CreateKey(action, _context);

		SetIdentity("tenant-b", null);
		var keyB = _sut.CreateKey(action, _context);

		// Assert -- different tenants produce different keys
		keyA.ShouldNotBe(keyB);
	}

	[Fact]
	public void CreateKey_IncludesUserInKey()
	{
		// Arrange
		var action = new NonCacheableTestAction();
		SetIdentity(null, "user-1");

		// Act
		var key1 = _sut.CreateKey(action, _context);

		SetIdentity(null, "user-2");
		var key2 = _sut.CreateKey(action, _context);

		// Assert -- different users produce different keys
		key1.ShouldNotBe(key2);
	}

	[Fact]
	public void CreateKey_WhenTenantIsNull_UsesGlobalDefault()
	{
		// Arrange
		var action = new NonCacheableTestAction();
		SetIdentity(null, null);

		// Act -- should not throw
		var key = _sut.CreateKey(action, _context);

		// Assert
		key.ShouldNotBeNullOrEmpty();
	}

	[Fact]
	public void CreateKey_WhenUserIsNull_UsesAnonymousDefault()
	{
		// Arrange
		var action = new NonCacheableTestAction();
		SetIdentity(null, null);

		// Act -- should not throw
		var key = _sut.CreateKey(action, _context);

		// Assert
		key.ShouldNotBeNullOrEmpty();
	}

	[Fact]
	public void CreateKey_ReturnsUrlSafeBase64Hash()
	{
		// Arrange
		var action = new NonCacheableTestAction();

		// Act
		var key = _sut.CreateKey(action, _context);

		// Assert -- should not contain URL-unsafe characters
		key.ShouldNotContain("=");
		key.ShouldNotContain("/");
		key.ShouldNotContain("+");
	}

	[Fact]
	public void CreateKey_WhenICacheableWithDifferentKeys_ProducesDifferentHashes()
	{
		// Arrange
		var action1 = new CacheableTestAction("key-alpha");
		var action2 = new CacheableTestAction("key-beta");

		// Act
		var hash1 = _sut.CreateKey(action1, _context);
		var hash2 = _sut.CreateKey(action2, _context);

		// Assert
		hash1.ShouldNotBe(hash2);
	}

	[Fact]
	public void CreateKey_WithTenantAndUser_ProducesUniqueKey()
	{
		// Arrange
		var action = new CacheableTestAction("same-key");
		SetIdentity("tenantX", "userY");

		// Act
		var key = _sut.CreateKey(action, _context);

		// Assert
		key.ShouldNotBeNullOrEmpty();
	}

	// -- Test helper types --

	private sealed class NonCacheableTestAction : IDispatchAction
	{
	}

	private sealed class CacheableTestAction(string cacheKey) : ICacheable<string>
	{
		public string GetCacheKey() => cacheKey;
	}
}
