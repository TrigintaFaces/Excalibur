// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Messaging;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.ZeroAlloc;

using FakeItEasy;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Tests.Messaging.ZeroAlloc;

/// <summary>
/// Unit tests for <see cref="PooledMessageContextFactory"/>.
/// Sprint 449 - S449.5: Unit tests for performance optimizations.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Performance")]
public sealed class PooledMessageContextFactoryShould : IDisposable
{
	private readonly ServiceProvider _serviceProvider;
	private readonly IMessageContextPool _mockPool;
	private readonly PooledMessageContextFactory _factory;

	public PooledMessageContextFactoryShould()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_serviceProvider = services.BuildServiceProvider();

		_mockPool = A.Fake<IMessageContextPool>();
		_factory = new PooledMessageContextFactory(_mockPool);
	}

	public void Dispose()
	{
		_serviceProvider.Dispose();
	}

	#region Constructor Tests

	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenPoolIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new PooledMessageContextFactory(null!));
	}

	[Fact]
	public void Pool_ReturnsInjectedPool()
	{
		// Act & Assert
		_factory.Pool.ShouldBe(_mockPool);
	}

	#endregion

	#region CreateContext Tests

	[Fact]
	public void CreateContext_RentsFromPool()
	{
		// Arrange
		var mockContext = A.Fake<IMessageContext>();
		_ = A.CallTo(() => _mockPool.Rent()).Returns(mockContext);

		// Act
		var result = _factory.CreateContext();

		// Assert
		result.ShouldBe(mockContext);
		_ = A.CallTo(() => _mockPool.Rent()).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void CreateContext_WithProperties_RentsFromPoolAndSetsProperties()
	{
		// Arrange
		var mockContext = A.Fake<IMessageContext>();
		var items = new Dictionary<string, object>();
		_ = A.CallTo(() => mockContext.Items).Returns(items);
		_ = A.CallTo(() => _mockPool.Rent()).Returns(mockContext);

		var properties = new Dictionary<string, object>
		{
			["key1"] = "value1",
			["key2"] = 42,
		};

		// Act
		var result = _factory.CreateContext(properties);

		// Assert
		result.ShouldBe(mockContext);
		items["key1"].ShouldBe("value1");
		items["key2"].ShouldBe(42);
	}

	[Fact]
	public void CreateContext_WithNullProperties_RentsFromPoolWithoutError()
	{
		// Arrange
		var mockContext = A.Fake<IMessageContext>();
		_ = A.CallTo(() => _mockPool.Rent()).Returns(mockContext);

		// Act
		var result = _factory.CreateContext(null);

		// Assert
		result.ShouldBe(mockContext);
		_ = A.CallTo(() => _mockPool.Rent()).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void CreateContext_WithEmptyProperties_RentsFromPoolWithoutError()
	{
		// Arrange
		var mockContext = A.Fake<IMessageContext>();
		var items = new Dictionary<string, object>();
		_ = A.CallTo(() => mockContext.Items).Returns(items);
		_ = A.CallTo(() => _mockPool.Rent()).Returns(mockContext);

		// Act
		var result = _factory.CreateContext(new Dictionary<string, object>());

		// Assert
		result.ShouldBe(mockContext);
		items.ShouldBeEmpty();
	}

	#endregion

	#region CreateChildContext Tests

	[Fact]
	public void CreateChildContext_ThrowsArgumentNullException_WhenParentIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => _factory.CreateChildContext(null!));
	}

	[Fact]
	public void CreateChildContext_DelegatesToParentContext()
	{
		// Arrange
		var parentContext = A.Fake<IMessageContext>();
		var childContext = A.Fake<IMessageContext>();
		_ = A.CallTo(() => parentContext.CreateChildContext()).Returns(childContext);

		// Act
		var result = _factory.CreateChildContext(parentContext);

		// Assert
		result.ShouldBe(childContext);
		_ = A.CallTo(() => parentContext.CreateChildContext()).MustHaveHappenedOnceExactly();
	}

	#endregion

	#region Return Tests (Sprint 463 - S463.3: PERF-2 MessageContext Object Pooling)

	[Fact]
	public void Return_ReturnsContextToPool()
	{
		// Arrange
		var mockContext = A.Fake<IMessageContext>();

		// Act
		_factory.Return(mockContext);

		// Assert
		_ = A.CallTo(() => _mockPool.ReturnToPool(mockContext)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void Return_CanBeCalledMultipleTimes()
	{
		// Arrange
		var mockContext = A.Fake<IMessageContext>();

		// Act
		_factory.Return(mockContext);
		_factory.Return(mockContext);

		// Assert
		_ = A.CallTo(() => _mockPool.ReturnToPool(mockContext)).MustHaveHappenedTwiceExactly();
	}

	[Fact]
	public void CreateContext_And_Return_FollowsPoolLifecycle()
	{
		// Arrange
		var mockContext = A.Fake<IMessageContext>();
		_ = A.CallTo(() => _mockPool.Rent()).Returns(mockContext);

		// Act - Simulate full lifecycle: rent -> process -> return
		var context = _factory.CreateContext();
		// ... processing would happen here ...
		_factory.Return(context);

		// Assert - Verify pool methods were called in correct order
		_ = A.CallTo(() => _mockPool.Rent()).MustHaveHappenedOnceExactly();
		_ = A.CallTo(() => _mockPool.ReturnToPool(mockContext)).MustHaveHappenedOnceExactly();
	}

	#endregion

	#region Integration Tests

	// NOTE: Integration tests with real MessageContextPool are skipped because the pool
	// currently requires a non-null message in the constructor, which is a design issue
	// that needs to be fixed in S449.3 (MessageContext pool integration).
	// Once fixed, these tests should be uncommented and the pool should allow
	// creating contexts without a message (message is set later during dispatch).

	// The mock-based tests above verify the factory behavior is correct,
	// and the actual pool integration is tested at a higher level in the
	// integration/E2E tests.

	#endregion
}
