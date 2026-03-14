// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Features;
using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Caching;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Caching.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Caching")]
public sealed class DefaultCacheKeyBuilderShould : IDisposable
{
	private readonly DispatchJsonSerializer _serializer = new();
	private readonly DefaultCacheKeyBuilder _sut;

	/// <summary>
	/// Concrete action type that the real <see cref="DispatchJsonSerializer"/> can serialize
	/// (unlike FakeItEasy Castle.Proxies which are not in the source-gen context).
	/// </summary>
	private sealed class TestAction : IDispatchAction
	{
		public string Id { get; init; } = "default";
	}

	/// <summary>
	/// A second concrete action type to produce different serialized output.
	/// </summary>
	private sealed class OtherAction : IDispatchAction
	{
		public string Value { get; init; } = "other";
	}

	public DefaultCacheKeyBuilderShould()
	{
		_sut = new DefaultCacheKeyBuilder(_serializer, NullLogger<DefaultCacheKeyBuilder>.Instance);
	}

	public void Dispose()
	{
		_serializer.Dispose();
	}

	private static IMessageContext CreateFakeContext(string? tenantId, string? userId)
	{
		var context = A.Fake<IMessageContext>();
		var features = new Dictionary<Type, object>();
		var items = new Dictionary<string, object>(StringComparer.Ordinal);
		A.CallTo(() => context.Features).Returns(features);
		A.CallTo(() => context.Items).Returns(items);

		var identity = new MessageIdentityFeature { TenantId = tenantId, UserId = userId };
		features[typeof(IMessageIdentityFeature)] = identity;

		return context;
	}

	[Fact]
	public void ThrowArgumentNullException_WhenActionIsNull()
	{
		// Arrange
		var context = A.Fake<IMessageContext>();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => _sut.CreateKey(null!, context));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenContextIsNull()
	{
		// Arrange
		var action = new TestAction();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => _sut.CreateKey(action, null!));
	}

	[Fact]
	public void ReturnNonEmptyKey()
	{
		// Arrange
		var action = new TestAction();
		var context = CreateFakeContext("tenant1", "user1");

		// Act
		var key = _sut.CreateKey(action, context);

		// Assert
		key.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public void ReturnConsistentKey_ForSameInput()
	{
		// Arrange
		var action = new TestAction();
		var context = CreateFakeContext("tenant1", "user1");

		// Act
		var key1 = _sut.CreateKey(action, context);
		var key2 = _sut.CreateKey(action, context);

		// Assert
		key1.ShouldBe(key2);
	}

	[Fact]
	public void UseGlobalTenantId_WhenTenantIdIsNull()
	{
		// Arrange
		var action = new TestAction();
		var context = CreateFakeContext(null, "user1");

		// Act
		var key = _sut.CreateKey(action, context);

		// Assert
		key.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public void UseAnonymousUserId_WhenUserIdIsNull()
	{
		// Arrange
		var action = new TestAction();
		var context = CreateFakeContext("tenant1", null);

		// Act
		var key = _sut.CreateKey(action, context);

		// Assert
		key.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public void ProduceDifferentKeys_ForDifferentTenants()
	{
		// Arrange
		var action = new TestAction();

		var context1 = CreateFakeContext("tenant-a", "user1");
		var context2 = CreateFakeContext("tenant-b", "user1");

		// Act
		var key1 = _sut.CreateKey(action, context1);
		var key2 = _sut.CreateKey(action, context2);

		// Assert
		key1.ShouldNotBe(key2);
	}

	[Fact]
	public void ProduceDifferentKeys_ForDifferentUsers()
	{
		// Arrange
		var action = new TestAction();

		var context1 = CreateFakeContext("tenant1", "user-a");
		var context2 = CreateFakeContext("tenant1", "user-b");

		// Act
		var key1 = _sut.CreateKey(action, context1);
		var key2 = _sut.CreateKey(action, context2);

		// Assert
		key1.ShouldNotBe(key2);
	}

	[Fact]
	public void ProduceUrlSafeKey()
	{
		// Arrange
		var action = new TestAction();
		var context = CreateFakeContext("tenant1", "user1");

		// Act
		var key = _sut.CreateKey(action, context);

		// Assert
		key.ShouldNotContain("=");
		key.ShouldNotContain("/");
		key.ShouldNotContain("+");
	}

	[Fact]
	public void WorkWithNullLogger()
	{
		// Arrange
		var builder = new DefaultCacheKeyBuilder(_serializer);
		var action = new TestAction();
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.Features).Returns(new Dictionary<Type, object>());
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>(StringComparer.Ordinal));

		// Act
		var key = builder.CreateKey(action, context);

		// Assert
		key.ShouldNotBeNullOrWhiteSpace();
	}
}
