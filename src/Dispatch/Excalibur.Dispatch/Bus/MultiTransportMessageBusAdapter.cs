// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR
// AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Frozen;
using System.Globalization;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Transport;

namespace Excalibur.Dispatch.Bus;

/// <summary>
/// Message bus adapter that can route messages across multiple transport adapters.
/// </summary>
public sealed class MultiTransportMessageBusAdapter : IMessageBusAdapter
{
	private const string AdapterQualifierDelimiter = "://";
	private readonly FrozenDictionary<string, IMessageBusAdapter> _adapters;
	private readonly IMessageBusAdapter[] _adapterArray;
	private readonly IMessageBusAdapter? _defaultAdapter;

	/// <summary>
	/// Initializes a new instance of the <see cref="MultiTransportMessageBusAdapter" /> class.
	/// </summary>
	/// <param name="adapters"> The collection of transport adapters. </param>
	/// <param name="defaultAdapter"> The default adapter to use when no specific adapter is specified. </param>
	public MultiTransportMessageBusAdapter(IEnumerable<IMessageBusAdapter> adapters, IMessageBusAdapter? defaultAdapter = null)
	{
		ArgumentNullException.ThrowIfNull(adapters);

		var adapterMap = new Dictionary<string, IMessageBusAdapter>(StringComparer.Ordinal);
		foreach (var adapter in adapters)
		{
			adapterMap[adapter.Name] = adapter;
		}

		_adapters = adapterMap.ToFrozenDictionary(StringComparer.Ordinal);
		_adapterArray = [.. _adapters.Values];
		_defaultAdapter = defaultAdapter ?? (_adapterArray.Length > 0 ? _adapterArray[0] : null);
	}

	/// <inheritdoc />
	public string Name => "MultiTransport";

	/// <inheritdoc />
	public bool SupportsPublishing
	{
		get
		{
			for (var i = 0; i < _adapterArray.Length; i++)
			{
				if (_adapterArray[i].SupportsPublishing)
				{
					return true;
				}
			}

			return false;
		}
	}

	/// <inheritdoc />
	public bool SupportsSubscription
	{
		get
		{
			for (var i = 0; i < _adapterArray.Length; i++)
			{
				if (_adapterArray[i].SupportsSubscription)
				{
					return true;
				}
			}

			return false;
		}
	}

	/// <inheritdoc />
	public bool SupportsTransactions
	{
		get
		{
			for (var i = 0; i < _adapterArray.Length; i++)
			{
				if (_adapterArray[i].SupportsTransactions)
				{
					return true;
				}
			}

			return false;
		}
	}

	/// <inheritdoc />
	public bool IsConnected
	{
		get
		{
			for (var i = 0; i < _adapterArray.Length; i++)
			{
				if (_adapterArray[i].IsConnected)
				{
					return true;
				}
			}

			return false;
		}
	}

	/// <inheritdoc />
	public Task InitializeAsync(IMessageBusOptions options, CancellationToken cancellationToken)
	{
		if (_adapterArray.Length == 0)
		{
			return Task.CompletedTask;
		}

		var tasks = new Task[_adapterArray.Length];
		for (var i = 0; i < _adapterArray.Length; i++)
		{
			tasks[i] = _adapterArray[i].InitializeAsync(options, cancellationToken);
		}

		return Task.WhenAll(tasks);
	}

