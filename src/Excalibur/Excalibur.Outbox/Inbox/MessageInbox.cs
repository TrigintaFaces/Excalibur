// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;
using System.Text;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Options.Delivery;

using Excalibur.Outbox.Diagnostics;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Delivery;

/// <summary>
/// Default implementation of <see cref="IInbox" /> that coordinates storage, serialization, and processing of inbox messages. Wraps
/// <see cref="IInboxStore" /> persistence and <see cref="IInboxProcessor" /> execution to provide a cohesive API for the inbox pattern.
/// </summary>
public sealed partial class MessageInbox : IInbox
{
	private static readonly Encoding PayloadEncoding = Encoding.UTF8;

	private readonly IInboxStore _inboxStore;
	private readonly IInboxProcessor _processor;
	private readonly IJsonSerializer _serializer;
	private readonly IPayloadSerializer? _payloadSerializer;
	private readonly ILogger<MessageInbox> _logger;
	private int _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="MessageInbox" /> class.
	/// </summary>
	/// <param name="inboxStore"> Persistent inbox storage implementation. </param>
	/// <param name="processor"> Processor responsible for executing inbox work items. </param>
	/// <param name="serializer"> Serializer used to persist message payloads. </param>
	/// <param name="options"> Inbox configuration options. </param>
	/// <param name="logger"> Structured logger for diagnostics. </param>
	public MessageInbox(
		IInboxStore inboxStore,
		IInboxProcessor processor,
		IJsonSerializer serializer,
		IOptions<InboxOptions> options,
		ILogger<MessageInbox> logger)
		: this(inboxStore, processor, serializer, payloadSerializer: null, options, logger)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="MessageInbox" /> class with pluggable serialization.
	/// </summary>
	/// <param name="inboxStore"> Persistent inbox storage implementation. </param>
	/// <param name="processor"> Processor responsible for executing inbox work items. </param>
	/// <param name="serializer"> Serializer used to persist message payloads (fallback). </param>
	/// <param name="payloadSerializer"> Optional pluggable serializer for message payloads. </param>
	/// <param name="options"> Inbox configuration options. </param>
	/// <param name="logger"> Structured logger for diagnostics. </param>
	public MessageInbox(
		IInboxStore inboxStore,
		IInboxProcessor processor,
		IJsonSerializer serializer,
		IPayloadSerializer? payloadSerializer,
		IOptions<InboxOptions> options,
		ILogger<MessageInbox> logger)
	{
		ArgumentNullException.ThrowIfNull(inboxStore);
		ArgumentNullException.ThrowIfNull(processor);
		ArgumentNullException.ThrowIfNull(serializer);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_inboxStore = inboxStore;
		_processor = processor;
		_serializer = serializer;
		_payloadSerializer = payloadSerializer;
		_logger = logger;
	}

	/// <inheritdoc />
	public async Task<int> RunInboxDispatchAsync(string dispatcherId, CancellationToken cancellationToken)
	{
		ThrowIfDisposed();
		ArgumentException.ThrowIfNullOrWhiteSpace(dispatcherId);

		LogStartingInboxDispatch(_logger, dispatcherId);

		_processor.Init(dispatcherId);
		var processed = await _processor.DispatchPendingMessagesAsync(cancellationToken).ConfigureAwait(false);

		LogCompletedInboxDispatch(_logger, dispatcherId, processed);
		return processed;
	}

	/// <inheritdoc />
	public async Task SaveMessageAsync<TMessage>(
		TMessage message,
		string externalId,
		IMessageMetadata metadata,
		CancellationToken cancellationToken)
		where TMessage : IDispatchMessage
	{
		ThrowIfDisposed();
		ArgumentNullException.ThrowIfNull(message);
		ArgumentException.ThrowIfNullOrWhiteSpace(externalId);
		ArgumentNullException.ThrowIfNull(metadata);

		var messageType = message.GetType();
		var payloadBytes = await SerializePayloadAsync(message).ConfigureAwait(false);
		var handlerType = messageType.FullName ?? messageType.Name;
		var messageTypeName = messageType.Name;

		var metadataDictionary = CreateMetadataDictionary(metadata);

		_ = await _inboxStore.CreateEntryAsync(
			externalId,
			handlerType,
			messageTypeName,
			payloadBytes,
			metadataDictionary,
			cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async ValueTask DisposeAsync()
	{
		if (Interlocked.Exchange(ref _disposed, 1) != 0)
		{
			return;
		}

		try
		{
			await _processor.DisposeAsync().ConfigureAwait(false);
		}
		finally
		{
			await DisposeStoreAsync().ConfigureAwait(false);
		}
	}

	[LoggerMessage(OutboxEventId.MessageInboxDispatchStarting, LogLevel.Information, "Starting inbox dispatch for dispatcher {DispatcherId}")]
	private static partial void LogStartingInboxDispatch(ILogger logger, string dispatcherId);

	[LoggerMessage(OutboxEventId.MessageInboxDispatchCompleted, LogLevel.Information, "Completed inbox dispatch for dispatcher {DispatcherId} with {Processed} processed messages")]
	private static partial void LogCompletedInboxDispatch(ILogger logger, string dispatcherId, int processed);

	private static Dictionary<string, object> CreateMetadataDictionary(IMessageMetadata metadata) =>
			new Dictionary<string, object>(StringComparer.Ordinal)
			{
				["CorrelationId"] = metadata.CorrelationId,
				["CausationId"] = metadata.CausationId ?? string.Empty,
				["TraceParent"] = metadata.TraceParent ?? string.Empty,
				["TenantId"] = metadata.TenantId ?? string.Empty,
				["UserId"] = metadata.UserId ?? string.Empty,
				["ContentType"] = metadata.ContentType,
				["SerializerVersion"] = metadata.SerializerVersion,
				["MessageVersion"] = metadata.MessageVersion,
				["ContractVersion"] = metadata.ContractVersion,
			};

	private async ValueTask DisposeStoreAsync()
	{
		if (_inboxStore is IAsyncDisposable asyncDisposable)
		{
			await asyncDisposable.DisposeAsync().ConfigureAwait(false);
			return;
		}

		if (_inboxStore is IDisposable disposable)
		{
			disposable.Dispose();
		}
	}

	private void ThrowIfDisposed()
		=> ObjectDisposedException.ThrowIf(_disposed == 1, this);

	/// <summary>
	/// Serializes a message payload using the configured serializer.
	/// Uses <see cref="IPayloadSerializer"/> when available for pluggable serialization,
	/// otherwise falls back to <see cref="IJsonSerializer"/> for backward compatibility.
	/// </summary>
	/// <typeparam name="T">The message type.</typeparam>
	/// <param name="message">The message to serialize.</param>
	/// <returns>The serialized payload bytes.</returns>
	[UnconditionalSuppressMessage(
			"AOT",
			"IL3050:Using RequiresDynamicCode member in AOT",
			Justification = "Inbox payload serialization relies on runtime generic serializer support.")]
	private async Task<byte[]> SerializePayloadAsync<T>(T message)
	{
		if (_payloadSerializer != null)
		{
			return _payloadSerializer.Serialize(message);
		}

		// Fallback to IJsonSerializer for backward compatibility
		var payloadJson = await _serializer.SerializeAsync(message).ConfigureAwait(false);
		return PayloadEncoding.GetBytes(payloadJson);
	}
}
