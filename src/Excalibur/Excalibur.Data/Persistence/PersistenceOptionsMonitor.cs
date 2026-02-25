// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Data.Abstractions.Persistence;

using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace Excalibur.Data.Persistence;

/// <summary>
/// Implementation of persistence options monitor.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="PersistenceOptionsMonitor{TOptions}" /> class. </remarks>
/// <param name="configuration"> The persistence configuration. </param>
/// <param name="optionsMonitor"> The options monitor. </param>
internal sealed class PersistenceOptionsMonitor<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] TOptions>(
	IPersistenceConfiguration configuration,
	IOptionsMonitor<TOptions> optionsMonitor) : IPersistenceOptionsMonitor<TOptions>, IDisposable
	where TOptions : class, IPersistenceOptions
{
	private readonly IOptionsMonitor<TOptions> _optionsMonitor = optionsMonitor ?? throw new ArgumentNullException(nameof(optionsMonitor));
	private readonly List<IDisposable> _changeSubscriptions = [];
	private readonly Dictionary<string, List<Action<TOptions, string>>> _providerListeners = new(StringComparer.Ordinal);
	private readonly Dictionary<string, DateTimeOffset> _lastChangeTime = new(StringComparer.Ordinal);
#if NET9_0_OR_GREATER

	private readonly Lock _lock = new();

#else

	private readonly object _lock = new();

#endif

	/// <inheritdoc />
	public TOptions CurrentValue => _optionsMonitor.CurrentValue;

	/// <inheritdoc />
	public static IChangeToken GetChangeToken() =>

		// For now, return a token that never changes In a real implementation, this would track configuration changes
		new CancellationChangeToken(CancellationToken.None);

	/// <inheritdoc />
	public TOptions Get(string? name) => _optionsMonitor.Get(name);

	/// <inheritdoc />
	public TOptions GetProviderOptions(string providerName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(providerName);

		// Return the current options with provider name
		return _optionsMonitor.Get(providerName);
	}

	/// <inheritdoc />
	public IDisposable OnChange(Action<TOptions, string?> listener)
	{
		ArgumentNullException.ThrowIfNull(listener);

		var subscription = _optionsMonitor.OnChange(listener);

		lock (_lock)
		{
			if (subscription != null)
			{
				_changeSubscriptions.Add(subscription);
			}
		}

		return new ChangeSubscription(() =>
		{
			lock (_lock)
			{
				if (subscription != null)
				{
					_ = _changeSubscriptions.Remove(subscription);
					subscription.Dispose();
				}
			}
		});
	}

	/// <inheritdoc />
	public IDisposable OnProviderChange(string providerName, Action<TOptions, string> listener)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(providerName);
		ArgumentNullException.ThrowIfNull(listener);

		lock (_lock)
		{
			if (!_providerListeners.TryGetValue(providerName, out var value))
			{
				value = [];
				_providerListeners[providerName] = value;
			}

			value.Add(listener);
		}

		// Track change time
		lock (_lock)
		{
			_lastChangeTime[providerName] = DateTimeOffset.UtcNow;
		}

		return new ChangeSubscription(() =>
		{
			lock (_lock)
			{
				if (_providerListeners.TryGetValue(providerName, out var listeners))
				{
					_ = listeners.Remove(listener);

					if (listeners.Count == 0)
					{
						_ = _providerListeners.Remove(providerName);
					}
				}
			}
		});
	}

	/// <inheritdoc />
	public IEnumerable<string> ValidateOptions(TOptions options)
	{
		var errors = new List<string>();

		if (options == null)
		{
			errors.Add("Options cannot be null");
			return errors;
		}

		// Validate based on the options type
		try
		{
			options.Validate();
		}
		catch (Exception ex)
		{
			errors.Add($"Validation failed: {ex.Message}");
		}

		return errors;
	}

	/// <inheritdoc />
	public void ForceReload(string? providerName = null)
	{
		// Trigger change notifications for the specified provider or all providers
		if (providerName != null)
		{
			lock (_lock)
			{
				_lastChangeTime[providerName] = DateTimeOffset.UtcNow;
				if (_providerListeners.TryGetValue(providerName, out var listeners))
				{
					var options = GetProviderOptions(providerName);
					foreach (var listener in listeners.ToList())
					{
						listener?.Invoke(options, providerName);
					}
				}
			}
		}
		else
		{
			// Reload all providers
			lock (_lock)
			{
				foreach (var kvp in _providerListeners.ToList())
				{
					_lastChangeTime[kvp.Key] = DateTimeOffset.UtcNow;
					var options = GetProviderOptions(kvp.Key);
					foreach (var listener in kvp.Value.ToList())
					{
						listener?.Invoke(options, kvp.Key);
					}
				}
			}
		}
	}

	/// <inheritdoc />
	public DateTimeOffset? GetLastChangeTime(string providerName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(providerName);

		lock (_lock)
		{
			return _lastChangeTime.TryGetValue(providerName, out var time) ? time : null;
		}
	}

	/// <inheritdoc />
	public void Dispose()
	{
		lock (_lock)
		{
			foreach (var subscription in _changeSubscriptions)
			{
				subscription?.Dispose();
			}

			_changeSubscriptions.Clear();
			_providerListeners.Clear();
		}
	}

	/// <summary>
	/// Represents a change subscription.
	/// </summary>
	private sealed class ChangeSubscription(Action dispose) : IDisposable
	{
		public void Dispose() => dispose?.Invoke();
	}
}
