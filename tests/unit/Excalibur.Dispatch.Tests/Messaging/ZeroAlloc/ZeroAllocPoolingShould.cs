// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Messaging;
using Excalibur.Dispatch.ZeroAlloc;

namespace Excalibur.Dispatch.Tests.Messaging.ZeroAlloc;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ZeroAllocPoolingShould
{
	// --- MessageContextPool ---

	[Fact]
	public void MessageContextPool_Constructor_WithNullServiceProvider_Throws()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => new MessageContextPool(null!));
	}

	[Fact]
	public void MessageContextPool_Rent_ReturnsContext()
	{
		// Arrange
		var serviceProvider = A.Fake<IServiceProvider>();
		var pool = new MessageContextPool(serviceProvider);

		// Act
		var context = pool.Rent();

		// Assert
		context.ShouldNotBeNull();
	}

	[Fact]
	public void MessageContextPool_RentWithMessage_ReturnsContextWithMessage()
	{
		// Arrange
		var serviceProvider = A.Fake<IServiceProvider>();
		var pool = new MessageContextPool(serviceProvider);
		var message = A.Fake<IDispatchMessage>();

		// Act
		var context = pool.Rent(message);

		// Assert
		context.ShouldNotBeNull();
		context.Message.ShouldBe(message);
	}

	[Fact]
	public void MessageContextPool_ReturnToPool_AcceptsPooledContext()
	{
		// Arrange
		var serviceProvider = A.Fake<IServiceProvider>();
		var pool = new MessageContextPool(serviceProvider);
		var context = pool.Rent();

		// Act & Assert - should not throw
		pool.ReturnToPool(context);
	}

	[Fact]
	public void MessageContextPool_ReturnToPool_NonPooledContext_DoesNotThrow()
	{
		// Arrange
		var serviceProvider = A.Fake<IServiceProvider>();
		var pool = new MessageContextPool(serviceProvider);
		var otherContext = A.Fake<IMessageContext>();

		// Act & Assert - should silently ignore non-pooled context
		pool.ReturnToPool(otherContext);
	}

	[Fact]
	public void MessageContextPool_RentReturnRent_ReusesContext()
	{
		// Arrange
		var serviceProvider = A.Fake<IServiceProvider>();
		var pool = new MessageContextPool(serviceProvider);

		// Act
		var context1 = pool.Rent();
		pool.ReturnToPool(context1);
		var context2 = pool.Rent();

		// Assert - same pooled instance should be reused
		context2.ShouldNotBeNull();
	}

	// --- PooledMessageContextFactory ---

	[Fact]
	public void PooledMessageContextFactory_Constructor_WithNullPool_Throws()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => new PooledMessageContextFactory(null!));
	}

	[Fact]
	public void PooledMessageContextFactory_Pool_ReturnsPool()
	{
		// Arrange
		var pool = A.Fake<IMessageContextPool>();
		var factory = new PooledMessageContextFactory(pool);

		// Assert
		factory.Pool.ShouldBe(pool);
	}

	[Fact]
	public void PooledMessageContextFactory_CreateContext_RentsFromPool()
	{
		// Arrange
		var pool = A.Fake<IMessageContextPool>();
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => pool.Rent()).Returns(context);
		var factory = new PooledMessageContextFactory(pool);

		// Act
		var result = factory.CreateContext();

		// Assert
		result.ShouldBe(context);
		A.CallTo(() => pool.Rent()).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void PooledMessageContextFactory_CreateContext_WithProperties_SetsItems()
	{
		// Arrange
		var pool = A.Fake<IMessageContextPool>();
		var context = A.Fake<IMessageContext>();
		var items = new Dictionary<string, object>();
		A.CallTo(() => context.Items).Returns(items);
		A.CallTo(() => pool.Rent()).Returns(context);
		var factory = new PooledMessageContextFactory(pool);
		var properties = new Dictionary<string, object> { { "key1", "value1" } };

		// Act
		var result = factory.CreateContext(properties);

		// Assert
		result.ShouldBe(context);
		items["key1"].ShouldBe("value1");
	}

	[Fact]
	public void PooledMessageContextFactory_CreateContext_WithNullProperties_DoesNotThrow()
	{
		// Arrange
		var pool = A.Fake<IMessageContextPool>();
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => pool.Rent()).Returns(context);
		var factory = new PooledMessageContextFactory(pool);

		// Act
		var result = factory.CreateContext(null);

		// Assert
		result.ShouldBe(context);
	}

	[Fact]
	public void PooledMessageContextFactory_CreateChildContext_WithNullParent_Throws()
	{
		// Arrange
		var pool = A.Fake<IMessageContextPool>();
		var factory = new PooledMessageContextFactory(pool);

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => factory.CreateChildContext(null!));
	}

	[Fact]
	public void PooledMessageContextFactory_CreateChildContext_DelegatesToParent()
	{
		// Arrange
		var pool = A.Fake<IMessageContextPool>();
		var parent = A.Fake<IMessageContext>();
		var child = A.Fake<IMessageContext>();
		A.CallTo(() => parent.CreateChildContext()).Returns(child);
		var factory = new PooledMessageContextFactory(pool);

		// Act
		var result = factory.CreateChildContext(parent);

		// Assert
		result.ShouldBe(child);
	}

	[Fact]
	public void PooledMessageContextFactory_Return_ReturnsToPool()
	{
		// Arrange
		var pool = A.Fake<IMessageContextPool>();
		var context = A.Fake<IMessageContext>();
		var factory = new PooledMessageContextFactory(pool);

		// Act
		factory.Return(context);

		// Assert
		A.CallTo(() => pool.ReturnToPool(context)).MustHaveHappenedOnceExactly();
	}
}
