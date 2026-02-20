// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Buffers;
using System.Collections.Concurrent;
using System.Text;

using Confluent.Kafka;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Abstractions.Transport;
using Excalibur.Dispatch.Extensions;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Transport.Kafka;

/// <summary>
/// Provides Kafka-based message bus implementation for publishing dispatch messages.
/// </summary>
/// <param name="producer"> Kafka producer for sending messages. </param>
/// <param name="serializer"> Payload serializer for message body serialization with pluggable format support. </param>
/// <param name="options"> Kafka configuration options. </param>
/// <param name="logger"> Logger for diagnostics. </param>
/// <param name="cloudEventMapper"> Optional CloudEvents mapper for structured events. </param>
/// <param name="cloudEventOptions"> Optional CloudEvents options for Kafka-specific behavior. </param>
/// <remarks>
/// <para>
/// This message bus uses <see cref="IPayloadSerializer"/> for message body serialization,
/// which prepends a magic byte to identify the serializer format. This enables:
/// </para>
/// <list type="bullet">
///   <item>Automatic format detection during deserialization</item>
///   <item>Seamless migration between serializers</item>
///   <item>Multi-format support within the same topic</item>
/// </list>
/// <para>
/// Headers remain as JSON strings for maximum interoperability with other systems.
/// </para>
/// <para>
/// See the pluggable serialization architecture documentation.
/// </para>
/// </remarks>
public sealed partial class KafkaMessageBus(
		IProducer<string, byte[]> producer,
		IPayloadSerializer serializer,
		KafkaOptions options,
		ILogger<KafkaMessageBus> logger,
		ICloudEventMapper<Message<string, string>>? cloudEventMapper = null,
		KafkaCloudEventOptions? cloudEventOptions = null) : IMessageBus, IAsyncDisposable
{
	private static readonly TimeSpan TransactionTimeout = TimeSpan.FromSeconds(30);

	private readonly IProducer<string, byte[]> _producer =
			producer ?? throw new ArgumentNullException(nameof(producer));
	private readonly IPayloadSerializer _serializer =
			serializer ?? throw new ArgumentNullException(nameof(serializer));
	private readonly KafkaOptions _options =
			options ?? throw new ArgumentNullException(nameof(options));
	private readonly ILogger<KafkaMessageBus> _logger =
			logger ?? throw new ArgumentNullException(nameof(logger));
	private readonly KafkaCloudEventOptions? _cloudEventOptions = cloudEventOptions;
	private readonly ICloudEventMapper<Message<string, string>>? _cloudEventMapper = cloudEventMapper;

	private readonly string _topic = !string.IsNullOrWhiteSpace(options.Topic)
			? options.Topic
			: cloudEventOptions?.DefaultTopic ?? string.Empty;

	private readonly bool _enableTransactions = cloudEventOptions?.EnableTransactions == true;
	private readonly bool _autoCreateTopics = cloudEventOptions?.AutoCreateTopics == true;

	private readonly SemaphoreSlim _transactionLock = new(1, 1);
	private readonly SemaphoreSlim _transactionInitLock = new(1, 1);
	private readonly ConcurrentDictionary<string, Lazy<Task>> _topicInitialization =
			new(StringComparer.Ordinal);
	private bool _transactionsInitialized;

	/// <summary>
	/// ///
	/// </summary>
	/// <returns> A <see cref="Task" /> representing the result of the asynchronous operation. </returns>
	public async Task PublishAsync(
			IDispatchAction action,
			IMessageContext context,
			CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(action);
		ArgumentNullException.ThrowIfNull(context);

		if (_cloudEventMapper is not null && _logger.IsEnabled(LogLevel.Trace))
		{
			LogCloudEventMapperResolved();
		}

		// Use SerializeObject with runtime type to ensure proper concrete type serialization
		var payload = _serializer.SerializeObject(action, action.GetType());
		await PublishInternalAsync(
				payload,
				context,
				action.GetType().Name,
				LogSentAction,
				cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// ///
	/// </summary>
	/// <returns> A <see cref="Task" /> representing the result of the asynchronous operation. </returns>
	public async Task PublishAsync(
			IDispatchEvent evt,
			IMessageContext context,
			CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(evt);
		ArgumentNullException.ThrowIfNull(context);

		// Use SerializeObject with runtime type to ensure proper concrete type serialization
		var payload = _serializer.SerializeObject(evt, evt.GetType());
		await PublishInternalAsync(
				payload,
				context,
				evt.GetType().Name,
				LogPublishedEvent,
				cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// ///
	/// </summary>
	/// <returns> A <see cref="Task" /> representing the result of the asynchronous operation. </returns>
	public async Task PublishAsync(
			IDispatchDocument doc,
			IMessageContext context,
			CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(doc);
		ArgumentNullException.ThrowIfNull(context);

		// Use SerializeObject with runtime type to ensure proper concrete type serialization
		var payload = _serializer.SerializeObject(doc, doc.GetType());
		await PublishInternalAsync(
				payload,
				context,
				doc.GetType().Name,
				LogSentDocument,
				cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public ValueTask DisposeAsync()
	{
		_ = _producer.Flush(TimeSpan.FromSeconds(5));
		_producer.Dispose();
		_transactionLock.Dispose();
		_transactionInitLock.Dispose();
		return ValueTask.CompletedTask;
	}

	private async Task PublishInternalAsync(
			byte[] payload,
			IMessageContext context,
			string messageType,
			Action<string> logAction,
			CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(_topic))
		{
			throw new InvalidOperationException("Kafka topic is not configured.");
		}

		var traceParent = context.GetTraceParent();
		var message = new Message<string, byte[]>
		{
			Key = context.CorrelationId ?? string.Empty,
			Value = payload,
			Headers = [],
		};

		if (!string.IsNullOrEmpty(traceParent))
		{
			var traceParentByteCount = Encoding.UTF8.GetByteCount(traceParent);
			var traceParentBytes = ArrayPool<byte>.Shared.Rent(traceParentByteCount);
			var actualTraceParentBytes = Encoding.UTF8.GetBytes(traceParent, traceParentBytes);
			message.Headers.Add(
					"trace-parent",
					traceParentBytes.AsSpan(0, actualTraceParentBytes).ToArray());
		}

		await EnsureTopicExistsAsync(_topic, cancellationToken).ConfigureAwait(false);

		if (_enableTransactions)
		{
			await PublishTransactionalAsync(_topic, message, cancellationToken)
					.ConfigureAwait(false);
		}
		else
		{
			_ = await _producer
					.ProduceAsync(_topic, message, cancellationToken)
					.ConfigureAwait(false);
		}

		if (_logger.IsEnabled(LogLevel.Information))
		{
			logAction(messageType);
		}
	}

	private Task EnsureTopicExistsAsync(string topic, CancellationToken cancellationToken)
	{
		if (!_autoCreateTopics || _cloudEventOptions is null || string.IsNullOrWhiteSpace(topic))
		{
			return Task.CompletedTask;
		}

		if (cancellationToken.IsCancellationRequested)
		{
			return Task.FromCanceled(cancellationToken);
		}

		var initializer = _topicInitialization.GetOrAdd(
				topic,
				t => new Lazy<Task>(() => KafkaProducerConfigBuilder.EnsureTopicExistsAsync(
						_options,
						_cloudEventOptions,
						t,
						CancellationToken.None)));

		return initializer.Value;
	}

	private async Task PublishTransactionalAsync(
			string topic,
			Message<string, byte[]> message,
			CancellationToken cancellationToken)
	{
		await EnsureTransactionsInitializedAsync(cancellationToken).ConfigureAwait(false);

		await _transactionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
		var transactionStarted = false;
		try
		{
			_producer.BeginTransaction();
			transactionStarted = true;
			LogTransactionBegin(topic);
			_ = await _producer.ProduceAsync(topic, message, cancellationToken)
					.ConfigureAwait(false);
			_producer.CommitTransaction();
			LogTransactionCommitted(topic);
		}
		catch (Exception ex)
		{
			if (ex is not OperationCanceledException)
			{
				LogTransactionPublishFailed(ex);
			}

			if (transactionStarted)
			{
				try
				{
					_producer.AbortTransaction();
					LogTransactionAborted(topic);
				}
				catch (KafkaException abortException)
				{
					LogTransactionAbortFailed(abortException);
				}
			}

			throw;
		}
		finally
		{
			_ = _transactionLock.Release();
		}
	}

	private async Task EnsureTransactionsInitializedAsync(CancellationToken cancellationToken)
	{
		if (!_enableTransactions || _transactionsInitialized)
		{
			return;
		}

		await _transactionInitLock.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			if (_transactionsInitialized)
			{
				return;
			}

			try
			{
				_producer.InitTransactions(TransactionTimeout);
				_transactionsInitialized = true;
				LogTransactionInitialized();
			}
			catch (Exception ex) when (ex is not OperationCanceledException)
			{
				LogTransactionInitializationFailed(ex);
				throw;
			}
		}
		finally
		{
			_ = _transactionInitLock.Release();
		}
	}

	// Source-generated logging methods
	[LoggerMessage(KafkaEventId.ActionSent, LogLevel.Information,
			"Sent action to Kafka: {Action}")]
	private partial void LogSentAction(string action);

	[LoggerMessage(KafkaEventId.EventPublished, LogLevel.Information,
			"Published event to Kafka: {Event}")]
	private partial void LogPublishedEvent(string @event);

	[LoggerMessage(KafkaEventId.DocumentSent, LogLevel.Information,
			"Sent document to Kafka: {Doc}")]
	private partial void LogSentDocument(string doc);

	[LoggerMessage(KafkaEventId.CloudEventMapperResolved, LogLevel.Trace,
			"Kafka CloudEvents mapper resolved for action publish path.")]
	private partial void LogCloudEventMapperResolved();

	[LoggerMessage(KafkaEventId.TransactionInitialized, LogLevel.Debug,
			"Kafka transaction initialization complete.")]
	private partial void LogTransactionInitialized();

	[LoggerMessage(KafkaEventId.TransactionBegin, LogLevel.Debug,
			"Kafka transaction begin for topic {Topic}.")]
	private partial void LogTransactionBegin(string topic);

	[LoggerMessage(KafkaEventId.TransactionCommitted, LogLevel.Debug,
			"Kafka transaction committed for topic {Topic}.")]
	private partial void LogTransactionCommitted(string topic);

	[LoggerMessage(KafkaEventId.TransactionAborted, LogLevel.Warning,
			"Kafka transaction aborted for topic {Topic}.")]
	private partial void LogTransactionAborted(string topic);

	[LoggerMessage(KafkaEventId.TransactionError, LogLevel.Error,
			"Kafka transactional publish failed; aborting transaction.")]
	private partial void LogTransactionPublishFailed(Exception exception);

	[LoggerMessage(KafkaEventId.TransactionAbortFailed, LogLevel.Warning,
			"Kafka transaction abort failed.")]
	private partial void LogTransactionAbortFailed(Exception exception);

	[LoggerMessage(KafkaEventId.TransactionInitializationFailed, LogLevel.Error,
			"Kafka transaction initialization failed.")]
	private partial void LogTransactionInitializationFailed(Exception exception);
}
