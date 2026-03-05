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
#if NET9_0_OR_GREATER
	private readonly Lock _writeSync = new();
#else
	private readonly object _writeSync = new();
#endif
	private volatile ITransportBinding[] _orderedBindingsSnapshot = [];
	private volatile bool _disposed;

	/// <summary>
	/// Registers a transport binding.
	/// </summary>
	/// <exception cref="InvalidOperationException"></exception>
	public void RegisterBinding(ITransportBinding binding)
	{
		ArgumentNullException.ThrowIfNull(binding);

		lock (_writeSync)
		{
			if (!_bindings.TryAdd(binding.Name, binding))
			{
				throw new InvalidOperationException(
					$"A binding with name '{binding.Name}' is already registered");
			}

			// Keep list sorted by descending priority without re-sorting the full list each time.
			var insertionIndex = FindInsertionIndex(binding.Priority);
			_orderedBindings.Insert(insertionIndex, binding);
			_orderedBindingsSnapshot = _orderedBindings.ToArray();
		}
	}

	private int FindInsertionIndex(int priority)
	{
		var low = 0;
		var high = _orderedBindings.Count;

		// Descending priority order.
		while (low < high)
		{
			var mid = low + ((high - low) >> 1);
			if (_orderedBindings[mid].Priority < priority)
			{
				high = mid;
			}
			else
			{
				low = mid + 1;
			}
		}

		return low;
	}

	/// <summary>
	/// Finds the best binding for an endpoint.
	/// </summary>
	public ITransportBinding? FindBinding(string endpoint)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);

		var snapshot = _orderedBindingsSnapshot;

		// Find first matching binding (ordered by priority)
		for (var i = 0; i < snapshot.Length; i++)
		{
			var binding = snapshot[i];
			if (binding.Matches(endpoint))
			{
				return binding;
			}
		}

		return null;
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
		return _orderedBindingsSnapshot;
	}

	/// <summary>
	/// Removes a binding by name.
	/// </summary>
	public bool RemoveBinding(string name)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(name);

		lock (_writeSync)
		{
			if (_bindings.TryRemove(name, out var binding))
			{
				_ = _orderedBindings.Remove(binding);
				_orderedBindingsSnapshot = _orderedBindings.ToArray();
				return true;
			}

			return false;
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

		_disposed = true;
	}
}
