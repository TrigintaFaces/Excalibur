// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Tests.Shared.Categories;
using Tests.Shared.Infrastructure;

namespace Tests.Shared;

/// <summary>
/// Base class for unit tests. Provides common utilities for isolated, fast tests with no external dependencies.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public abstract class UnitTestBase : IDisposable
{
	private bool _disposed;

	protected UnitTestBase()
	{
		Services = new ServiceCollection();
		_ = Services.AddLogging();
		ServiceProvider = Services.BuildServiceProvider();
	}

	/// <summary>
	/// Null logger factory instance for tests that don't need logging.
	/// </summary>
	protected static ILoggerFactory NullLoggerFactory => Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance;

	/// <summary>
	/// Service collection for dependency injection in tests.
	/// </summary>
	protected IServiceCollection Services { get; }

	/// <summary>
	/// Service provider built from <see cref="Services"/>.
	/// </summary>
	protected IServiceProvider ServiceProvider { get; private set; }

	public void Dispose()
	{
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}

	/// <summary>
	/// Rebuilds the service provider after modifying <see cref="Services"/>.
	/// </summary>
	protected void BuildServiceProvider()
	{
		ServiceProvider = Services.BuildServiceProvider();
	}

	/// <summary>
	/// Gets a required service from the service provider.
	/// </summary>
	protected T GetRequiredService<T>() where T : notnull
	{
		return ServiceProvider.GetRequiredService<T>();
	}

	/// <summary>
	/// Gets an optional service from the service provider.
	/// </summary>
	protected T? GetService<T>()
	{
		return ServiceProvider.GetService<T>();
	}

	/// <summary>
	/// Polls a condition until it returns <see langword="true"/> or the timeout expires.
	/// Useful for timer-based tests that need to wait for asynchronous callbacks.
	/// Delegates to <see cref="WaitHelpers.WaitUntilAsync(Func{bool}, TimeSpan, TimeSpan?, CancellationToken)"/>.
	/// </summary>
	/// <param name="condition">The condition to poll.</param>
	/// <param name="timeout">Maximum time to wait.</param>
	/// <param name="pollInterval">Time between polls. Defaults to 100ms.</param>
	protected static async Task WaitUntilAsync(
		Func<bool> condition,
		TimeSpan timeout,
		TimeSpan? pollInterval = null)
	{
		_ = await WaitHelpers.WaitUntilAsync(condition, timeout, pollInterval).ConfigureAwait(false);
	}

	/// <summary>
	/// Disposes managed and unmanaged resources.
	/// </summary>
	protected virtual void Dispose(bool disposing)
	{
		if (!_disposed)
		{
			if (disposing)
			{
				if (ServiceProvider is IDisposable disposable)
				{
					disposable.Dispose();
				}
			}
			_disposed = true;
		}
	}
}
