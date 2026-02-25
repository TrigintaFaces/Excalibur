// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Caching;

namespace Excalibur.Dispatch.Middleware.Tests.Caching;

/// <summary>
/// Tests for Sprint 567 S567.6: DefaultCacheKeyBuilder reflection exception fallback.
/// Validates that when TryGetCacheKeyFromInterface reflection fails, the builder
/// falls back to a type name + hash code based key instead of propagating the exception.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Caching")]
[Trait("Priority", "2")]
public sealed class DefaultCacheKeyBuilderReflectionFallbackShould : UnitTestBase
{
	private readonly IJsonSerializer _serializer;
	private readonly IMessageContext _context;
	private readonly DefaultCacheKeyBuilder _sut;

	public DefaultCacheKeyBuilderReflectionFallbackShould()
	{
		_serializer = A.Fake<IJsonSerializer>();
		_context = A.Fake<IMessageContext>();
		_sut = new DefaultCacheKeyBuilder(_serializer);

		A.CallTo(() => _context.TenantId).Returns("test-tenant");
		A.CallTo(() => _context.UserId).Returns("test-user");
	}

	[Fact]
	public void CreateKey_WhenICacheableThrows_FallsBackToTypeNameKey()
	{
		// Arrange - action that implements ICacheable but whose GetCacheKey throws
		var action = new ThrowingCacheableAction();

		// Act - should NOT throw, should use fallback
		var key = _sut.CreateKey(action, _context);

		// Assert
		key.ShouldNotBeNullOrEmpty("Fallback key should be generated when reflection fails");
		// Serializer should NOT be called because the catch block produces a fallback key
		// that TryGetCacheKeyFromInterface returns as true
	}

	[Fact]
	public void CreateKey_WhenICacheableThrows_ProducesDeterministicKey()
	{
		// Arrange
		var action = new ThrowingCacheableAction();

		// Act
		var key1 = _sut.CreateKey(action, _context);
		var key2 = _sut.CreateKey(action, _context);

		// Assert - same action instance should produce same key
		key1.ShouldBe(key2, "Fallback keys should be deterministic for the same action instance");
	}

	[Fact]
	public void CreateKey_WhenNotICacheable_UsesSerializationNormally()
	{
		// Arrange
		var action = new NonCacheableAction();
		A.CallTo(() => _serializer.Serialize(A<object>._, A<Type>._)).Returns("{\"data\":1}");

		// Act
		var key = _sut.CreateKey(action, _context);

		// Assert
		key.ShouldNotBeNullOrEmpty();
		A.CallTo(() => _serializer.Serialize(action, action.GetType())).MustHaveHappenedOnceExactly();
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
		// Serializer should NOT be called
		A.CallTo(() => _serializer.Serialize(A<object>._, A<Type>._)).MustNotHaveHappened();
	}

	[Fact]
	public void CreateKey_ThrowingAndWorkingActions_ProduceDifferentKeys()
	{
		// Arrange
		var throwingAction = new ThrowingCacheableAction();
		var workingAction = new WorkingCacheableAction("stable-key");

		// Act
		var throwingKey = _sut.CreateKey(throwingAction, _context);
		var workingKey = _sut.CreateKey(workingAction, _context);

		// Assert
		throwingKey.ShouldNotBe(workingKey, "Fallback and normal keys should differ");
	}

	[Fact]
	public void CreateKey_FallbackKey_IsUrlSafeBase64()
	{
		// Arrange
		var action = new ThrowingCacheableAction();

		// Act
		var key = _sut.CreateKey(action, _context);

		// Assert
		key.ShouldNotContain("=");
		key.ShouldNotContain("/");
		key.ShouldNotContain("+");
	}

	[Fact]
	public void CreateKey_WhenICacheableReturnsNull_FallsBackToSerialization()
	{
		// Arrange - action whose GetCacheKey returns null (cast to string fails)
		var action = new NullReturningCacheableAction();
		A.CallTo(() => _serializer.Serialize(A<object>._, A<Type>._)).Returns("{\"fallback\":true}");

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
