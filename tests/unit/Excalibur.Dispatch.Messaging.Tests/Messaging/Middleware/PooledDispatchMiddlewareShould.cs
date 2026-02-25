// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Middleware;
using Excalibur.Dispatch.Tests.TestFakes;

using MessageResult = Excalibur.Dispatch.Abstractions.MessageResult;

namespace Excalibur.Dispatch.Tests.Messaging.Middleware;

/// <summary>
/// Unit tests for the <see cref="PooledDispatchMiddleware"/> abstract class.
/// </summary>
/// <remarks>
/// Sprint 554 - Task S554.43: PooledDispatchMiddleware tests.
/// Tests object pool usage, middleware delegation, reset behavior, and poolability state.
/// Uses a concrete test subclass to exercise the abstract base class behavior.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Middleware")]
public sealed class PooledDispatchMiddlewareShould
{
	private static DispatchRequestDelegate CreateSuccessDelegate()
	{
		return (msg, ctx, ct) => new ValueTask<IMessageResult>(MessageResult.Success());
	}

	#region CanBePooled Tests

	[Fact]
	public void BePoolableByDefault()
	{
		// Arrange
		var middleware = new TestablePooledMiddleware();

		// Assert
		middleware.CanBePooled.ShouldBeTrue();
	}

	[Fact]
	public void NotBePoolable_WhenInUse()
	{
		// Arrange
		var middleware = new TestablePooledMiddleware();

		// Act
		middleware.SimulateRent();

		// Assert
		middleware.CanBePooled.ShouldBeFalse();
	}

	[Fact]
	public void BePoolable_AfterReset()
	{
		// Arrange
		var middleware = new TestablePooledMiddleware();
		middleware.SimulateRent();
		middleware.CanBePooled.ShouldBeFalse();

		// Act
		middleware.Reset();

		// Assert
		middleware.CanBePooled.ShouldBeTrue();
	}

	[Fact]
	public void NotBePoolable_AfterMarkedAsUnpoolable()
	{
		// Arrange
		var middleware = new TestablePooledMiddleware();

		// Act
		middleware.SimulateMarkUnpoolable();

		// Assert
		middleware.CanBePooled.ShouldBeFalse();
	}

	[Fact]
	public void NotBePoolable_AfterReset_WhenMarkedAsUnpoolable()
	{
		// Arrange
		var middleware = new TestablePooledMiddleware();
		middleware.SimulateMarkUnpoolable();

		// Act
		middleware.Reset();

		// Assert - Still not poolable even after reset
		middleware.CanBePooled.ShouldBeFalse();
	}

	#endregion

	#region Reset Tests

	[Fact]
	public void ClearIsInUseFlag_OnReset()
	{
		// Arrange
		var middleware = new TestablePooledMiddleware();
		middleware.SimulateRent();
		middleware.IsInUsePublic.ShouldBeTrue();

		// Act
		middleware.Reset();

		// Assert
		middleware.IsInUsePublic.ShouldBeFalse();
	}

	[Fact]
	public void CallResetState_OnReset()
	{
		// Arrange
		var middleware = new TestablePooledMiddleware();
		middleware.SimulateRent();
		middleware.SetCustomState("important-data");

		// Act
		middleware.Reset();

		// Assert
		middleware.ResetStateCalled.ShouldBeTrue();
		middleware.CustomState.ShouldBeNull();
	}

	#endregion

	#region OnRent and OnReturn Tests

	[Fact]
	public void SetIsInUse_OnRent()
	{
		// Arrange
		var middleware = new TestablePooledMiddleware();
		middleware.IsInUsePublic.ShouldBeFalse();

		// Act
		middleware.SimulateRent();

		// Assert
		middleware.IsInUsePublic.ShouldBeTrue();
	}

	[Fact]
	public void ResetState_OnReturn()
	{
		// Arrange
		var middleware = new TestablePooledMiddleware();
		middleware.SimulateRent();
		middleware.SetCustomState("data");

		// Act
		middleware.SimulateReturn();

		// Assert
		middleware.IsInUsePublic.ShouldBeFalse();
		middleware.CustomState.ShouldBeNull();
	}

	#endregion

	#region Middleware Delegation via ProcessAsync Tests

	[Fact]
	public async Task CallOnRentAndOnReturn_DuringProcessing()
	{
		// Arrange
		var middleware = new TestablePooledMiddleware();
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "test-msg-1" };

		// Act
		_ = await middleware.InvokeAsync(message, context, CreateSuccessDelegate(), CancellationToken.None);

