// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Caching;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Caching.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Caching")]
public sealed class DefaultCacheKeyBuilderShould
{
	private readonly IJsonSerializer _serializer = A.Fake<IJsonSerializer>();
	private readonly DefaultCacheKeyBuilder _sut;

	public DefaultCacheKeyBuilderShould()
	{
		A.CallTo(() => _serializer.Serialize(A<object>._, A<Type>._))
			.Returns("serialized-json");
		_sut = new DefaultCacheKeyBuilder(_serializer, NullLogger<DefaultCacheKeyBuilder>.Instance);
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
		var action = A.Fake<IDispatchAction>();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => _sut.CreateKey(action, null!));
	}

	[Fact]
	public void ReturnNonEmptyKey()
	{
		// Arrange
		var action = A.Fake<IDispatchAction>();
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.TenantId).Returns("tenant1");
		A.CallTo(() => context.UserId).Returns("user1");

		// Act
		var key = _sut.CreateKey(action, context);

		// Assert
		key.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public void ReturnConsistentKey_ForSameInput()
	{
		// Arrange
		var action = A.Fake<IDispatchAction>();
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.TenantId).Returns("tenant1");
		A.CallTo(() => context.UserId).Returns("user1");

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
		var action = A.Fake<IDispatchAction>();
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.TenantId).Returns(null);
		A.CallTo(() => context.UserId).Returns("user1");

		// Act
		var key = _sut.CreateKey(action, context);

		// Assert
		key.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public void UseAnonymousUserId_WhenUserIdIsNull()
	{
		// Arrange
		var action = A.Fake<IDispatchAction>();
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.TenantId).Returns("tenant1");
		A.CallTo(() => context.UserId).Returns(null);

		// Act
		var key = _sut.CreateKey(action, context);

		// Assert
		key.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public void ProduceDifferentKeys_ForDifferentTenants()
	{
		// Arrange
		var action = A.Fake<IDispatchAction>();

		var context1 = A.Fake<IMessageContext>();
		A.CallTo(() => context1.TenantId).Returns("tenant-a");
		A.CallTo(() => context1.UserId).Returns("user1");

		var context2 = A.Fake<IMessageContext>();
		A.CallTo(() => context2.TenantId).Returns("tenant-b");
		A.CallTo(() => context2.UserId).Returns("user1");

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
		var action = A.Fake<IDispatchAction>();

		var context1 = A.Fake<IMessageContext>();
		A.CallTo(() => context1.TenantId).Returns("tenant1");
		A.CallTo(() => context1.UserId).Returns("user-a");

		var context2 = A.Fake<IMessageContext>();
		A.CallTo(() => context2.TenantId).Returns("tenant1");
		A.CallTo(() => context2.UserId).Returns("user-b");

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
		var action = A.Fake<IDispatchAction>();
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.TenantId).Returns("tenant1");
		A.CallTo(() => context.UserId).Returns("user1");

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
		var action = A.Fake<IDispatchAction>();
		var context = A.Fake<IMessageContext>();

		// Act
		var key = builder.CreateKey(action, context);

		// Assert
		key.ShouldNotBeNullOrWhiteSpace();
	}
}
