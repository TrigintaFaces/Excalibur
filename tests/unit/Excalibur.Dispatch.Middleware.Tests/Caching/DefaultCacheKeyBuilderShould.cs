// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Caching;

namespace Excalibur.Dispatch.Middleware.Tests.Caching;

/// <summary>
/// Unit tests for <see cref="DefaultCacheKeyBuilder"/>.
/// Covers all key building paths: ICacheable-based keys, serialization fallback, tenant/user context,
/// and hashing behavior.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Caching")]
public sealed class DefaultCacheKeyBuilderShould : UnitTestBase
{
	private readonly IJsonSerializer _serializer;
	private readonly IMessageContext _context;
	private readonly DefaultCacheKeyBuilder _sut;

	public DefaultCacheKeyBuilderShould()
	{
		_serializer = A.Fake<IJsonSerializer>();
		_context = A.Fake<IMessageContext>();
		_sut = new DefaultCacheKeyBuilder(_serializer);

		// Default context values
		A.CallTo(() => _context.TenantId).Returns(null);
		A.CallTo(() => _context.UserId).Returns(null);
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
		// Arrange
		var action = new NonCacheableTestAction();
		A.CallTo(() => _serializer.Serialize(A<object>._, A<Type>._)).Returns("{\"id\":1}");

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
		// Serializer should NOT have been called because ICacheable provides the key
		A.CallTo(() => _serializer.Serialize(A<object>._, A<Type>._)).MustNotHaveHappened();
	}

	[Fact]
	public void CreateKey_WhenNotICacheable_FallsBackToSerialization()
	{
		// Arrange
		var action = new NonCacheableTestAction();
		A.CallTo(() => _serializer.Serialize(A<object>._, A<Type>._)).Returns("{\"value\":42}");

		// Act
		var key = _sut.CreateKey(action, _context);

		// Assert
		key.ShouldNotBeNullOrEmpty();
		A.CallTo(() => _serializer.Serialize(action, action.GetType())).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void CreateKey_IncludesTenantInKey()
	{
		// Arrange
		var action = new NonCacheableTestAction();
		A.CallTo(() => _serializer.Serialize(A<object>._, A<Type>._)).Returns("{}");
		A.CallTo(() => _context.TenantId).Returns("tenant-a");

		// Act
		var keyA = _sut.CreateKey(action, _context);

		A.CallTo(() => _context.TenantId).Returns("tenant-b");
		var keyB = _sut.CreateKey(action, _context);

		// Assert -- different tenants produce different keys
		keyA.ShouldNotBe(keyB);
	}

	[Fact]
	public void CreateKey_IncludesUserInKey()
	{
		// Arrange
		var action = new NonCacheableTestAction();
		A.CallTo(() => _serializer.Serialize(A<object>._, A<Type>._)).Returns("{}");
		A.CallTo(() => _context.UserId).Returns("user-1");

		// Act
		var key1 = _sut.CreateKey(action, _context);

		A.CallTo(() => _context.UserId).Returns("user-2");
		var key2 = _sut.CreateKey(action, _context);

		// Assert -- different users produce different keys
		key1.ShouldNotBe(key2);
	}

	[Fact]
	public void CreateKey_WhenTenantIsNull_UsesGlobalDefault()
	{
		// Arrange
		var action = new NonCacheableTestAction();
		A.CallTo(() => _serializer.Serialize(A<object>._, A<Type>._)).Returns("{}");
		A.CallTo(() => _context.TenantId).Returns((string?)null);

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
		A.CallTo(() => _serializer.Serialize(A<object>._, A<Type>._)).Returns("{}");
		A.CallTo(() => _context.UserId).Returns((string?)null);

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
		A.CallTo(() => _serializer.Serialize(A<object>._, A<Type>._)).Returns("test-data");

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
		A.CallTo(() => _context.TenantId).Returns("tenantX");
		A.CallTo(() => _context.UserId).Returns("userY");

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