		// Assert - After InvokeAsync completes, OnReturn should have been called
		middleware.OnRentCalled.ShouldBeTrue();
		middleware.OnReturnCalled.ShouldBeTrue();
		middleware.IsInUsePublic.ShouldBeFalse(); // Returned to pool
	}

	[Fact]
	public async Task DelegateToNextMiddleware()
	{
		// Arrange
		var middleware = new TestablePooledMiddleware();
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "test-msg-1" };
		var nextCalled = false;

		DispatchRequestDelegate next = (msg, ctx, ct) =>
		{
			nextCalled = true;
			return new ValueTask<IMessageResult>(MessageResult.Success());
		};

		// Act
		var result = await middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		nextCalled.ShouldBeTrue();
		result.IsSuccess.ShouldBeTrue();
	}

	[Fact]
	public async Task ReturnToPool_EvenWhenNextDelegateThrows()
	{
		// Arrange
		var middleware = new TestablePooledMiddleware();
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "test-msg-1" };

		DispatchRequestDelegate next = (msg, ctx, ct) =>
			throw new InvalidOperationException("handler error");

		// Act & Assert - The base class wraps in InvalidOperationException
		try
		{
			_ = await middleware.InvokeAsync(message, context, next, CancellationToken.None);
		}
		catch (InvalidOperationException)
		{
			// Expected
		}

		// Assert - Should still have been returned (OnReturn called in finally)
		middleware.OnRentCalled.ShouldBeTrue();
		middleware.OnReturnCalled.ShouldBeTrue();
		middleware.IsInUsePublic.ShouldBeFalse();
	}

	[Fact]
	public async Task ReturnSuccessResult_FromNextDelegate()
	{
		// Arrange
		var middleware = new TestablePooledMiddleware();
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "test-msg-1" };
		var expectedResult = MessageResult.Failed("expected error");

		DispatchRequestDelegate next = (msg, ctx, ct) =>
			new ValueTask<IMessageResult>(expectedResult);

		// Act
		var result = await middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		result.ShouldBeSameAs(expectedResult);
	}

	#endregion

	#region InvokeAsync Parameter Validation Tests

	[Fact]
	public async Task ThrowArgumentNullException_WhenMessageIsNull()
	{
		// Arrange
		var middleware = new TestablePooledMiddleware();
		var context = new FakeMessageContext { MessageId = "test-msg-1" };

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(
			middleware.InvokeAsync(null!, context, CreateSuccessDelegate(), CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task ThrowArgumentNullException_WhenContextIsNull()
	{
		// Arrange
		var middleware = new TestablePooledMiddleware();
		var message = new FakeDispatchMessage();

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(
			middleware.InvokeAsync(message, null!, CreateSuccessDelegate(), CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task ThrowArgumentNullException_WhenNextDelegateIsNull()
	{
		// Arrange
		var middleware = new TestablePooledMiddleware();
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "test-msg-1" };

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(
			middleware.InvokeAsync(message, context, null!, CancellationToken.None).AsTask());
	}

	#endregion

	#region IPooledObject Interface Tests

	[Fact]
	public void ImplementIPooledObject()
	{
		// Arrange
		var middleware = new TestablePooledMiddleware();

		// Assert
		middleware.ShouldBeAssignableTo<IPooledObject>();
	}

	#endregion

	#region Testable Pooled Middleware Implementation

	/// <summary>
	/// Concrete implementation of <see cref="PooledDispatchMiddleware"/> for testing.
	/// Exposes internal state and lifecycle hooks for verification.
	/// </summary>
	private sealed class TestablePooledMiddleware : PooledDispatchMiddleware
	{
		public bool OnRentCalled { get; private set; }
		public bool OnReturnCalled { get; private set; }
		public bool ResetStateCalled { get; private set; }
		public string? CustomState { get; private set; }

		/// <summary>
		/// Exposes the protected IsInUse property for testing.
		/// </summary>
		public bool IsInUsePublic => IsInUse;

		/// <summary>
		/// Simulates renting the middleware from the pool.
		/// </summary>
		public void SimulateRent() => OnRent();

		/// <summary>
		/// Simulates returning the middleware to the pool.
		/// </summary>
		public void SimulateReturn() => OnReturn();

		/// <summary>
		/// Simulates marking the middleware as unpoolable.
		/// </summary>
		public void SimulateMarkUnpoolable() => MarkAsUnpoolable();

		/// <summary>
		/// Sets custom state data for testing reset behavior.
		/// </summary>
		public void SetCustomState(string state) => CustomState = state;

		protected override void OnRent()
		{
			OnRentCalled = true;
			base.OnRent();
		}

		protected override void OnReturn()
		{
			OnReturnCalled = true;
			base.OnReturn();
		}

		protected override void ResetState()
		{
			ResetStateCalled = true;
			CustomState = null;
		}
	}

	#endregion
}
