// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;

using Excalibur.Dispatch.Abstractions.Transport;

namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Registry for managing transport bindings.
/// </summary>
public sealed class TransportBindingRegistry : IDisposable
{
	private readonly ConcurrentDictionary<string, ITransportBinding> _bindings = new(StringComparer.Ordinal);
	private readonly List<ITransportBinding> _orderedBindings = [];
	private readonly ReaderWriterLockSlim _lock = new();
	private volatile bool _disposed;

	/// <summary>
	/// Registers a transport binding.
	/// </summary>
	/// <exception cref="InvalidOperationException"></exception>
	public void RegisterBinding(ITransportBinding binding)
	{
		ArgumentNullException.ThrowIfNull(binding);

		_lock.EnterWriteLock();
		try
		{
			if (!_bindings.TryAdd(binding.Name, binding))
			{
				throw new InvalidOperationException(
					$"A binding with name '{binding.Name}' is already registered");
			}

			// Reorder bindings by priority
			_orderedBindings.Add(binding);
			_orderedBindings.Sort(static (a, b) => b.Priority.CompareTo(a.Priority));
		}
		finally
		{
			_lock.ExitWriteLock();
		}
	}

	/// <summary>
	/// Finds the best binding for an endpoint.
	/// </summary>
	public ITransportBinding? FindBinding(string endpoint)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);

		_lock.EnterReadLock();
		try
		{
			// Find first matching binding (ordered by priority)
			return _orderedBindings.FirstOrDefault(b => b.Matches(endpoint));
		}
		finally
		{
			_lock.ExitReadLock();
		}
	}

	/// <summary>
	/// Tries to get a binding by name.
	/// </summary>
	/// <param name="name">The binding name.</param>
	/// <param name="binding">The binding if found; otherwise, null.</param>
	/// <returns>True if the binding was found; otherwise, false.</returns>
	public bool TryGetBinding(string name, out ITransportBinding? binding)
	{
		if (string.IsNullOrWhiteSpace(name))
		{
			binding = null;
			return false;
		}

		return _bindings.TryGetValue(name, out binding);
	}

	/// <summary>
	/// Gets all registered bindings.
	/// </summary>
	public IReadOnlyList<ITransportBinding> GetBindings()
	{
		_lock.EnterReadLock();
		try
		{
			return _orderedBindings.ToList();
		}
		finally
		{
			_lock.ExitReadLock();
		}
	}

	/// <summary>
	/// Removes a binding by name.
	/// </summary>
	public bool RemoveBinding(string name)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(name);

		_lock.EnterWriteLock();
		try
		{
			if (_bindings.TryRemove(name, out var binding))
			{
				_ = _orderedBindings.Remove(binding);
				return true;
			}

			return false;
		}
		finally
		{
			_lock.ExitWriteLock();
		}
	}

	/// <summary>
	/// Disposes the registry and releases the lock.
	/// </summary>
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_lock?.Dispose();
		_disposed = true;
	}
}
