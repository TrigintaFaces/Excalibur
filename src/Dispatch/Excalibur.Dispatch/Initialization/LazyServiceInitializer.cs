// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Runtime.CompilerServices;

namespace Excalibur.Dispatch.Initialization;

/// <summary>
/// Provides lazy initialization for expensive services to improve startup time and reduce memory usage when services are not used.
/// </summary>
/// <typeparam name="TService"> The type of service to lazily initialize. </typeparam>
/// <remarks> Initializes a new instance of the <see cref="LazyServiceInitializer{TService}" /> class. </remarks>
/// <param name="factory"> Factory function to create the service. </param>
/// <param name="threadSafetyMode"> Thread safety mode for initialization. </param>
public sealed class LazyServiceInitializer<TService>(
	Func<TService> factory,
	LazyThreadSafetyMode threadSafetyMode = LazyThreadSafetyMode.ExecutionAndPublication)
	where TService : class
{
	private readonly Func<TService> _factory = factory ?? throw new ArgumentNullException(nameof(factory));
#if NET9_0_OR_GREATER

	private readonly Lock _lock = new();

#else

	private readonly object _lock = new();

#endif
	private TService? _instance;
	private volatile bool _initialized;

	/// <summary>
	/// Gets the lazily initialized service instance.
	/// </summary>
	/// <value> The lazily initialized service instance. </value>
	public TService Value
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get
		{
			// Fast path for already initialized
			if (_initialized)
			{
				return _instance!;
			}

			return InitializeValue();
		}
	}

	/// <summary>
	/// Gets a value indicating whether the service has been initialized.
	/// </summary>
	/// <value> The current <see cref="IsValueCreated" /> value. </value>
	public bool IsValueCreated => _initialized;

	/// <summary>
	/// Resets the lazy initializer, forcing re-initialization on next access.
	/// </summary>
	public void Reset()
	{
		lock (_lock)
		{
			if (_instance is IDisposable disposable)
			{
				disposable.Dispose();
			}

			_instance = null;
			_initialized = false;
		}
	}

	/// <summary>
	/// Initializes the service value in a thread-safe manner.
	/// </summary>
	[MethodImpl(MethodImplOptions.NoInlining)]
	private TService InitializeValue()
	{
		if (threadSafetyMode == LazyThreadSafetyMode.None)
		{
			if (!_initialized)
			{
				_instance = _factory();
				_initialized = true;
			}

			return _instance!;
		}

		lock (_lock)
		{
			if (!_initialized)
			{
				_instance = _factory();
				_initialized = true;
			}

			return _instance!;
		}
	}
}
