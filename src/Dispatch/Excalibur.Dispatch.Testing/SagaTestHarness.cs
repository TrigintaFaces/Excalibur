// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Messaging;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Testing;

/// <summary>
/// Test harness for testing sagas in isolation with fake dependencies and an in-memory saga store.
/// Provides methods for sending events to sagas and inspecting state transitions.
/// </summary>
/// <typeparam name="TSaga">The saga type under test. Must implement <see cref="ISaga"/>.</typeparam>
/// <remarks>
/// <para>
/// The saga test harness provides an in-memory saga store and state tracking,
/// enabling isolated saga testing without infrastructure dependencies:
/// </para>
/// <para>
/// Example:
/// <code>
/// await using var harness = new SagaTestHarness&lt;OrderSaga&gt;()
///     .ConfigureServices(s => s.AddSingleton(A.Fake&lt;IPaymentService&gt;()));
///
/// await harness.SendAsync(new OrderPlacedEvent { OrderId = "123" });
/// harness.Saga.IsCompleted.ShouldBeFalse();
///
/// await harness.SendAsync(new PaymentConfirmedEvent { OrderId = "123" });
/// harness.Saga.IsCompleted.ShouldBeTrue();
/// </code>
/// </para>
/// </remarks>
public sealed class SagaTestHarness<TSaga> : IAsyncDisposable
	where TSaga : class, ISaga
{
	private readonly List<Action<IServiceCollection>> _serviceConfigurations = [];
	private readonly List<object> _processedEvents = [];
	private ServiceProvider? _serviceProvider;
	private volatile bool _built;
	private volatile bool _disposed;

	/// <summary>
	/// Configures additional services in the saga test harness DI container.
	/// Must be called before first access to <see cref="Saga"/> or <see cref="SendAsync"/>.
	/// </summary>
	/// <param name="configure">Action to configure services.</param>
	/// <returns>This harness for chaining.</returns>
	/// <exception cref="InvalidOperationException">Thrown if the harness has already been built.</exception>
	public SagaTestHarness<TSaga> ConfigureServices(Action<IServiceCollection> configure)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		if (_built)
		{
			throw new InvalidOperationException(
				"Cannot configure services after the harness has been built. " +
				"Call ConfigureServices before accessing Saga or SendAsync.");
		}

		ArgumentNullException.ThrowIfNull(configure);
		_serviceConfigurations.Add(configure);
		return this;
	}

	/// <summary>
	/// Gets the saga instance under test. Triggers a lazy build on first access.
	/// </summary>
	public TSaga Saga
	{
		get
		{
			EnsureBuilt();
			return _serviceProvider!.GetRequiredService<TSaga>();
		}
	}

	/// <summary>
	/// Gets the service provider for resolving additional services.
	/// </summary>
	public IServiceProvider Services
	{
		get
		{
			EnsureBuilt();
			return _serviceProvider!;
		}
	}

	/// <summary>
	/// Gets the list of events that have been sent to the saga in chronological order.
	/// </summary>
	public IReadOnlyList<object> ProcessedEvents => _processedEvents;

	/// <summary>
	/// Sends an event to the saga for processing.
	/// Records the event and invokes the saga's HandleAsync method.
	/// </summary>
	/// <param name="eventMessage">The event to send to the saga.</param>
	/// <param name="cancellationToken">Optional cancellation token.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	/// <exception cref="InvalidOperationException">Thrown if the saga does not handle this event type.</exception>
	public async Task SendAsync(object eventMessage, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(eventMessage);
		ObjectDisposedException.ThrowIf(_disposed, this);

		var saga = Saga;

		if (!saga.HandlesEvent(eventMessage))
		{
			throw new InvalidOperationException(
				$"Saga {typeof(TSaga).Name} does not handle events of type {eventMessage.GetType().Name}. " +
				$"Check your saga's HandlesEvent implementation.");
		}

		await saga.HandleAsync(eventMessage, cancellationToken).ConfigureAwait(false);
		_processedEvents.Add(eventMessage);
	}

	/// <summary>
	/// Sends an event to the saga without checking HandlesEvent first.
	/// Useful for testing error handling when invalid events are received.
	/// </summary>
	/// <param name="eventMessage">The event to force-send to the saga.</param>
	/// <param name="cancellationToken">Optional cancellation token.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	public async Task ForceSendAsync(object eventMessage, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(eventMessage);
		ObjectDisposedException.ThrowIf(_disposed, this);

		await Saga.HandleAsync(eventMessage, cancellationToken).ConfigureAwait(false);
		_processedEvents.Add(eventMessage);
	}

	/// <summary>
	/// Checks whether the saga handles the specified event type.
	/// </summary>
	/// <param name="eventMessage">The event to check.</param>
	/// <returns><see langword="true"/> if the saga can handle this event; otherwise, <see langword="false"/>.</returns>
	public bool HandlesEvent(object eventMessage)
	{
		ArgumentNullException.ThrowIfNull(eventMessage);
		return Saga.HandlesEvent(eventMessage);
	}

	/// <inheritdoc/>
	public async ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;

		if (_serviceProvider is not null)
		{
			await _serviceProvider.DisposeAsync().ConfigureAwait(false);
			_serviceProvider = null;
		}
	}

	private void EnsureBuilt()
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		if (_built)
		{
			return;
		}

		lock (_serviceConfigurations)
		{
			if (_built)
			{
				return;
			}

			var services = new ServiceCollection();

			// Register NullLoggerFactory as default
			services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
			services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

			// Apply user service configurations
			foreach (var configure in _serviceConfigurations)
			{
				configure(services);
			}

			// Register the saga itself
			services.AddSingleton<TSaga>();

			_serviceProvider = services.BuildServiceProvider();
			_built = true;
		}
	}
}
