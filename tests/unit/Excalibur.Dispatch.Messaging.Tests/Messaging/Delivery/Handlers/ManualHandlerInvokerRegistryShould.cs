// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Delivery.Handlers;

namespace Excalibur.Dispatch.Tests.Messaging.Delivery.Handlers;

/// <summary>
/// Unit tests for <see cref="HandlerInvokerRegistry"/> (Manual implementation).
/// </summary>
[Collection("HandlerInvokerRegistry")]
[Trait("Category", "Unit")]
[Trait("Component", "Handlers")]
[Trait("Priority", "0")]
public sealed class ManualHandlerInvokerRegistryShould : IDisposable
{
	public ManualHandlerInvokerRegistryShould()
	{
		// Clear cache before each test to ensure isolation
		HandlerInvokerRegistry.ClearCache();
	}

	public void Dispose()
	{
		// Clean up after each test
		HandlerInvokerRegistry.ClearCache();
	}

	#region Test Types

	private sealed class TestMessage : IDispatchMessage
	{
		public string Value { get; init; } = string.Empty;
	}

	private sealed class TestResultMessage : IDispatchAction<string>
	{
		public string Input { get; init; } = string.Empty;
	}

	private sealed class TestHandler
	{
		public bool WasInvoked { get; private set; }
		public Task HandleAsync(TestMessage message, CancellationToken cancellationToken)
		{
			WasInvoked = true;
			return Task.CompletedTask;
		}
	}

	private sealed class TestHandlerWithResult
	{
		public bool WasInvoked { get; private set; }
		public Task<string> HandleAsync(TestResultMessage message, CancellationToken cancellationToken)
		{
			WasInvoked = true;
			return Task.FromResult($"Result: {message.Input}");
		}
	}

	#endregion

	#region IsCacheFrozen Tests

	[Fact]
	public void IsCacheFrozen_InitiallyFalse()
	{
		// Assert
		HandlerInvokerRegistry.IsCacheFrozen.ShouldBeFalse();
	}

	[Fact]
	public void IsCacheFrozen_TrueAfterFreeze()
	{
		// Act
		HandlerInvokerRegistry.FreezeCache();

		// Assert
		HandlerInvokerRegistry.IsCacheFrozen.ShouldBeTrue();
	}

	#endregion

	#region RegisterInvoker Tests (Void)

	[Fact]
	public void RegisterInvoker_WithVoidHandler_RegistersSuccessfully()
	{
		// Arrange
		Func<TestHandler, TestMessage, CancellationToken, Task> invoker =
			(handler, message, ct) => handler.HandleAsync(message, ct);

		// Act & Assert - should not throw
		Should.NotThrow(() =>
			HandlerInvokerRegistry.RegisterInvoker(invoker));
	}

	[Fact]
	public void RegisterInvoker_AfterFreeze_ThrowsInvalidOperationException()
	{
		// Arrange
		HandlerInvokerRegistry.FreezeCache();

		Func<TestHandler, TestMessage, CancellationToken, Task> invoker =
			(handler, message, ct) => handler.HandleAsync(message, ct);

		// Act & Assert
		_ = Should.Throw<InvalidOperationException>(() =>
			HandlerInvokerRegistry.RegisterInvoker(invoker));
	}

	#endregion

	#region RegisterInvoker Tests (With Result)

	[Fact]
	public void RegisterInvoker_WithResultHandler_RegistersSuccessfully()
	{
		// Arrange
		Func<TestHandlerWithResult, TestResultMessage, CancellationToken, Task<string>> invoker =
			(handler, message, ct) => handler.HandleAsync(message, ct);

		// Act & Assert - should not throw
		Should.NotThrow(() =>
			HandlerInvokerRegistry.RegisterInvoker(invoker));
	}

	[Fact]
	public void RegisterInvoker_WithResult_AfterFreeze_ThrowsInvalidOperationException()
	{
		// Arrange
		HandlerInvokerRegistry.FreezeCache();

		Func<TestHandlerWithResult, TestResultMessage, CancellationToken, Task<string>> invoker =
			(handler, message, ct) => handler.HandleAsync(message, ct);

		// Act & Assert
		_ = Should.Throw<InvalidOperationException>(() =>
			HandlerInvokerRegistry.RegisterInvoker(invoker));
	}

	#endregion

	#region GetInvoker Tests

	[Fact]
	public async Task GetInvoker_ForRegisteredHandler_ReturnsInvoker()
	{
		// Arrange
		Func<TestHandler, TestMessage, CancellationToken, Task> invoker =
			(handler, message, ct) => handler.HandleAsync(message, ct);
		HandlerInvokerRegistry.RegisterInvoker(invoker);

		var handler = new TestHandler();
		var message = new TestMessage();

		// Act
		var retrievedInvoker = HandlerInvokerRegistry.GetInvoker(typeof(TestHandler));

		// Assert
		_ = retrievedInvoker.ShouldNotBeNull();
		_ = await retrievedInvoker(handler, message, CancellationToken.None);
		handler.WasInvoked.ShouldBeTrue();
	}

