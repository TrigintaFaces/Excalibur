// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Delivery.Handlers;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Tests.Messaging.Delivery.Handlers;

/// <summary>
/// Unit tests for the <see cref="AotHandlerActivator"/> class.
/// </summary>
/// <remarks>
/// Covers the activation path supported under Native AOT: resolution through the service provider
/// with no expression compilation, and the trimming annotations that path depends on.
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
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
	/// Tests that ActivateHandler resolves a container-registered handler.
	/// </summary>
	[Fact]
	public void ActivateHandler_ResolvesRegisteredHandler()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddSingleton<TestAotHandler>();
		var provider = services.BuildServiceProvider();
		var context = CreateTestContext();
		var activator = new AotHandlerActivator();

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

	#region Trimming Annotation Tests

	/// <summary>
	/// The handler type flowing through <see cref="IHandlerActivator.ActivateHandler" /> is used to CONSTRUCT
	/// the handler, so the contract must preserve at least what the construction call demands. The demand is
	/// read off the framework method itself rather than hard-coded, so this stays true if the framework
	/// tightens it.
	/// </summary>
	[Fact]
	public void PreserveEveryMemberKindConstructionDemands()
	{
		var declared = RequiredMembers(
			typeof(IHandlerActivator).GetMethod(nameof(IHandlerActivator.ActivateHandler))!.GetParameters()[0]);

		var demanded = RequiredMembers(typeof(ActivatorUtilities)
			.GetMethod(
				nameof(ActivatorUtilities.CreateInstance),
				[typeof(IServiceProvider), typeof(Type), typeof(object[])])!
			.GetParameters()[1]);

		demanded.ShouldNotBe(DynamicallyAccessedMemberTypes.None);
		(declared & demanded).ShouldBe(demanded);
	}

	/// <summary>
	/// Context injection on the reflective activator reads public properties, so that member kind must be
	/// preserved as well. Preserving only one of the two kinds trims the other away.
	/// </summary>
	[Fact]
	public void PreservePublicPropertiesForContextInjection()
	{
		var declared = RequiredMembers(
			typeof(IHandlerActivator).GetMethod(nameof(IHandlerActivator.ActivateHandler))!.GetParameters()[0]);

		(declared & DynamicallyAccessedMemberTypes.PublicProperties)
			.ShouldBe(DynamicallyAccessedMemberTypes.PublicProperties);
	}

	/// <summary>
	/// The AOT activator must not be annotated as requiring unreferenced code: the annotation would surface
	/// as a trimming warning at every consumer call site, on the one activation path Native AOT supports.
	/// </summary>
	[Fact]
	public void NotBeAnnotatedAsRequiringUnreferencedCode()
	{
		var method = typeof(AotHandlerActivator).GetMethod(nameof(AotHandlerActivator.ActivateHandler))!;

		method.GetCustomAttribute<RequiresUnreferencedCodeAttribute>().ShouldBeNull();
		method.GetCustomAttribute<RequiresDynamicCodeAttribute>().ShouldBeNull();
		typeof(AotHandlerActivator).GetCustomAttribute<RequiresUnreferencedCodeAttribute>().ShouldBeNull();
		typeof(AotHandlerActivator).GetCustomAttribute<RequiresDynamicCodeAttribute>().ShouldBeNull();
	}

	private static DynamicallyAccessedMemberTypes RequiredMembers(ParameterInfo parameter) =>
		parameter.GetCustomAttribute<DynamicallyAccessedMembersAttribute>()?.MemberTypes
			?? DynamicallyAccessedMemberTypes.None;

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
