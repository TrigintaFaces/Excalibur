// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Delivery.Handlers;

namespace Excalibur.Dispatch.Tests.Messaging.Delivery.Handlers;

/// <summary>
/// Unit tests for <see cref="HandlerActivatorRegistry"/> — AOT-safe context setter registry.
/// </summary>
/// <remarks>
/// Post-sprint fix: validates the new registry-based context setter approach
/// that replaces the old PrecompiledHandlerActivator reflection scanning.
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
[Trait("Priority", "0")]
public sealed class HandlerActivatorRegistryShould
{
	#region Test Types

	private sealed class TestMessage : IDispatchMessage;

	private sealed class HandlerWithContext
	{
		public IMessageContext? Context { get; set; }

		public Task HandleAsync(TestMessage message, CancellationToken cancellationToken)
		{
			return Task.CompletedTask;
		}
	}

	private sealed class AnotherHandler
	{
		public IMessageContext? Context { get; set; }

		public Task HandleAsync(TestMessage message, CancellationToken cancellationToken)
		{
			return Task.CompletedTask;
		}
	}

	private sealed class UnregisteredHandler
	{
		public Task HandleAsync(TestMessage message, CancellationToken cancellationToken)
		{
			return Task.CompletedTask;
		}
	}

	#endregion

	#region RegisterContextSetter Tests

	[Fact]
	public void RegisterContextSetter_WithValidSetter_Succeeds()
	{
		// Arrange & Act — should not throw
		HandlerActivatorRegistry.RegisterContextSetter<HandlerWithContext>(
			(handler, context) => handler.Context = context);

		// Assert — registration is visible
		HandlerActivatorRegistry.HasContextSetter(typeof(HandlerWithContext)).ShouldBeTrue();
	}

	[Fact]
	public void RegisterContextSetter_WithNullSetter_ThrowsArgumentNullException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			HandlerActivatorRegistry.RegisterContextSetter<HandlerWithContext>(null!));
	}

	[Fact]
	public void RegisterContextSetter_CalledTwice_OverwritesPreviousRegistration()
	{
		// Arrange
		var firstInvoked = false;
		var secondInvoked = false;

		HandlerActivatorRegistry.RegisterContextSetter<AnotherHandler>(
			(handler, context) => firstInvoked = true);
		HandlerActivatorRegistry.RegisterContextSetter<AnotherHandler>(
			(handler, context) => secondInvoked = true);

		// Act
		HandlerActivatorRegistry.TryGetContextSetter(typeof(AnotherHandler), out var setter);
		setter!.Invoke(new AnotherHandler(), A.Fake<IMessageContext>());

		// Assert — second registration wins (ConcurrentDictionary indexer overwrites)
		firstInvoked.ShouldBeFalse();
		secondInvoked.ShouldBeTrue();
	}

	#endregion

	#region TryGetContextSetter Tests

	[Fact]
	public void TryGetContextSetter_ForRegisteredType_ReturnsTrueAndSetter()
	{
		// Arrange
		HandlerActivatorRegistry.RegisterContextSetter<HandlerWithContext>(
			(handler, context) => handler.Context = context);

		// Act
		var found = HandlerActivatorRegistry.TryGetContextSetter(typeof(HandlerWithContext), out var setter);

		// Assert
		found.ShouldBeTrue();
		setter.ShouldNotBeNull();
	}

	[Fact]
	public void TryGetContextSetter_ForUnregisteredType_ReturnsFalse()
	{
		// Act
		var found = HandlerActivatorRegistry.TryGetContextSetter(typeof(UnregisteredHandler), out var setter);

		// Assert
		found.ShouldBeFalse();
	}

	[Fact]
	public void TryGetContextSetter_InvokesSetter_CorrectlyAppliesContext()
	{
		// Arrange
		var handler = new HandlerWithContext();
		var context = A.Fake<IMessageContext>();

		HandlerActivatorRegistry.RegisterContextSetter<HandlerWithContext>(
			(h, ctx) => h.Context = ctx);

		// Act
		HandlerActivatorRegistry.TryGetContextSetter(typeof(HandlerWithContext), out var setter);
		setter!(handler, context);

		// Assert
		handler.Context.ShouldBeSameAs(context);
	}

	#endregion

	#region HasContextSetter Tests

	[Fact]
	public void HasContextSetter_ForRegisteredType_ReturnsTrue()
	{
		// Arrange
		HandlerActivatorRegistry.RegisterContextSetter<HandlerWithContext>(
			(handler, context) => handler.Context = context);

		// Act & Assert
		HandlerActivatorRegistry.HasContextSetter(typeof(HandlerWithContext)).ShouldBeTrue();
	}

	[Fact]
	public void HasContextSetter_ForUnregisteredType_ReturnsFalse()
	{
		// Act & Assert
		HandlerActivatorRegistry.HasContextSetter(typeof(UnregisteredHandler)).ShouldBeFalse();
	}

	#endregion

	#region Concurrent Registration Tests

	[Fact]
	public void RegisterContextSetter_ConcurrentRegistrations_DoNotThrow()
	{
		// Arrange & Act — multiple threads registering different types should not throw
		var exceptions = new System.Collections.Concurrent.ConcurrentBag<Exception>();

		Parallel.For(0, 100, i =>
		{
			try
			{
				// All register the same type — last-writer wins, but no exceptions
				HandlerActivatorRegistry.RegisterContextSetter<HandlerWithContext>(
					(handler, context) => handler.Context = context);
			}
#pragma warning disable CA1031 // Do not catch general exception types
			catch (Exception ex)
			{
				exceptions.Add(ex);
			}
#pragma warning restore CA1031
		});

		// Assert
		exceptions.ShouldBeEmpty();
		HandlerActivatorRegistry.HasContextSetter(typeof(HandlerWithContext)).ShouldBeTrue();
	}

	#endregion
}
