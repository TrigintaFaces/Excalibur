// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions.Messaging;

namespace Excalibur.Testing;

/// <summary>
/// Provides a fluent Given-When-Then API for testing event-driven choreography sagas.
/// </summary>
/// <typeparam name="TSaga">The type of saga to test. Must implement <see cref="ISaga{TSagaState}"/> and have a parameterless constructor.</typeparam>
/// <typeparam name="TSagaState">The type of saga state. Must inherit from <see cref="SagaState"/>.</typeparam>
/// <remarks>
/// This test fixture is test-framework-agnostic and works with xUnit, NUnit, MSTest, or any other test framework.
/// </remarks>
/// <example>
/// <code>
/// // Basic usage
/// var result = await new SagaTestFixture&lt;OrderSaga, OrderSagaState&gt;()
///     .Given(state => state.OrderId = "123")
///     .When(new PaymentReceived { OrderId = "123" })
///     .ThenAsync();
///
/// result.ThenState(state => state.PaymentReceived == true);
///
/// // Testing from initial event
/// var result = await new SagaTestFixture&lt;OrderSaga, OrderSagaState&gt;()
///     .When(new OrderCreated { OrderId = "123" })
///     .ThenAsync();
///
/// result.ThenState(state => state.Completed == false);
/// </code>
/// </example>
public sealed class SagaTestFixture<TSaga, TSagaState>
	where TSaga : ISaga<TSagaState>, new()
	where TSagaState : SagaState
{
	private readonly TSaga _saga = new();
	private readonly List<Action<TSagaState>> _stateSetupActions = [];
	private readonly List<object> _givenEvents = [];
	private object? _whenEvent;
	private Exception? _caughtException;
	private bool _whenExecuted;

	/// <summary>
	/// Sets up the saga state before event processing by applying a configuration action.
	/// </summary>
	/// <param name="setupState">An action to configure the saga state.</param>
	/// <returns>This fixture for method chaining.</returns>
	/// <remarks>
	/// Multiple calls to Given are cumulative. State setup actions are applied in order before the When event is processed.
	/// </remarks>
	/// <example>
	/// <code>
	/// fixture.Given(state =>
	/// {
	///     state.OrderId = "123";
	///     state.PaymentReceived = true;
	/// });
	/// </code>
	/// </example>
	public SagaTestFixture<TSaga, TSagaState> Given(Action<TSagaState> setupState)
	{
		ArgumentNullException.ThrowIfNull(setupState);
		_stateSetupActions.Add(setupState);
		return this;
	}

	/// <summary>
	/// Sets up the saga by replaying historical events through <see cref="ISaga.HandleAsync"/>.
	/// </summary>
	/// <param name="events">The events to replay onto the saga.</param>
	/// <returns>This fixture for method chaining.</returns>
	/// <remarks>
	/// Events provided via Given are processed via HandleAsync in order, establishing the saga's prior state.
	/// This can be combined with the state setup overload.
	/// </remarks>
	/// <example>
	/// <code>
	/// fixture.Given(
	///     new OrderCreated { OrderId = "123" },
	///     new PaymentReceived { OrderId = "123" }
	/// );
	/// </code>
	/// </example>
	public SagaTestFixture<TSaga, TSagaState> Given(params object[] events)
	{
		_givenEvents.AddRange(events);
		return this;
	}

	/// <summary>
	/// Specifies the event to process as the action under test.
	/// </summary>
	/// <param name="event">The event to feed into the saga's HandleAsync method.</param>
	/// <returns>This fixture for method chaining.</returns>
	/// <example>
	/// <code>
	/// fixture.When(new PaymentReceived { OrderId = "123", Amount = 99.99m });
	/// </code>
	/// </example>
	public SagaTestFixture<TSaga, TSagaState> When(object @event)
	{
		ArgumentNullException.ThrowIfNull(@event);
		_whenEvent = @event;
		return this;
	}

	/// <summary>
	/// Executes the saga test and returns a result object for assertions.
	/// </summary>
	/// <param name="cancellationToken">Optional cancellation token.</param>
	/// <returns>A result object that provides assertion methods.</returns>
	/// <example>
	/// <code>
	/// var result = await fixture.ThenAsync();
	/// result.ThenState(state => state.Completed == true);
	/// </code>
	/// </example>
	public async Task<SagaTestFixtureResult<TSaga, TSagaState>> ThenAsync(CancellationToken cancellationToken = default)
	{
		// Apply state setup actions
		foreach (var action in _stateSetupActions)
		{
			action(_saga.State);
		}

		// Replay given events
		foreach (var evt in _givenEvents)
		{
			await _saga.HandleAsync(evt, cancellationToken).ConfigureAwait(false);
		}

		// Execute the when event
		if (_whenEvent is not null)
		{
			try
			{
				await _saga.HandleAsync(_whenEvent, cancellationToken).ConfigureAwait(false);
				_whenExecuted = true;
			}
			catch (Exception ex)
			{
				_caughtException = ex;
			}
		}

		return new SagaTestFixtureResult<TSaga, TSagaState>(_saga, _caughtException, _whenExecuted);
	}

	/// <summary>
	/// Shortcut to assert that an exception was thrown during event handling.
	/// </summary>
	/// <typeparam name="TException">The type of exception expected.</typeparam>
	/// <param name="cancellationToken">Optional cancellation token.</param>
	/// <returns>A task representing the asynchronous assertion.</returns>
	/// <example>
	/// <code>
	/// await fixture.When(new InvalidEvent())
	///     .ShouldThrowAsync&lt;InvalidOperationException&gt;();
	/// </code>
	/// </example>
	public async Task ShouldThrowAsync<TException>(CancellationToken cancellationToken = default)
		where TException : Exception
	{
		var result = await ThenAsync(cancellationToken).ConfigureAwait(false);
		result.ShouldThrow<TException>();
	}
}
