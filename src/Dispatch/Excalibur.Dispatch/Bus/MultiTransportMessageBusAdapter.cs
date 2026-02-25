// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR
// AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Globalization;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Transport;

namespace Excalibur.Dispatch.Bus;

/// <summary>
/// Message bus adapter that can route messages across multiple transport adapters.
/// </summary>
public sealed class MultiTransportMessageBusAdapter : IMessageBusAdapter
{
	private readonly ConcurrentDictionary<string, IMessageBusAdapter> _adapters = new(StringComparer.Ordinal);
	private readonly IMessageBusAdapter? _defaultAdapter;

	/// <summary>
	/// Initializes a new instance of the <see cref="MultiTransportMessageBusAdapter" /> class.
	/// </summary>
	/// <param name="adapters"> The collection of transport adapters. </param>
	/// <param name="defaultAdapter"> The default adapter to use when no specific adapter is specified. </param>
	public MultiTransportMessageBusAdapter(IEnumerable<IMessageBusAdapter> adapters, IMessageBusAdapter? defaultAdapter = null)
	{
		ArgumentNullException.ThrowIfNull(adapters);

		foreach (var adapter in adapters)
		{
			_adapters[adapter.Name] = adapter;
		}

		_defaultAdapter = defaultAdapter ?? _adapters.Values.FirstOrDefault();
	}

	/// <inheritdoc />
	public string Name => "MultiTransport";

	/// <inheritdoc />
	public bool SupportsPublishing => _adapters.Values.Any(static a => a.SupportsPublishing);

	/// <inheritdoc />
	public bool SupportsSubscription => _adapters.Values.Any(static a => a.SupportsSubscription);

	/// <inheritdoc />
	public bool SupportsTransactions => _adapters.Values.Any(static a => a.SupportsTransactions);

	/// <inheritdoc />
	public bool IsConnected => _adapters.Values.Any(static a => a.IsConnected);

	/// <inheritdoc />
	public async Task InitializeAsync(IMessageBusOptions options, CancellationToken cancellationToken)
	{
		var tasks = _adapters.Values.Select(a => a.InitializeAsync(options, cancellationToken));
		await Task.WhenAll(tasks).ConfigureAwait(false);
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

		// Parse subscription name to determine which adapter to use
		// Format: "adapter://subscription" or just "subscription" for default
		var parts = subscriptionName.Split("://", 2);
		IMessageBusAdapter? adapter;

		if (parts.Length == 2)
		{
			if (!_adapters.TryGetValue(parts[0], out adapter))
			{
				throw new ArgumentException(
					string.Format(
						CultureInfo.CurrentCulture,
						Resources.MultiTransportMessageBusAdapter_AdapterNotRegistered,
						parts[0]),
					nameof(subscriptionName));
			}

			subscriptionName = parts[1];
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

		// Parse subscription name to determine which adapter to use
		var parts = subscriptionName.Split("://", 2);
		IMessageBusAdapter? adapter;

		if (parts.Length == 2)
		{
			if (!_adapters.TryGetValue(parts[0], out adapter))
			{
				return; // Silently ignore unknown adapters on unsubscribe
			}

			subscriptionName = parts[1];
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
		var tasks = _adapters.Values.Select(async a =>
			new { Adapter = a.Name, Result = await a.CheckHealthAsync(cancellationToken).ConfigureAwait(false) });

		var results = await Task.WhenAll(tasks).ConfigureAwait(false);
		var allHealthy = results.All(r => r.Result.IsHealthy);
		var data = new Dictionary<string, object>(StringComparer.Ordinal);

		foreach (var result in results)
		{
			data[$"{result.Adapter}_healthy"] = result.Result.IsHealthy;
			if (!string.IsNullOrEmpty(result.Result.Description))
			{
				data[$"{result.Adapter}_description"] = result.Result.Description;
			}
		}

		return new HealthCheckResult(
			allHealthy,
			allHealthy ? "All adapters are healthy" : "One or more adapters are unhealthy",
			data);
	}

	/// <inheritdoc />
	public async Task StartAsync(CancellationToken cancellationToken)
	{
		var tasks = _adapters.Values.Select(a => a.StartAsync(cancellationToken));
		await Task.WhenAll(tasks).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task StopAsync(CancellationToken cancellationToken)
	{
		var tasks = _adapters.Values.Select(a => a.StopAsync(cancellationToken));
		await Task.WhenAll(tasks).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public void Dispose()
	{
		foreach (var adapter in _adapters.Values)
		{
			adapter.Dispose();
		}

		_adapters.Clear();
	}
}
