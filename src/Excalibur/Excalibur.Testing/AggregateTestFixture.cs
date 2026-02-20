// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;

using Excalibur.Domain.Model;

namespace Excalibur.Testing;

/// <summary>
/// Provides a fluent Given-When-Then API for testing event-sourced aggregates.
/// </summary>
/// <typeparam name="TAggregate">The type of aggregate to test. Must implement <see cref="IAggregateRoot"/> and have a parameterless constructor.</typeparam>
/// <remarks>
/// This test fixture is test-framework-agnostic and works with xUnit, NUnit, MSTest, or any other test framework.
/// </remarks>
/// <example>
/// <code>
/// // Basic usage
/// var fixture = new AggregateTestFixture&lt;OrderAggregate&gt;()
///     .Given(new OrderCreated { OrderId = "123", CustomerId = "C1" })
///     .When(order => order.Ship())
///     .Then()
///     .ShouldRaise&lt;OrderShipped&gt;()
///     .StateShould(order => order.Status == OrderStatus.Shipped);
///
/// // Testing exceptions
/// new AggregateTestFixture&lt;OrderAggregate&gt;()
///     .Given(new OrderCreated { OrderId = "123" })
///     .When(order => order.Ship()) // Already shipped
///     .ShouldThrow&lt;InvalidOperationException&gt;();
/// </code>
/// </example>
public sealed class AggregateTestFixture<TAggregate>
	where TAggregate : IAggregateRoot, IAggregateSnapshotSupport, new()
{
	private readonly TAggregate _aggregate = new();
	private readonly List<IDomainEvent> _givenEvents = [];
	private Exception? _caughtException;
	private bool _whenExecuted;

	/// <summary>
	/// Sets up the aggregate with historical events that represent its prior state.
	/// </summary>
	/// <param name="events">The events to replay onto the aggregate.</param>
	/// <returns>This fixture for method chaining.</returns>
	/// <remarks>
	/// Events provided via Given are applied using <see cref="IAggregateRoot.LoadFromHistory"/>
	/// and do not appear in the uncommitted events collection.
	/// </remarks>
	/// <example>
	/// <code>
	/// fixture.Given(
	///     new OrderCreated { OrderId = "123" },
	///     new OrderItemAdded { ProductId = "P1", Quantity = 2 }
	/// );
	/// </code>
	/// </example>
	public AggregateTestFixture<TAggregate> Given(params IDomainEvent[] events)
	{
		_givenEvents.AddRange(events);
		return this;
	}

	/// <summary>
	/// Sets up the aggregate with historical events that represent its prior state.
	/// </summary>
	/// <param name="events">The events to replay onto the aggregate.</param>
	/// <returns>This fixture for method chaining.</returns>
	public AggregateTestFixture<TAggregate> Given(IEnumerable<IDomainEvent> events)
	{
		ArgumentNullException.ThrowIfNull(events);
		_givenEvents.AddRange(events);
		return this;
	}

	/// <summary>
	/// Executes a command on the aggregate.
	/// </summary>
	/// <param name="action">The action to execute on the aggregate (typically a command method).</param>
	/// <returns>This fixture for method chaining.</returns>
	/// <remarks>
	/// The aggregate's historical events (from Given) are applied before executing the action.
	/// Any exception thrown during execution is captured and can be asserted via ShouldThrow.
	/// </remarks>
	/// <example>
	/// <code>
	/// fixture.When(order => order.Ship());
	/// fixture.When(order => order.AddItem("P1", 5));
	/// </code>
	/// </example>
	public AggregateTestFixture<TAggregate> When(Action<TAggregate> action)
	{
		ArgumentNullException.ThrowIfNull(action);

		// Apply given events to establish initial state
		_aggregate.LoadFromHistory(_givenEvents);

		// Clear any events that might be in uncommitted (shouldn't be any, but defensive)
		_aggregate.MarkEventsAsCommitted();

		try
		{
			action(_aggregate);
			_whenExecuted = true;
		}
		catch (Exception ex)
		{
			_caughtException = ex;
		}

		return this;
	}

	/// <summary>
	/// Executes an asynchronous command on the aggregate.
	/// </summary>
	/// <param name="action">The async action to execute on the aggregate (typically an async command handler).</param>
	/// <returns>A task that completes with this fixture for method chaining.</returns>
	/// <remarks>
	/// The aggregate's historical events (from Given) are applied before executing the action.
	/// Any exception thrown during execution is captured and can be asserted via ShouldThrow.
	/// </remarks>
	/// <example>
	/// <code>
	/// await fixture.WhenAsync(async order => await order.ProcessPaymentAsync());
	/// </code>
	/// </example>
	public async Task<AggregateTestFixture<TAggregate>> WhenAsync(Func<TAggregate, Task> action)
	{
		ArgumentNullException.ThrowIfNull(action);

		// Apply given events to establish initial state
		_aggregate.LoadFromHistory(_givenEvents);

		// Clear any events that might be in uncommitted (shouldn't be any, but defensive)
		_aggregate.MarkEventsAsCommitted();

		try
		{
			await action(_aggregate).ConfigureAwait(false);
			_whenExecuted = true;
		}
		catch (Exception ex)
		{
			_caughtException = ex;
		}

		return this;
	}

	/// <summary>
	/// Begins the assertion phase of the test.
	/// </summary>
	/// <returns>A result object that provides assertion methods.</returns>
	/// <example>
	/// <code>
	/// fixture.Then()
	///     .ShouldRaise&lt;OrderShipped&gt;()
	///     .StateShould(o => o.Status == OrderStatus.Shipped);
	/// </code>
	/// </example>
	public AggregateTestFixtureResult<TAggregate> Then() =>
		new(_aggregate, _caughtException, _whenExecuted);

	/// <summary>
	/// Shortcut to assert that an exception was thrown during command execution.
	/// </summary>
	/// <typeparam name="TException">The type of exception expected.</typeparam>
	/// <remarks>
	/// This is equivalent to calling <c>.Then().ShouldThrow&lt;TException&gt;()</c>.
	/// </remarks>
	/// <example>
	/// <code>
	/// fixture.When(order => order.Ship())
	///     .ShouldThrow&lt;InvalidOperationException&gt;();
	/// </code>
	/// </example>
	public void ShouldThrow<TException>() where TException : Exception =>
		Then().ShouldThrow<TException>();

	/// <summary>
	/// Shortcut to assert that an exception with a specific message was thrown.
	/// </summary>
	/// <typeparam name="TException">The type of exception expected.</typeparam>
	/// <param name="messageContains">A substring that the exception message should contain.</param>
	public void ShouldThrow<TException>(string messageContains) where TException : Exception =>
		Then().ShouldThrow<TException>(messageContains);
}
