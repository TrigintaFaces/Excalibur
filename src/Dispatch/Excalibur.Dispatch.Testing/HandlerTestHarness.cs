// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Testing;

/// <summary>
/// Test harness for testing individual handlers in isolation with fake dependencies.
/// Provides a fluent API for configuring handler dependencies and executing handler methods.
/// </summary>
/// <typeparam name="THandler">The handler type under test.</typeparam>
/// <remarks>
/// <para>
/// The handler test harness manages a minimal DI container, creates context objects,
/// and tracks results. Use it to test handlers without the full Dispatch pipeline:
/// </para>
/// <para>
/// Example:
/// <code>
/// await using var harness = new HandlerTestHarness&lt;CreateOrderHandler&gt;()
///     .ConfigureServices(s => s.AddSingleton(A.Fake&lt;IOrderRepository&gt;()));
///
/// var result = await harness.HandleAsync(new CreateOrderCommand { ... });
/// result.ShouldNotBeNull();
/// </code>
/// </para>
/// </remarks>
public sealed class HandlerTestHarness<THandler> : IAsyncDisposable
	where THandler : class
{
	private readonly List<Action<IServiceCollection>> _serviceConfigurations = [];
	private ServiceProvider? _serviceProvider;
	private volatile bool _built;
	private volatile bool _disposed;

	/// <summary>
	/// Configures additional services in the handler test harness DI container.
	/// Must be called before first HandleAsync invocation.
	/// </summary>
	/// <param name="configure">Action to configure services.</param>
	/// <returns>This harness for chaining.</returns>
	/// <exception cref="InvalidOperationException">Thrown if the harness has already been built.</exception>
	public HandlerTestHarness<THandler> ConfigureServices(Action<IServiceCollection> configure)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		if (_built)
		{
			throw new InvalidOperationException(
				"Cannot configure services after the harness has been built. " +
				"Call ConfigureServices before HandleAsync.");
		}

		ArgumentNullException.ThrowIfNull(configure);
		_serviceConfigurations.Add(configure);
		return this;
	}

	/// <summary>
	/// Gets the handler instance. Triggers a lazy build of the service provider on first access.
	/// </summary>
	public THandler Handler
	{
		get
		{
			EnsureBuilt();
			return _serviceProvider!.GetRequiredService<THandler>();
		}
	}

	/// <summary>
	/// Gets the service provider for resolving additional services.
	/// Triggers a lazy build on first access.
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
	/// Executes the handler's HandleAsync method for a void action handler.
	/// </summary>
	/// <typeparam name="TAction">The action type.</typeparam>
	/// <param name="action">The action to handle.</param>
	/// <param name="cancellationToken">Optional cancellation token.</param>
	/// <returns>A task representing the handler execution.</returns>
	public async Task HandleAsync<TAction>(TAction action, CancellationToken cancellationToken = default)
		where TAction : IDispatchAction
	{
		ArgumentNullException.ThrowIfNull(action);

		if (Handler is IActionHandler<TAction> handler)
		{
			await handler.HandleAsync(action, cancellationToken).ConfigureAwait(false);
			return;
		}

		throw new InvalidOperationException(
			$"Handler {typeof(THandler).Name} does not implement IActionHandler<{typeof(TAction).Name}>.");
	}

	/// <summary>
	/// Executes the handler's HandleAsync method for a result-returning action handler.
	/// </summary>
	/// <typeparam name="TAction">The action type.</typeparam>
	/// <typeparam name="TResult">The result type.</typeparam>
	/// <param name="action">The action to handle.</param>
	/// <param name="cancellationToken">Optional cancellation token.</param>
	/// <returns>The handler's result.</returns>
	public async Task<TResult> HandleAsync<TAction, TResult>(TAction action, CancellationToken cancellationToken = default)
		where TAction : IDispatchAction<TResult>
	{
		ArgumentNullException.ThrowIfNull(action);

		if (Handler is IActionHandler<TAction, TResult> handler)
		{
			return await handler.HandleAsync(action, cancellationToken).ConfigureAwait(false);
		}

		throw new InvalidOperationException(
			$"Handler {typeof(THandler).Name} does not implement IActionHandler<{typeof(TAction).Name}, {typeof(TResult).Name}>.");
	}

	/// <summary>
	/// Creates a new <see cref="MessageContextBuilder"/> pre-configured with the harness service provider.
	/// </summary>
	/// <returns>A configured message context builder.</returns>
	public MessageContextBuilder CreateContextBuilder()
	{
		EnsureBuilt();
		return new MessageContextBuilder()
			.WithRequestServices(_serviceProvider!);
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

			// Register the handler itself if not already registered
			services.AddTransient<THandler>();

			_serviceProvider = services.BuildServiceProvider();
			_built = true;
		}
	}
}