	/// <inheritdoc />
	public async Task<IMessageResult> PublishAsync(IDispatchMessage message, IMessageContext context,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentNullException.ThrowIfNull(context);

		if (_defaultAdapter == null)
		{
			return MessageResult.Failed(new MessageProblemDetails
			{
				Type = "NoDefaultAdapter",
				Title = "No Default Adapter",
				ErrorCode = 500,
				Status = 500,
				Detail = ErrorMessages.NoDefaultAdapterConfigured,
				Instance = context.MessageId ?? string.Empty,
			});
		}

		return await _defaultAdapter.PublishAsync(message, context, cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task SubscribeAsync(
		string subscriptionName,
		Func<IDispatchMessage, IMessageContext, CancellationToken, Task<IMessageResult>> messageHandler,
		IMessageBusOptions? options,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(subscriptionName);
		ArgumentNullException.ThrowIfNull(messageHandler);

		IMessageBusAdapter? adapter;
		if (TrySplitQualifiedSubscriptionName(subscriptionName, out var adapterName, out var parsedSubscriptionName))
		{
			if (!_adapters.TryGetValue(adapterName, out adapter))
			{
				throw new ArgumentException(
					string.Format(
						CultureInfo.CurrentCulture,
						Resources.MultiTransportMessageBusAdapter_AdapterNotRegistered,
						adapterName),
					nameof(subscriptionName));
			}

			subscriptionName = parsedSubscriptionName;
		}
		else
		{
			adapter = _defaultAdapter;
			if (adapter == null)
			{
				throw new InvalidOperationException(ErrorMessages.NoDefaultAdapterConfigured);
			}
		}

		await adapter.SubscribeAsync(subscriptionName, messageHandler, options, cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task UnsubscribeAsync(string subscriptionName, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(subscriptionName);

		IMessageBusAdapter? adapter;
		if (TrySplitQualifiedSubscriptionName(subscriptionName, out var adapterName, out var parsedSubscriptionName))
		{
			if (!_adapters.TryGetValue(adapterName, out adapter))
			{
				return; // Silently ignore unknown adapters on unsubscribe
			}

			subscriptionName = parsedSubscriptionName;
		}
		else
		{
			adapter = _defaultAdapter;
			if (adapter == null)
			{
				return; // Silently ignore if no default adapter
			}
		}

		await adapter.UnsubscribeAsync(subscriptionName, cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task<HealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken)
	{
		var healthChecks = new Task<HealthCheckResult>[_adapterArray.Length];
		for (var i = 0; i < _adapterArray.Length; i++)
		{
			healthChecks[i] = _adapterArray[i].CheckHealthAsync(cancellationToken);
		}

		var results = await Task.WhenAll(healthChecks).ConfigureAwait(false);
		var allHealthy = true;
		var data = new Dictionary<string, object>(StringComparer.Ordinal);

		for (var i = 0; i < results.Length; i++)
		{
			var adapterName = _adapterArray[i].Name;
			var result = results[i];
			if (!result.IsHealthy)
			{
				allHealthy = false;
			}

			data[$"{adapterName}_healthy"] = result.IsHealthy;
			if (!string.IsNullOrEmpty(result.Description))
			{
				data[$"{adapterName}_description"] = result.Description;
			}
		}

		return new HealthCheckResult(
			allHealthy,
			allHealthy ? "All adapters are healthy" : "One or more adapters are unhealthy",
			data);
	}

	/// <inheritdoc />
	public Task StartAsync(CancellationToken cancellationToken)
	{
		if (_adapterArray.Length == 0)
		{
			return Task.CompletedTask;
		}

		var tasks = new Task[_adapterArray.Length];
		for (var i = 0; i < _adapterArray.Length; i++)
		{
			tasks[i] = _adapterArray[i].StartAsync(cancellationToken);
		}

		return Task.WhenAll(tasks);
	}

	/// <inheritdoc />
	public Task StopAsync(CancellationToken cancellationToken)
	{
		if (_adapterArray.Length == 0)
		{
			return Task.CompletedTask;
		}

		var tasks = new Task[_adapterArray.Length];
		for (var i = 0; i < _adapterArray.Length; i++)
		{
			tasks[i] = _adapterArray[i].StopAsync(cancellationToken);
		}

		return Task.WhenAll(tasks);
	}

	/// <inheritdoc />
	public void Dispose()
	{
		for (var i = 0; i < _adapterArray.Length; i++)
		{
			_adapterArray[i].Dispose();
		}
	}

	private static bool TrySplitQualifiedSubscriptionName(
		string subscriptionName,
		out string adapterName,
		out string parsedSubscriptionName)
	{
		var delimiterIndex = subscriptionName.IndexOf(AdapterQualifierDelimiter, StringComparison.Ordinal);
		if (delimiterIndex < 0)
		{
			adapterName = string.Empty;
			parsedSubscriptionName = subscriptionName;
			return false;
		}

		adapterName = subscriptionName[..delimiterIndex];
		parsedSubscriptionName = subscriptionName[(delimiterIndex + AdapterQualifierDelimiter.Length)..];
		return true;
	}
}
