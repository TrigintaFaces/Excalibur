// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions.Messaging;

namespace Excalibur.Testing;

/// <summary>
/// Represents the result of a saga test fixture execution, providing assertion methods.
/// </summary>
/// <typeparam name="TSaga">The type of saga being tested.</typeparam>
/// <typeparam name="TSagaState">The type of saga state.</typeparam>
public sealed class SagaTestFixtureResult<TSaga, TSagaState>
	where TSaga : ISaga<TSagaState>
	where TSagaState : SagaState
{
	private readonly TSaga _saga;
	private readonly Exception? _exception;
	private readonly bool _executed;

	/// <summary>
	/// Initializes a new instance of the <see cref="SagaTestFixtureResult{TSaga, TSagaState}"/> class.
	/// </summary>
	/// <param name="saga">The saga instance after executing the event.</param>
	/// <param name="exception">Any exception that was thrown during event handling.</param>
	/// <param name="executed">Whether the event was handled successfully.</param>
	internal SagaTestFixtureResult(TSaga saga, Exception? exception, bool executed)
	{
		_saga = saga;
		_exception = exception;
		_executed = executed;
	}

	/// <summary>
	/// Asserts that the saga state matches a predicate.
	/// </summary>
	/// <param name="predicate">A predicate that the saga state must match.</param>
	/// <param name="message">Optional custom message for the assertion failure.</param>
	/// <returns>This instance for method chaining.</returns>
	/// <exception cref="TestFixtureAssertionException">Thrown when the predicate returns false.</exception>
	/// <example>
	/// <code>
	/// result.ThenState(state => state.PaymentReceived == true);
	/// result.ThenState(state => state.Completed, "Expected saga to be completed");
	/// </code>
	/// </example>
	public SagaTestFixtureResult<TSaga, TSagaState> ThenState(Func<TSagaState, bool> predicate, string? message = null)
	{
		ArgumentNullException.ThrowIfNull(predicate);

		if (!predicate(_saga.State))
		{
			throw new TestFixtureAssertionException(
				message ?? "Saga state did not match the expected predicate.");
		}

		return this;
	}

	/// <summary>
	/// Provides direct access to the saga state for custom assertions.
	/// </summary>
	/// <param name="assertion">An action to perform assertions on the saga state.</param>
	/// <returns>This instance for method chaining.</returns>
	/// <example>
	/// <code>
	/// result.AssertState(state =>
	/// {
	///     Assert.Equal("123", state.OrderId);
	///     Assert.True(state.PaymentReceived);
	/// });
	/// </code>
	/// </example>
	public SagaTestFixtureResult<TSaga, TSagaState> AssertState(Action<TSagaState> assertion)
	{
		ArgumentNullException.ThrowIfNull(assertion);
		assertion(_saga.State);
		return this;
	}

	/// <summary>
	/// Asserts that the saga has been marked as completed.
	/// </summary>
	/// <returns>This instance for method chaining.</returns>
	/// <exception cref="TestFixtureAssertionException">Thrown when the saga is not completed.</exception>
	public SagaTestFixtureResult<TSaga, TSagaState> ShouldBeCompleted()
	{
		if (!_saga.IsCompleted)
		{
			throw new TestFixtureAssertionException(
				"Expected saga to be completed, but IsCompleted is false.");
		}

		return this;
	}

	/// <summary>
	/// Asserts that the saga has not been marked as completed.
	/// </summary>
	/// <returns>This instance for method chaining.</returns>
	/// <exception cref="TestFixtureAssertionException">Thrown when the saga is completed.</exception>
	public SagaTestFixtureResult<TSaga, TSagaState> ShouldNotBeCompleted()
	{
		if (_saga.IsCompleted)
		{
			throw new TestFixtureAssertionException(
				"Expected saga to not be completed, but IsCompleted is true.");
		}

		return this;
	}

	/// <summary>
	/// Asserts that the saga handles the specified event type.
	/// </summary>
	/// <param name="eventMessage">The event to check handling for.</param>
	/// <returns>This instance for method chaining.</returns>
	/// <exception cref="TestFixtureAssertionException">Thrown when the saga does not handle the event.</exception>
	public SagaTestFixtureResult<TSaga, TSagaState> ShouldHandleEvent(object eventMessage)
	{
		ArgumentNullException.ThrowIfNull(eventMessage);

		if (!_saga.HandlesEvent(eventMessage))
		{
			throw new TestFixtureAssertionException(
				$"Expected saga to handle event of type {eventMessage.GetType().Name}, but HandlesEvent returned false.");
		}

		return this;
	}

	/// <summary>
	/// Asserts that an exception of the specified type was thrown during event handling.
	/// </summary>
	/// <typeparam name="TException">The type of exception expected.</typeparam>
	/// <exception cref="TestFixtureAssertionException">Thrown when no exception or a different exception type was thrown.</exception>
	/// <example>
	/// <code>
	/// result.ShouldThrow&lt;InvalidOperationException&gt;();
	/// </code>
	/// </example>
	public void ShouldThrow<TException>() where TException : Exception
	{
		if (_exception is null)
		{
			throw new TestFixtureAssertionException(
				$"Expected exception {typeof(TException).Name} to be thrown, but no exception was thrown.");
		}

		if (_exception is not TException)
		{
			throw new TestFixtureAssertionException(
				$"Expected exception {typeof(TException).Name} but got {_exception.GetType().Name}: {_exception.Message}");
		}
	}

	/// <summary>
	/// Asserts that an exception of the specified type with a matching message was thrown.
	/// </summary>
	/// <typeparam name="TException">The type of exception expected.</typeparam>
	/// <param name="messageContains">A substring that the exception message should contain.</param>
	/// <exception cref="TestFixtureAssertionException">Thrown when the exception message doesn't match.</exception>
	public void ShouldThrow<TException>(string messageContains) where TException : Exception
	{
		ShouldThrow<TException>();

		if (!_exception.Message.Contains(messageContains, StringComparison.OrdinalIgnoreCase))
		{
			throw new TestFixtureAssertionException(
				$"Expected exception message to contain '{messageContains}', but was: '{_exception.Message}'");
		}
	}

	/// <summary>
	/// Asserts that no exception was thrown during event handling.
	/// </summary>
	/// <returns>This instance for method chaining.</returns>
	/// <exception cref="TestFixtureAssertionException">Thrown when an exception was thrown.</exception>
	public SagaTestFixtureResult<TSaga, TSagaState> ShouldNotThrow()
	{
		if (_exception is not null)
		{
			throw new TestFixtureAssertionException(
				$"Expected no exception to be thrown, but got {_exception.GetType().Name}: {_exception.Message}");
		}

		return this;
	}
}
