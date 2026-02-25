// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Buffers;
using System.Collections.Concurrent;
using System.Text;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Abstractions.Transport;
using Excalibur.Dispatch.CloudEvents;
using Excalibur.Dispatch.Extensions;

using Microsoft.Extensions.Logging;

using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;

using RabbitMqBasicProperties = RabbitMQ.Client.BasicProperties;

namespace Excalibur.Dispatch.Transport.RabbitMQ;

/// <remarks>
/// <para>
/// This message bus uses <see cref="IPayloadSerializer"/> for message body serialization,
/// which prepends a magic byte to identify the serializer format. This enables:
/// </para>
/// <list type="bullet">
///   <item>Automatic format detection during deserialization</item>
///   <item>Seamless migration between serializers</item>
///   <item>Multi-format support within the same exchange</item>
/// </list>
/// <para>
/// Headers remain as JSON strings for maximum interoperability with other systems.
/// </para>
/// <para>
/// See the pluggable serialization architecture documentation.
/// </para>
/// </remarks>
public sealed partial class RabbitMqMessageBus(
	IChannel channel,
	IPayloadSerializer serializer,
	RabbitMqOptions options,
	ILogger<RabbitMqMessageBus> logger,
	IEnvelopeCloudEventBridge? cloudEventBridge = null,
	ICloudEventMapper<(IBasicProperties properties, ReadOnlyMemory<byte> body)>? cloudEventMapper = null,
	RabbitMqCloudEventOptions? cloudEventOptions = null,
	RabbitMqTopologyInitializer? topologyInitializer = null) : IMessageBus
{
	private readonly IChannel _channel = channel ?? throw new ArgumentNullException(nameof(channel));
	private readonly string _exchange = options.Exchange;
	private readonly string _routingKey = options.RoutingKey;
	private readonly RabbitMqCloudEventOptions? _cloudEventOptions = cloudEventOptions;
	private readonly RabbitMqTopologyInitializer? _topologyInitializer = topologyInitializer;

	private readonly PublishConfirmationTracker? _confirmationTracker =
		PublishConfirmationTracker.Create(channel, logger, cloudEventOptions);

	/// <summary>
	/// ///
	/// </summary>
	/// <returns> A <see cref="Task" /> representing the result of the asynchronous operation. </returns>
	public async Task PublishAsync(IDispatchAction action, IMessageContext context, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(action);
		ArgumentNullException.ThrowIfNull(context);

		await EnsureTopologyAsync(cancellationToken).ConfigureAwait(false);

		if (cloudEventBridge is not null && cloudEventMapper is not null)
		{
			await PublishWithCloudEventsAsync(action, context, LogSentAction, cancellationToken).ConfigureAwait(false);
			return;
		}

		// Use SerializeObject with runtime type to ensure proper concrete type serialization
		var payload = serializer.SerializeObject(action, action.GetType());

		var props = new RabbitMqBasicProperties();
		var traceParent = context.GetTraceParent();
		if (!string.IsNullOrEmpty(traceParent))
		{
			props.Headers ??= new Dictionary<string, object?>(StringComparer.Ordinal);
			var traceParentByteCount = Encoding.UTF8.GetByteCount(traceParent);
			var traceParentBytes = ArrayPool<byte>.Shared.Rent(traceParentByteCount);
			var actualTraceParentBytes = Encoding.UTF8.GetBytes(traceParent, traceParentBytes);
			props.Headers["trace-parent"] = traceParentBytes.AsSpan(0, actualTraceParentBytes).ToArray();
		}

		ApplyPersistence(props);
		await PublishBasicAsync(props, payload, cancellationToken).ConfigureAwait(false);

		if (logger.IsEnabled(LogLevel.Information))
		{
			LogSentAction(action.GetType().Name);
		}
	}

	/// <summary>
	/// ///
	/// </summary>
	/// <returns> A <see cref="Task" /> representing the result of the asynchronous operation. </returns>
	public async Task PublishAsync(IDispatchEvent evt, IMessageContext context, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(evt);
		ArgumentNullException.ThrowIfNull(context);

		await EnsureTopologyAsync(cancellationToken).ConfigureAwait(false);

		if (cloudEventBridge is not null && cloudEventMapper is not null)
		{
			await PublishWithCloudEventsAsync(evt, context, LogPublishedEvent, cancellationToken).ConfigureAwait(false);
			return;
		}

		// Use SerializeObject with runtime type to ensure proper concrete type serialization
		var payload = serializer.SerializeObject(evt, evt.GetType());

		var props = new RabbitMqBasicProperties();
		var traceParent = context.GetTraceParent();
		if (!string.IsNullOrEmpty(traceParent))
		{
			props.Headers ??= new Dictionary<string, object?>(StringComparer.Ordinal);
			var traceParentByteCount = Encoding.UTF8.GetByteCount(traceParent);
			var traceParentBytes = ArrayPool<byte>.Shared.Rent(traceParentByteCount);
			var actualTraceParentBytes = Encoding.UTF8.GetBytes(traceParent, traceParentBytes);
			props.Headers["trace-parent"] = traceParentBytes.AsSpan(0, actualTraceParentBytes).ToArray();
		}

		ApplyPersistence(props);
		await PublishBasicAsync(props, payload, cancellationToken).ConfigureAwait(false);

		if (logger.IsEnabled(LogLevel.Information))
		{
			LogPublishedEvent(evt.GetType().Name);
		}
	}

	/// <summary>
	/// ///
	/// </summary>
	/// <returns> A <see cref="Task" /> representing the result of the asynchronous operation. </returns>
	public async Task PublishAsync(IDispatchDocument doc, IMessageContext context, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(doc);
		ArgumentNullException.ThrowIfNull(context);

		await EnsureTopologyAsync(cancellationToken).ConfigureAwait(false);

		if (cloudEventBridge is not null && cloudEventMapper is not null)
		{
			await PublishWithCloudEventsAsync(doc, context, LogSentDocument, cancellationToken).ConfigureAwait(false);
			return;
		}

		// Use SerializeObject with runtime type to ensure proper concrete type serialization
		var payload = serializer.SerializeObject(doc, doc.GetType());

		var props = new RabbitMqBasicProperties();
		var traceParent = context.GetTraceParent();
		if (!string.IsNullOrEmpty(traceParent))
		{
			props.Headers ??= new Dictionary<string, object?>(StringComparer.Ordinal);
			var traceParentByteCount = Encoding.UTF8.GetByteCount(traceParent);
			var traceParentBytes = ArrayPool<byte>.Shared.Rent(traceParentByteCount);
			var actualTraceParentBytes = Encoding.UTF8.GetBytes(traceParent, traceParentBytes);
			props.Headers["trace-parent"] = traceParentBytes.AsSpan(0, actualTraceParentBytes).ToArray();
		}

		ApplyPersistence(props);
		await PublishBasicAsync(props, payload, cancellationToken).ConfigureAwait(false);

		if (logger.IsEnabled(LogLevel.Information))
		{
			LogSentDocument(doc.GetType().Name);
		}
	}

	private static MessageEnvelope CreateEnvelope(IDispatchMessage message, IMessageContext context)
	{
		var envelope = new MessageEnvelope(message)
		{
			MessageId = context.MessageId ?? Guid.NewGuid().ToString(),
			ExternalId = context.ExternalId,
			UserId = context.UserId,
			CorrelationId = context.CorrelationId,
			CausationId = context.CausationId,
			TraceParent = context.TraceParent,
			TenantId = context.TenantId,
			SessionId = context.SessionId,
			WorkflowId = context.WorkflowId,
			PartitionKey = context.PartitionKey,
			Source = context.Source,
			MessageType = context.MessageType ?? message.GetType().FullName,
			ContentType = context.ContentType ?? "application/json",
			DeliveryCount = context.DeliveryCount,
			ReceivedTimestampUtc = context.ReceivedTimestampUtc,
			SentTimestampUtc = context.SentTimestampUtc,
		};

		foreach (var item in context.Items)
		{
			envelope.SetItem(item.Key, item.Value);
		}

		return envelope;
	}

	private async Task PublishWithCloudEventsAsync(
		IDispatchMessage message,
		IMessageContext context,
		Action<string> logAction,
		CancellationToken cancellationToken)
	{
		var envelope = CreateEnvelope(message, context);
		try
		{
			var transportMessage = await cloudEventBridge
				.ToTransportAsync<(IBasicProperties properties, ReadOnlyMemory<byte> body)>(
					envelope,
					cloudEventMapper.Options.DefaultMode,
					cancellationToken)
				.ConfigureAwait(false);

			await PublishTransportMessage(transportMessage, cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			envelope.Dispose();
		}

		if (logger.IsEnabled(LogLevel.Information))
		{
			logAction(message.GetType().Name);
		}
	}

	private async Task PublishTransportMessage((IBasicProperties properties, ReadOnlyMemory<byte> body) transportMessage,
		CancellationToken cancellationToken)
	{
		await EnsureTopologyAsync(cancellationToken).ConfigureAwait(false);

		var (properties, body) = transportMessage;
		// RabbitMQ.Client 7.x requires concrete BasicProperties type for BasicPublishAsync
		if (properties is not RabbitMqBasicProperties rabbitProps)
		{
			throw new InvalidOperationException($"Expected {nameof(RabbitMqBasicProperties)} but got {properties.GetType().Name}");
		}

		await PublishWithConfirmationAsync(rabbitProps, body, cancellationToken).ConfigureAwait(false);
	}

	private void ApplyPersistence(RabbitMqBasicProperties props)
	{
		if (_cloudEventOptions?.Persistence == RabbitMqPersistence.Persistent)
		{
			props.DeliveryMode = DeliveryModes.Persistent;
		}
	}

	private async Task PublishBasicAsync(
		RabbitMqBasicProperties props,
		byte[] payload,
		CancellationToken cancellationToken)
	{
		await PublishWithConfirmationAsync(props, payload, cancellationToken).ConfigureAwait(false);
	}

	private async Task PublishWithConfirmationAsync(
		RabbitMqBasicProperties props,
		ReadOnlyMemory<byte> body,
		CancellationToken cancellationToken)
	{
		if (_confirmationTracker is null)
		{
			try
			{
				await _channel.BasicPublishAsync(
						_exchange,
						_routingKey,
						mandatory: _cloudEventOptions?.MandatoryPublishing ?? false,
						basicProperties: props,
						body: body,
						cancellationToken)
					.ConfigureAwait(false);
			}
			catch (PublishException ex)
			{
				LogPublishFailed(ex);
				throw;
			}

			return;
		}

		try
		{
			await _confirmationTracker
				.PublishAsync(
					props,
					() => _channel.BasicPublishAsync(
						_exchange,
						_routingKey,
						mandatory: _cloudEventOptions?.MandatoryPublishing ?? false,
						basicProperties: props,
						body: body,
						cancellationToken),
					cancellationToken)
				.ConfigureAwait(false);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			LogPublishFailed(ex);
			throw;
		}
	}

	private Task EnsureTopologyAsync(CancellationToken cancellationToken) =>
		_topologyInitializer is null
			? Task.CompletedTask
			: _topologyInitializer.EnsureInitializedAsync(_channel, cancellationToken);

	// Source-generated logging methods
	[LoggerMessage(RabbitMqEventId.ActionSent, LogLevel.Information,
		"Sent action to RabbitMQ: {Action}")]
	private partial void LogSentAction(string action);

	[LoggerMessage(RabbitMqEventId.EventPublished, LogLevel.Information,
		"Published event to RabbitMQ: {Event}")]
	private partial void LogPublishedEvent(string @event);

	[LoggerMessage(RabbitMqEventId.DocumentSent, LogLevel.Information,
		"Sent document to RabbitMQ: {Doc}")]
	private partial void LogSentDocument(string doc);

	[LoggerMessage(RabbitMqEventId.PublishFailed, LogLevel.Error,
		"RabbitMQ publish failed.")]
	private partial void LogPublishFailed(Exception ex);

	private sealed class PublishConfirmationTracker
	{
		private const string PublishIdHeader = "dispatch-publish-id";
		private readonly IChannel _channel;
		private readonly ConcurrentDictionary<ulong, PublishRegistration> _pendingPublishes = new();

		private readonly ConcurrentDictionary<string, ulong> _pendingByPublishId =
			new(StringComparer.Ordinal);

		private readonly TimeSpan _confirmationTimeout;
		private readonly bool _trackReturns;

		private PublishConfirmationTracker(IChannel channel, RabbitMqCloudEventOptions options)
		{
			_channel = channel ?? throw new ArgumentNullException(nameof(channel));
			ArgumentNullException.ThrowIfNull(options);

			_trackReturns = options.MandatoryPublishing || options.Publisher.MandatoryPublishing;

			// Use explicit ConfirmTimeout from Publisher options if set, otherwise fall back to channel timeout or default
			_confirmationTimeout = options.Publisher.ConfirmTimeout > TimeSpan.Zero
				? options.Publisher.ConfirmTimeout
				: channel.ContinuationTimeout > TimeSpan.Zero
					? channel.ContinuationTimeout
					: TimeSpan.FromSeconds(5);

			_channel.BasicAcksAsync += OnBasicAcksAsync;
			_channel.BasicNacksAsync += OnBasicNacksAsync;

			if (_trackReturns)
			{
				_channel.BasicReturnAsync += OnBasicReturnAsync;
			}
		}

		public static PublishConfirmationTracker? Create(
			IChannel channel,
			ILogger<RabbitMqMessageBus> logger,
			RabbitMqCloudEventOptions? options)
		{
			// Check both legacy property and new Publisher options for confirms
			var enableConfirms = options is not null &&
				(options.EnablePublisherConfirms || options.Publisher.EnableConfirms);

			if (!enableConfirms)
			{
				var mandatoryEnabled = options?.MandatoryPublishing == true || options?.Publisher.MandatoryPublishing == true;
				if (mandatoryEnabled)
				{
					logger.LogWarning(
						"RabbitMQ mandatory publishing is enabled but publisher confirms are disabled; publish failures may not surface.");
				}

				return null;
			}

			return new PublishConfirmationTracker(channel, options);
		}

		public async Task PublishAsync(
			RabbitMqBasicProperties properties,
			Func<ValueTask> publishOperation,
			CancellationToken cancellationToken)
		{
			ArgumentNullException.ThrowIfNull(properties);
			ArgumentNullException.ThrowIfNull(publishOperation);

			var registration = await RegisterAsync(properties).ConfigureAwait(false);

			try
			{
				await publishOperation().ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				FailRegistration(registration, ex);
				throw;
			}

			await AwaitConfirmationAsync(registration, cancellationToken).ConfigureAwait(false);
		}

		private static string? TryGetPublishId(IReadOnlyBasicProperties? properties)
		{
			if (properties?.Headers is null ||
				!properties.Headers.TryGetValue(PublishIdHeader, out var value) ||
				value is null)
			{
				return null;
			}

			return value switch
			{
				byte[] bytes => Encoding.UTF8.GetString(bytes),
				ReadOnlyMemory<byte> memory => Encoding.UTF8.GetString(memory.Span),
				string text => text,
				_ => value.ToString(),
			};
		}

		private async ValueTask<PublishRegistration> RegisterAsync(RabbitMqBasicProperties properties)
		{
			var sequenceNumber = await _channel
				.GetNextPublishSequenceNumberAsync()
				.ConfigureAwait(false);
			var publishId = EnsurePublishId(properties);

			var registration = new PublishRegistration(
				sequenceNumber,
				publishId,
				new TaskCompletionSource<PublishOutcome>(TaskCreationOptions.RunContinuationsAsynchronously));

			_pendingPublishes[sequenceNumber] = registration;
			if (!string.IsNullOrWhiteSpace(publishId))
			{
				_pendingByPublishId[publishId] = sequenceNumber;
			}

			return registration;
		}

		private async Task AwaitConfirmationAsync(
			PublishRegistration registration,
			CancellationToken cancellationToken)
		{
			using var cancellationRegistration =
				cancellationToken.Register(() => registration.Completion.TrySetCanceled(cancellationToken));

			PublishOutcome outcome;
			try
			{
				outcome = await registration.Completion.Task
					.TimeoutAfterAsync(_confirmationTimeout)
					.ConfigureAwait(false);
			}
			catch (TimeoutException)
			{
				RemovePending(registration);
				throw new TimeoutException(
					$"RabbitMQ publish confirmation timed out after {_confirmationTimeout.TotalSeconds} seconds.");
			}
			finally
			{
				RemovePending(registration);
			}

			if (!outcome.Success)
			{
				throw new InvalidOperationException(outcome.Reason ?? "RabbitMQ publish failed.");
			}
		}

		private void FailRegistration(PublishRegistration registration, Exception exception)
		{
			RemovePending(registration);
			_ = registration.Completion.TrySetException(exception);
		}

		private Task OnBasicAcksAsync(object sender, BasicAckEventArgs args)
		{
			if (args.Multiple)
			{
				foreach (var sequence in _pendingPublishes.Keys)
				{
					if (sequence <= args.DeliveryTag)
					{
						CompletePublish(sequence, PublishOutcome.Succeeded());
					}
				}
			}
			else
			{
				CompletePublish(args.DeliveryTag, PublishOutcome.Succeeded());
			}

			return Task.CompletedTask;
		}

		private Task OnBasicNacksAsync(object sender, BasicNackEventArgs args)
		{
			var outcome = PublishOutcome.Failure("RabbitMQ publish was nacked by broker.");

			if (args.Multiple)
			{
				foreach (var sequence in _pendingPublishes.Keys)
				{
					if (sequence <= args.DeliveryTag)
					{
						CompletePublish(sequence, outcome);
					}
				}
			}
			else
			{
				CompletePublish(args.DeliveryTag, outcome);
			}

			return Task.CompletedTask;
		}

		private Task OnBasicReturnAsync(object sender, BasicReturnEventArgs args)
		{
			var publishId = TryGetPublishId(args.BasicProperties);
			if (string.IsNullOrWhiteSpace(publishId))
			{
				return Task.CompletedTask;
			}

			if (_pendingByPublishId.TryRemove(publishId, out var sequenceNumber))
			{
				CompletePublish(
					sequenceNumber,
					PublishOutcome.Failure(
						$"RabbitMQ mandatory publish returned. ReplyCode={args.ReplyCode} ReplyText={args.ReplyText} Exchange={args.Exchange} RoutingKey={args.RoutingKey}."));
			}

			return Task.CompletedTask;
		}

		private void CompletePublish(ulong sequenceNumber, PublishOutcome outcome)
		{
			if (_pendingPublishes.TryRemove(sequenceNumber, out var registration))
			{
				RemovePublishId(registration.PublishId);
				_ = registration.Completion.TrySetResult(outcome);
			}
		}

		private string? EnsurePublishId(RabbitMqBasicProperties properties)
		{
			if (!_trackReturns)
			{
				return null;
			}

			properties.Headers ??= new Dictionary<string, object?>(StringComparer.Ordinal);

			if (properties.Headers.TryGetValue(PublishIdHeader, out var existing))
			{
				var existingId = existing switch
				{
					string text => text,
					byte[] bytes => Encoding.UTF8.GetString(bytes),
					ReadOnlyMemory<byte> memory => Encoding.UTF8.GetString(memory.Span),
					_ => null,
				};

				if (!string.IsNullOrWhiteSpace(existingId))
				{
					return existingId;
				}
			}

			var publishId = Guid.NewGuid().ToString("N");
			properties.Headers[PublishIdHeader] = publishId;
			return publishId;
		}

		private void RemovePending(PublishRegistration registration)
		{
			_ = _pendingPublishes.TryRemove(registration.SequenceNumber, out _);
			RemovePublishId(registration.PublishId);
		}

		private void RemovePublishId(string? publishId)
		{
			if (!string.IsNullOrWhiteSpace(publishId))
			{
				_ = _pendingByPublishId.TryRemove(publishId, out _);
			}
		}

		private sealed record PublishRegistration(
			ulong SequenceNumber,
			string? PublishId,
			TaskCompletionSource<PublishOutcome> Completion);

		private sealed record PublishOutcome(bool Success, string? Reason)
		{
			public static PublishOutcome Succeeded() => new(true, null);
			public static PublishOutcome Failure(string reason) => new(false, reason);
		}
	}
}
