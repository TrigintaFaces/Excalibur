// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.ZeroAlloc;

namespace Excalibur.Dispatch.Tests.Messaging.Delivery;

/// <summary>
/// Unit tests for MessageContextFactory and IMessageContextFactory DI resolution (Sprint 70 - Task av82).
/// </summary>
[Trait("Category", "Unit")]
public sealed class MessageContextFactoryShould
{
	private readonly IServiceProvider _serviceProvider = A.Fake<IServiceProvider>();

	#region Sprint 70 - IMessageContextFactory DI Resolution Tests (Task av82)

	/// <summary>
	/// Verifies that IMessageContextFactory resolves correctly from DI.
	/// </summary>
	[Fact]
	public void Resolve_From_ServiceProvider()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddDispatchPipeline();
		var provider = services.BuildServiceProvider();

		// Act
		var factory = provider.GetService<IMessageContextFactory>();

		// Assert
		_ = factory.ShouldNotBeNull();
		_ = factory.ShouldBeOfType<PooledMessageContextFactory>();
	}

	/// <summary>
	/// Verifies that CreateContext() returns a valid MessageContext.
	/// </summary>
	[Fact]
	public void CreateContext_Should_Return_Valid_MessageContext()
	{
		// Arrange
		var factory = new MessageContextFactory(_serviceProvider);

		// Act
		var context = factory.CreateContext();

		// Assert
		_ = context.ShouldNotBeNull();
		_ = context.ShouldBeAssignableTo<IMessageContext>();
		_ = context.ShouldBeOfType<MessageContext>();
		context.RequestServices.ShouldBe(_serviceProvider);
	}

	/// <summary>
	/// Verifies that CreateContext() returns unique instances each time.
	/// </summary>
	[Fact]
	public void CreateContext_Should_Return_New_Instance_Each_Time()
	{
		// Arrange
		var factory = new MessageContextFactory(_serviceProvider);

		// Act
		var context1 = factory.CreateContext();
		var context2 = factory.CreateContext();

		// Assert
		context1.ShouldNotBeSameAs(context2);
	}

	/// <summary>
	/// Verifies that CreateContext(properties) populates the context Items dictionary.
	/// </summary>
	[Fact]
	public void CreateContext_With_Properties_Should_Populate_Items()
	{
		// Arrange
		var factory = new MessageContextFactory(_serviceProvider);
		var properties = new Dictionary<string, object>
		{
			["key1"] = "value1",
			["key2"] = 42,
			["key3"] = new object(),
		};

		// Act
		var context = factory.CreateContext(properties);

		// Assert
		_ = context.ShouldNotBeNull();
		context.Items.ShouldContainKey("key1");
		context.Items["key1"].ShouldBe("value1");
		context.Items.ShouldContainKey("key2");
		context.Items["key2"].ShouldBe(42);
		context.Items.ShouldContainKey("key3");
	}

	/// <summary>
	/// Verifies that CreateContext with null properties returns an empty Items dictionary.
	/// </summary>
	[Fact]
	public void CreateContext_With_Null_Properties_Should_Return_Empty_Items()
	{
		// Arrange
		var factory = new MessageContextFactory(_serviceProvider);

		// Act
		var context = factory.CreateContext(null);

		// Assert
		_ = context.ShouldNotBeNull();
		context.Items.ShouldBeEmpty();
	}

	/// <summary>
	/// Verifies that CreateChildContext(parent) delegates to parent's CreateChildContext method.
	/// </summary>
	[Fact]
	public void CreateChildContext_Should_Delegate_To_Parent()
	{
		// Arrange
		var factory = new MessageContextFactory(_serviceProvider);
		var parentContext = new MessageContext
		{
			MessageId = "parent-msg-id",
			CorrelationId = "correlation-123",
			TenantId = "tenant-abc",
			UserId = "user-xyz",
		};
		parentContext.Initialize(_serviceProvider);

		// Act
		var childContext = factory.CreateChildContext(parentContext);

		// Assert
		_ = childContext.ShouldNotBeNull();
		childContext.ShouldNotBeSameAs(parentContext);

		// Child should have propagated identifiers
		childContext.CorrelationId.ShouldBe(parentContext.CorrelationId);
		childContext.TenantId.ShouldBe(parentContext.TenantId);
		childContext.UserId.ShouldBe(parentContext.UserId);

		// Child should have parent's MessageId as CausationId
		childContext.CausationId.ShouldBe(parentContext.MessageId);

		// Child should have a new MessageId
		childContext.MessageId.ShouldNotBe(parentContext.MessageId);
		childContext.MessageId.ShouldNotBeNullOrEmpty();
	}

	/// <summary>
	/// Verifies that CreateChildContext throws ArgumentNullException when parent is null.
	/// </summary>
	[Fact]
	public void CreateChildContext_Should_Throw_When_Parent_Is_Null()
	{
		// Arrange
		var factory = new MessageContextFactory(_serviceProvider);

		// Act & Assert
		var exception = Should.Throw<ArgumentNullException>(() => factory.CreateChildContext(null!));
		exception.ParamName.ShouldBe("parent");
	}

	/// <summary>
	/// Verifies that Return does not throw for the non-pooled factory (no-op).
	/// Sprint 463 - S463.3: PERF-2 MessageContext Object Pooling.
	/// </summary>
	[Fact]
	public void Return_Should_Not_Throw_For_NonPooled_Factory()
	{
		// Arrange
		var factory = new MessageContextFactory(_serviceProvider);
		var context = factory.CreateContext();

		// Act & Assert - Should not throw
		Should.NotThrow(() => factory.Return(context));
	}

	/// <summary>
	/// Verifies that Return can be called with a context not created by this factory.
	/// Sprint 463 - S463.3: PERF-2 MessageContext Object Pooling.
	/// </summary>
	[Fact]
	public void Return_Should_Accept_Any_Context()
	{
		// Arrange
		var factory = new MessageContextFactory(_serviceProvider);
		var context = A.Fake<IMessageContext>();

		// Act & Assert - Should not throw even for fake context
		Should.NotThrow(() => factory.Return(context));
	}

	/// <summary>
	/// Verifies that factory is registered as singleton in DI.
	/// </summary>
	[Fact]
	public void Should_Be_Registered_As_Singleton()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddDispatchPipeline();
		var provider = services.BuildServiceProvider();

		// Act
		var factory1 = provider.GetService<IMessageContextFactory>();
		var factory2 = provider.GetService<IMessageContextFactory>();

		// Assert
		factory1.ShouldBeSameAs(factory2);
	}

	/// <summary>
	/// Verifies that the factory injects the service provider into created contexts.
	/// </summary>
	[Fact]
	public void CreateContext_Should_Inject_ServiceProvider_Into_Context()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddDispatchPipeline();
		_ = services.AddSingleton<ITestService, TestService>();
		var provider = services.BuildServiceProvider();

		var factory = provider.GetRequiredService<IMessageContextFactory>();

		// Act
		var context = factory.CreateContext();

		// Assert
		_ = context.RequestServices.ShouldNotBeNull();
		var testService = context.RequestServices.GetService<ITestService>();
		_ = testService.ShouldNotBeNull();
		_ = testService.ShouldBeOfType<TestService>();
	}

	#endregion Sprint 70 - IMessageContextFactory DI Resolution Tests (Task av82)

	// Test service interface and implementation for DI verification
	private interface ITestService
	{ }

	private sealed class TestService : ITestService
	{ }
}