	[Fact]
	public async Task GetInvoker_ForRegisteredHandlerWithResult_ReturnsInvokerWithResult()
	{
		// Arrange
		Func<TestHandlerWithResult, TestResultMessage, CancellationToken, Task<string>> invoker =
			(handler, message, ct) => handler.HandleAsync(message, ct);
		HandlerInvokerRegistry.RegisterInvoker(invoker);

		var handler = new TestHandlerWithResult();
		var message = new TestResultMessage { Input = "test" };

		// Act
		var retrievedInvoker = HandlerInvokerRegistry.GetInvoker(typeof(TestHandlerWithResult));
		var result = await retrievedInvoker(handler, message, CancellationToken.None);

		// Assert
		result.ShouldBe("Result: test");
	}

	[Fact]
	public void GetInvoker_ForUnregisteredHandler_CreatesInvokerViaReflection()
	{
		// Arrange - Don't register the handler explicitly

		// Act
		var invoker = HandlerInvokerRegistry.GetInvoker(typeof(TestHandler));

		// Assert - Should create an invoker via reflection
		_ = invoker.ShouldNotBeNull();
	}

	[Fact]
	public async Task GetInvoker_AfterFreeze_ReturnsFromFrozenCache()
	{
		// Arrange
		Func<TestHandler, TestMessage, CancellationToken, Task> invoker =
			(handler, message, ct) => handler.HandleAsync(message, ct);
		HandlerInvokerRegistry.RegisterInvoker(invoker);
		HandlerInvokerRegistry.FreezeCache();

		var handler = new TestHandler();
		var message = new TestMessage();

		// Act
		var retrievedInvoker = HandlerInvokerRegistry.GetInvoker(typeof(TestHandler));

		// Assert
		_ = retrievedInvoker.ShouldNotBeNull();
		_ = await retrievedInvoker(handler, message, CancellationToken.None);
		handler.WasInvoked.ShouldBeTrue();
	}

	#endregion

	#region FreezeCache Tests

	[Fact]
	public void FreezeCache_WhenCalled_SetsFrozenFlag()
	{
		// Act
		HandlerInvokerRegistry.FreezeCache();

		// Assert
		HandlerInvokerRegistry.IsCacheFrozen.ShouldBeTrue();
	}

	[Fact]
	public void FreezeCache_CalledMultipleTimes_IsIdempotent()
	{
		// Act
		HandlerInvokerRegistry.FreezeCache();
		HandlerInvokerRegistry.FreezeCache();
		HandlerInvokerRegistry.FreezeCache();

		// Assert - Should not throw and remain frozen
		HandlerInvokerRegistry.IsCacheFrozen.ShouldBeTrue();
	}

	[Fact]
	public void FreezeCache_PreservesRegisteredInvokers()
	{
		// Arrange
		Func<TestHandler, TestMessage, CancellationToken, Task> invoker =
			(handler, message, ct) => handler.HandleAsync(message, ct);
		HandlerInvokerRegistry.RegisterInvoker(invoker);

		// Act
		HandlerInvokerRegistry.FreezeCache();
		var retrievedInvoker = HandlerInvokerRegistry.GetInvoker(typeof(TestHandler));

		// Assert
		_ = retrievedInvoker.ShouldNotBeNull();
	}

	#endregion

	#region ClearCache Tests

	[Fact]
	public void ClearCache_ResetsFrozenFlag()
	{
		// Arrange
		HandlerInvokerRegistry.FreezeCache();
		HandlerInvokerRegistry.IsCacheFrozen.ShouldBeTrue();

		// Act
		HandlerInvokerRegistry.ClearCache();

		// Assert
		HandlerInvokerRegistry.IsCacheFrozen.ShouldBeFalse();
	}

	[Fact]
	public void ClearCache_AllowsNewRegistrations()
	{
		// Arrange
		HandlerInvokerRegistry.FreezeCache();

		// Act
		HandlerInvokerRegistry.ClearCache();

		// Assert - Should allow registration after clearing
		Func<TestHandler, TestMessage, CancellationToken, Task> invoker =
			(handler, message, ct) => handler.HandleAsync(message, ct);
		Should.NotThrow(() =>
			HandlerInvokerRegistry.RegisterInvoker(invoker));
	}

	#endregion

	#region Error Handling Tests

	[Fact]
	public void GetInvoker_ForTypeWithoutHandleAsync_ThrowsInvalidOperationException()
	{
		// Arrange
		var typeWithoutHandleAsync = typeof(string);

		// Act & Assert
		_ = Should.Throw<InvalidOperationException>(() =>
			HandlerInvokerRegistry.GetInvoker(typeWithoutHandleAsync));
	}

	#endregion
}
