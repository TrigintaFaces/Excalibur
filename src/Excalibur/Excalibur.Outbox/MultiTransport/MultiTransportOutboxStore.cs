// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Outbox.Diagnostics;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Outbox.MultiTransport;

/// <summary>
/// Decorating implementation of <see cref="IMultiTransportOutboxStore"/> that adds
/// transport routing on top of an existing <see cref="IOutboxStore"/>.
/// </summary>
/// <remarks>
/// <para>
/// This decorator wraps any <see cref="IOutboxStore"/> and adds multi-transport
/// routing by setting the <see cref="OutboundMessage.TargetTransports"/> and
/// <see cref="OutboundMessage.IsMultiTransport"/> properties before delegating
/// to the inner store.
/// </para>
/// </remarks>
public sealed partial class MultiTransportOutboxStore : IMultiTransportOutboxStore
{
	private readonly IOutboxStore _innerStore;
	private readonly MultiTransportOutboxOptions _options;
	private readonly ILogger<MultiTransportOutboxStore> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="MultiTransportOutboxStore"/> class.
	/// </summary>
	/// <param name="innerStore"> The inner outbox store to delegate to. </param>
	/// <param name="options"> The multi-transport configuration options. </param>
	/// <param name="logger"> The logger instance. </param>
	public MultiTransportOutboxStore(
		IOutboxStore innerStore,
		IOptions<MultiTransportOutboxOptions> options,
		ILogger<MultiTransportOutboxStore> logger)
	{
		ArgumentNullException.ThrowIfNull(innerStore);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_innerStore = innerStore;
		_options = options.Value;
		_logger = logger;
	}

	/// <inheritdoc />
	public async ValueTask PublishToTransportAsync(
		string transportName,
		OutboundMessage message,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(transportName);
		ArgumentNullException.ThrowIfNull(message);

		message.TargetTransports = transportName;
		message.IsMultiTransport = false;

		LogPublishToTransport(message.Id, transportName);

		await _innerStore.StageMessageAsync(message, cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async ValueTask PublishToTransportsAsync(
		IReadOnlyList<string> transportNames,
		OutboundMessage message,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(transportNames);
		ArgumentNullException.ThrowIfNull(message);

		if (transportNames.Count == 0)
		{
			throw new ArgumentException("At least one transport name must be specified.", nameof(transportNames));
		}

		message.TargetTransports = string.Join(",", transportNames);

		foreach (var transportName in transportNames)
		{
			_ = message.AddTransport(transportName);
		}

		// Set after AddTransport calls, which unconditionally set IsMultiTransport = true
		message.IsMultiTransport = transportNames.Count > 1;

		LogPublishToMultipleTransports(message.Id, transportNames.Count);

		await _innerStore.StageMessageAsync(message, cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public IReadOnlyList<string> GetRegisteredTransports()
	{
		var transports = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
		{
			_options.DefaultTransport
		};

		foreach (var binding in _options.TransportBindings.Values)
		{
			_ = transports.Add(binding);
		}

		return transports.ToList().AsReadOnly();
	}

	/// <inheritdoc />
	public ValueTask StageMessageAsync(OutboundMessage message, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);

		// Apply transport binding based on message type
		var transport = ResolveTransport(message.MessageType);
		message.TargetTransports = transport;

		return _innerStore.StageMessageAsync(message, cancellationToken);
	}

	/// <inheritdoc />
	public ValueTask EnqueueAsync(IDispatchMessage message, IMessageContext context, CancellationToken cancellationToken)
	{
		return _innerStore.EnqueueAsync(message, context, cancellationToken);
	}

	/// <inheritdoc />
	public ValueTask<IEnumerable<OutboundMessage>> GetUnsentMessagesAsync(int batchSize, CancellationToken cancellationToken)
	{
		return _innerStore.GetUnsentMessagesAsync(batchSize, cancellationToken);
	}

	/// <inheritdoc />
	public ValueTask MarkSentAsync(string messageId, CancellationToken cancellationToken)
	{
		return _innerStore.MarkSentAsync(messageId, cancellationToken);
	}

	/// <inheritdoc />
	public ValueTask MarkFailedAsync(string messageId, string errorMessage, int retryCount, CancellationToken cancellationToken)
	{
		return _innerStore.MarkFailedAsync(messageId, errorMessage, retryCount, cancellationToken);
	}

	private string ResolveTransport(string messageType)
	{
		// Try exact match first
		if (_options.TransportBindings.TryGetValue(messageType, out var transport))
		{
			return transport;
		}

		// Try wildcard match
		foreach (var binding in _options.TransportBindings)
		{
			if (binding.Key.EndsWith('*') &&
				messageType.StartsWith(binding.Key[..^1], StringComparison.OrdinalIgnoreCase))
			{
				return binding.Value;
			}
		}

		if (_options.RequireExplicitBindings)
		{
			throw new InvalidOperationException(
				$"No transport binding found for message type '{messageType}' and RequireExplicitBindings is enabled.");
		}

		return _options.DefaultTransport;
	}

	[LoggerMessage(OutboxEventId.OutboxMessageStored + 50, LogLevel.Debug,
		"Publishing message {MessageId} to transport {TransportName}")]
	private partial void LogPublishToTransport(string messageId, string transportName);

	[LoggerMessage(OutboxEventId.OutboxMessageStored + 51, LogLevel.Debug,
		"Publishing message {MessageId} to {TransportCount} transports")]
	private partial void LogPublishToMultipleTransports(string messageId, int transportCount);
}
