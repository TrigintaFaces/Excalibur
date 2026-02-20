// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;

using Excalibur.Domain.Model;

namespace Excalibur.Testing;

/// <summary>
/// Represents the result of an aggregate test fixture execution, providing assertion methods.
/// </summary>
/// <typeparam name="TAggregate">The type of aggregate being tested.</typeparam>
public sealed class AggregateTestFixtureResult<TAggregate>
	where TAggregate : IAggregateRoot
{
	private readonly TAggregate _aggregate;
	private readonly Exception? _exception;
	private readonly bool _executed;

	/// <summary>
	/// Initializes a new instance of the <see cref="AggregateTestFixtureResult{TAggregate}"/> class.
	/// </summary>
	/// <param name="aggregate">The aggregate instance after executing the command.</param>
	/// <param name="exception">Any exception that was thrown during command execution.</param>
	/// <param name="executed">Whether the command was executed successfully.</param>
	internal AggregateTestFixtureResult(TAggregate aggregate, Exception? exception, bool executed)
	{
		_aggregate = aggregate;
		_exception = exception;
		_executed = executed;
	}

	/// <summary>
	/// Asserts that an event of the specified type was raised by the aggregate.
	/// </summary>
	/// <typeparam name="TEvent">The type of event expected to be raised.</typeparam>
	/// <returns>This instance for method chaining.</returns>
	/// <exception cref="TestFixtureAssertionException">Thrown when no event of the specified type was raised.</exception>
	/// <example>
	/// <code>
	/// fixture.Then().ShouldRaise&lt;OrderShipped&gt;();
	/// </code>
	/// </example>
	public AggregateTestFixtureResult<TAggregate> ShouldRaise<TEvent>()
		where TEvent : IDomainEvent
	{
		var events = _aggregate.GetUncommittedEvents();
		if (!events.OfType<TEvent>().Any())
		{
			throw new TestFixtureAssertionException(
				$"Expected event {typeof(TEvent).Name} was not raised. " +
				$"Actual events: [{string.Join(", ", events.Select(e => e.GetType().Name))}]");
		}

		return this;
	}

	/// <summary>
	/// Asserts that an event of the specified type matching a predicate was raised.
	/// </summary>
	/// <typeparam name="TEvent">The type of event expected to be raised.</typeparam>
	/// <param name="predicate">A predicate to match the event properties.</param>
	/// <returns>This instance for method chaining.</returns>
	/// <exception cref="TestFixtureAssertionException">Thrown when no matching event was raised.</exception>
	/// <example>
	/// <code>
	/// fixture.Then().ShouldRaise&lt;OrderShipped&gt;(e => e.OrderId == "123");
	/// </code>
	/// </example>
	public AggregateTestFixtureResult<TAggregate> ShouldRaise<TEvent>(Func<TEvent, bool> predicate)
		where TEvent : IDomainEvent
	{
		ArgumentNullException.ThrowIfNull(predicate);

		var events = _aggregate.GetUncommittedEvents();
		if (!events.OfType<TEvent>().Any(predicate))
		{
			var matchingTypeCount = events.OfType<TEvent>().Count();
			throw new TestFixtureAssertionException(
				$"Expected event {typeof(TEvent).Name} matching predicate was not raised. " +
				$"Found {matchingTypeCount} event(s) of type {typeof(TEvent).Name} but none matched the predicate.");
		}

		return this;
	}

	/// <summary>
	/// Asserts that no events were raised by the aggregate.
	/// </summary>
	/// <returns>This instance for method chaining.</returns>
	/// <exception cref="TestFixtureAssertionException">Thrown when events were raised.</exception>
	public AggregateTestFixtureResult<TAggregate> ShouldRaiseNoEvents()
	{
		var events = _aggregate.GetUncommittedEvents();
		if (events.Count > 0)
		{
			throw new TestFixtureAssertionException(
				$"Expected no events to be raised, but found: [{string.Join(", ", events.Select(e => e.GetType().Name))}]");
		}

		return this;
	}

	/// <summary>
	/// Asserts that the aggregate state matches a predicate.
	/// </summary>
	/// <param name="predicate">A predicate that the aggregate state must match.</param>
	/// <param name="message">Optional custom message for the assertion failure.</param>
	/// <returns>This instance for method chaining.</returns>
	/// <exception cref="TestFixtureAssertionException">Thrown when the predicate returns false.</exception>
	/// <example>
	/// <code>
	/// fixture.Then().StateShould(order => order.Status == OrderStatus.Shipped);
	/// </code>
	/// </example>
	public AggregateTestFixtureResult<TAggregate> StateShould(Func<TAggregate, bool> predicate, string? message = null)
	{
		ArgumentNullException.ThrowIfNull(predicate);

		if (!predicate(_aggregate))
		{
			throw new TestFixtureAssertionException(
				message ?? "Aggregate state did not match the expected predicate.");
		}

		return this;
	}

	/// <summary>
	/// Provides direct access to the aggregate for custom assertions.
	/// </summary>
	/// <param name="assertion">An action to perform assertions on the aggregate.</param>
	/// <returns>This instance for method chaining.</returns>
	/// <example>
	/// <code>
	/// fixture.Then().AssertAggregate(order =>
	/// {
	///     Assert.Equal("123", order.Id);
	///     Assert.Equal(OrderStatus.Shipped, order.Status);
	/// });
	/// </code>
	/// </example>
	public AggregateTestFixtureResult<TAggregate> AssertAggregate(Action<TAggregate> assertion)
	{
		ArgumentNullException.ThrowIfNull(assertion);
		assertion(_aggregate);
		return this;
	}

	/// <summary>
	/// Asserts that an exception of the specified type was thrown during command execution.
	/// </summary>
	/// <typeparam name="TException">The type of exception expected.</typeparam>
	/// <exception cref="TestFixtureAssertionException">Thrown when no exception or a different exception type was thrown.</exception>
	/// <example>
	/// <code>
	/// fixture.Then().ShouldThrow&lt;InvalidOperationException&gt;();
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
	/// Asserts that no exception was thrown during command execution.
	/// </summary>
	/// <returns>This instance for method chaining.</returns>
	/// <exception cref="TestFixtureAssertionException">Thrown when an exception was thrown.</exception>
	public AggregateTestFixtureResult<TAggregate> ShouldNotThrow()
	{
		if (_exception is not null)
		{
			throw new TestFixtureAssertionException(
				$"Expected no exception to be thrown, but got {_exception.GetType().Name}: {_exception.Message}");
		}

		return this;
	}
}
