// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Configuration;
using Excalibur.Dispatch.Testing.Tracking;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Testing;

/// <summary>
/// Core test harness for testing Dispatch pipelines, handlers, and message flows.
/// Framework-agnostic — works with xUnit, NUnit, MSTest, or any test runner.
/// </summary>
/// <remarks>
/// <para>
/// The harness uses a lazy-build pattern: configuration methods (<see cref="ConfigureServices"/> and
/// <see cref="ConfigureDispatch"/>) accumulate configuration lambdas. The service provider is built
/// on first access to <see cref="Dispatcher"/> or <see cref="Services"/>.
/// </para>
/// <para>
/// A <see cref="TestTrackingMiddleware"/> is automatically registered at the
/// <see cref="DispatchMiddlewareStage.Start"/> stage to capture all dispatched messages
/// into <see cref="Dispatched"/>.
/// </para>
/// <para>
/// Example usage:
/// <code>
/// await using var harness = new DispatchTestHarness()
///     .ConfigureServices(services => services.AddSingleton&lt;IMyService, MyService&gt;())
///     .ConfigureDispatch(dispatch => dispatch.AddHandlersFromAssembly(typeof(MyHandler).Assembly));
///
/// var result = await harness.Dispatcher.DispatchAsync(new MyCommand(), context, CancellationToken.None);
///
/// harness.Dispatched.Any&lt;MyCommand&gt;().ShouldBeTrue();
/// </code>
/// </para>
/// </remarks>
public sealed class DispatchTestHarness : IAsyncDisposable
{
	private readonly List<Action<IServiceCollection>> _serviceConfigurations = [];
	private readonly List<Action<IDispatchBuilder>> _dispatchConfigurations = [];
	private readonly DispatchedMessageLog _messageLog = new();
	private ServiceProvider? _serviceProvider;
	private volatile bool _built;
	private volatile bool _disposed;

	/// <summary>
	/// Configures additional services in the test harness DI container.
	/// Must be called before first access to <see cref="Dispatcher"/> or <see cref="Services"/>.
	/// </summary>
	/// <param name="configure">Action to configure services.</param>
	/// <returns>This harness for chaining.</returns>
	/// <exception cref="InvalidOperationException">Thrown if the harness has already been built.</exception>
	public DispatchTestHarness ConfigureServices(Action<IServiceCollection> configure)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		if (_built)
		{
			throw new InvalidOperationException(
				"Cannot configure services after the harness has been built. " +
				"Call ConfigureServices before accessing Dispatcher or Services.");
		}

		ArgumentNullException.ThrowIfNull(configure);
		_serviceConfigurations.Add(configure);
		return this;
	}

	/// <summary>
	/// Configures the Dispatch pipeline in the test harness.
	/// Must be called before first access to <see cref="Dispatcher"/> or <see cref="Services"/>.
	/// </summary>
	/// <param name="configure">Action to configure the dispatch builder.</param>
	/// <returns>This harness for chaining.</returns>
	/// <exception cref="InvalidOperationException">Thrown if the harness has already been built.</exception>
	public DispatchTestHarness ConfigureDispatch(Action<IDispatchBuilder> configure)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		if (_built)
		{
			throw new InvalidOperationException(
				"Cannot configure dispatch after the harness has been built. " +
				"Call ConfigureDispatch before accessing Dispatcher or Services.");
		}

		ArgumentNullException.ThrowIfNull(configure);
		_dispatchConfigurations.Add(configure);
		return this;
	}

	/// <summary>
	/// Gets the <see cref="IDispatcher"/> for dispatching messages.
	/// Triggers a lazy build of the service provider on first access.
	/// </summary>
	public IDispatcher Dispatcher
	{
		get
		{
			EnsureBuilt();
			return _serviceProvider.GetRequiredService<IDispatcher>();
		}
	}

	/// <summary>
	/// Gets the <see cref="IServiceProvider"/> for resolving services.
	/// Triggers a lazy build of the service provider on first access.
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
	/// Gets the log of all dispatched messages for test assertions.
	/// </summary>
	public IDispatchedMessageLog Dispatched => _messageLog;

	/// <summary>
	/// Creates a new <see cref="IServiceScope"/> for resolving scoped services.
	/// Each scope provides isolated scoped service instances, which is useful for testing
	/// services registered with <see cref="ServiceLifetime.Scoped"/> lifetime.
	/// </summary>
	/// <returns>A new service scope. The caller is responsible for disposing the scope.</returns>
	/// <remarks>
	/// <para>
	/// Use scopes to verify that scoped services are properly isolated between operations.
	/// Each scope gets its own instances of scoped services, while singleton services are shared.
	/// </para>
	/// <para>
	/// Example:
	/// <code>
	/// await using var harness = new DispatchTestHarness()
	///     .ConfigureServices(s => s.AddScoped&lt;IMyScopedService, MyScopedService&gt;());
	///
	/// using var scope1 = harness.CreateScope();
	/// using var scope2 = harness.CreateScope();
	///
	/// var svc1 = scope1.ServiceProvider.GetRequiredService&lt;IMyScopedService&gt;();
	/// var svc2 = scope2.ServiceProvider.GetRequiredService&lt;IMyScopedService&gt;();
	/// svc1.ShouldNotBeSameAs(svc2); // Different instances per scope
	/// </code>
	/// </para>
	/// </remarks>
	public IServiceScope CreateScope()
	{
		EnsureBuilt();
		return _serviceProvider.CreateScope();
	}

	/// <inheritdoc />
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

			// Register NullLoggerFactory as default (can be overridden by ConfigureServices)
			services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
			services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

			// Register the tracking middleware and its log
			services.AddSingleton(_messageLog);
			services.AddSingleton<TestTrackingMiddleware>();
			services.AddSingleton<IDispatchMiddleware>(sp => sp.GetRequiredService<TestTrackingMiddleware>());

			// Register core Dispatch pipeline
			services.AddDispatch(builder =>
			{
				foreach (var configure in _dispatchConfigurations)
				{
					configure(builder);
				}
			});

			// Apply user service configurations (after AddDispatch so overrides work)
			foreach (var configure in _serviceConfigurations)
			{
				configure(services);
			}

			_serviceProvider = services.BuildServiceProvider();
			_built = true;
		}
	}
}
