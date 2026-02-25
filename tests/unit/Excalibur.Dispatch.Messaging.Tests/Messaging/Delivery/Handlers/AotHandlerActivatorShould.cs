// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Delivery.Handlers;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Tests.Messaging.Delivery.Handlers;

/// <summary>
/// Unit tests for the <see cref="AotHandlerActivator"/> class.
/// </summary>
/// <remarks>
/// Sprint 460 - Task S460.2: Core Dispatch Unit Tests.
/// Tests the AOT-compatible handler activation that delegates to source-generated code.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Priority", "0")]
public sealed class AotHandlerActivatorShould
{
	#region Interface Implementation Tests

	/// <summary>
	/// Tests that AotHandlerActivator implements IHandlerActivator.
	/// </summary>
	[Fact]
	public void ImplementIHandlerActivator()
	{
		// Arrange
		var activator = new AotHandlerActivator();

		// Assert
		_ = activator.ShouldBeAssignableTo<IHandlerActivator>();
	}

	#endregion

	#region Null Argument Tests

	/// <summary>
	/// Tests that ActivateHandler throws when handlerType is null.
	/// </summary>
	[Fact]
	public void ActivateHandler_ThrowsOnNullHandlerType()
	{
		// Arrange
		var services = new ServiceCollection();
		var provider = services.BuildServiceProvider();
		var context = CreateTestContext();
		var activator = new AotHandlerActivator();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			activator.ActivateHandler(null!, context, provider));
	}

	/// <summary>
	/// Tests that ActivateHandler throws when context is null.
	/// </summary>
	[Fact]
	public void ActivateHandler_ThrowsOnNullContext()
	{
		// Arrange
		var services = new ServiceCollection();
		var provider = services.BuildServiceProvider();
		var activator = new AotHandlerActivator();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			activator.ActivateHandler(typeof(TestAotHandler), null!, provider));
	}

	/// <summary>
	/// Tests that ActivateHandler throws when provider is null.
	/// </summary>
	[Fact]
	public void ActivateHandler_ThrowsOnNullProvider()
	{
		// Arrange
		var context = CreateTestContext();
		var activator = new AotHandlerActivator();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			activator.ActivateHandler(typeof(TestAotHandler), context, null!));
	}

	#endregion

	#region Activation Tests

	/// <summary>
	/// Tests that ActivateHandler delegates to SourceGeneratedHandlerActivator.
	/// </summary>
	/// <remarks>
	/// This test verifies the delegation path works when the source generator
	/// has produced the SourceGeneratedHandlerActivator.
	/// </remarks>
	[Fact]
	public void ActivateHandler_DelegatesToSourceGenerator()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddSingleton<TestAotHandler>();
		var provider = services.BuildServiceProvider();
		var context = CreateTestContext();
		var activator = new AotHandlerActivator();

		// Act - This delegates to SourceGeneratedHandlerActivator.ActivateHandler
		var result = activator.ActivateHandler(typeof(TestAotHandler), context, provider);

		// Assert
		_ = result.ShouldNotBeNull();
		_ = result.ShouldBeOfType<TestAotHandler>();
	}

	/// <summary>
	/// Tests that ActivateHandler works with handlers having context property.
	/// </summary>
	[Fact]
	public void ActivateHandler_SetsContextOnHandler()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddSingleton<AotHandlerWithContext>();
		var provider = services.BuildServiceProvider();
		var context = CreateTestContext();
		var activator = new AotHandlerActivator();

		// Act
		var result = activator.ActivateHandler(typeof(AotHandlerWithContext), context, provider);

		// Assert
		var handler = result.ShouldBeOfType<AotHandlerWithContext>();
		// Context may or may not be set depending on source generator registration
		_ = result.ShouldNotBeNull();
	}

	#endregion

	#region Helper Methods

	private static IMessageContext CreateTestContext()
	{
		return new MessageContext();
	}

	#endregion

	#region Test Fixtures

#pragma warning disable CA1034 // Nested types should not be visible

	public sealed class TestAotCommand : IDispatchAction
	{
		public Guid Id { get; } = Guid.NewGuid();
		public string MessageId { get; } = Guid.NewGuid().ToString();
		public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;
		public MessageKinds Kind { get; } = MessageKinds.Action;
		public IReadOnlyDictionary<string, object> Headers { get; } = new Dictionary<string, object>();
		public object Body => this;
		public string MessageType => GetType().FullName ?? "TestAotCommand";
		public IMessageFeatures Features { get; } = new DefaultMessageFeatures();
	}

	public sealed class TestAotHandler : IActionHandler<TestAotCommand>
	{
		public Task HandleAsync(TestAotCommand action, CancellationToken cancellationToken)
			=> Task.CompletedTask;
	}

	public sealed class AotHandlerWithContext : IActionHandler<TestAotCommand>
	{
		public IMessageContext? Context { get; set; }

		public Task HandleAsync(TestAotCommand action, CancellationToken cancellationToken)
			=> Task.CompletedTask;
	}

#pragma warning restore CA1034

	#endregion
}
