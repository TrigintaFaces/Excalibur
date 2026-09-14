// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Amazon.EventBridge;
using Amazon.EventBridge.Model;

using Excalibur.Dispatch;
using Excalibur.Dispatch.CloudEvents;
using Excalibur.Dispatch.Features;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Serialization;
using Excalibur.Dispatch.Transport.AwsSqs;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// AWS EventBridge implementation of the message bus for publishing dispatch actions, events, and documents.
/// </summary>
/// <param name="client"> The AWS EventBridge client for publishing events. </param>
/// <param name="serializer"> Payload serializer for message body serialization with pluggable format support. </param>
/// <param name="options"> The EventBridge specific configuration options. </param>
/// <param name="logger"> The logger instance for diagnostic information. </param>
/// <param name="cloudEventBridge"> Optional envelope-to-CloudEvent bridge; when supplied with <paramref name="cloudEventEncoder"/>, every publish is emitted as a CloudEvent instead of the native envelope format. </param>
/// <param name="cloudEventEncoder"> Optional CloudEvents encoder for <see cref="PutEventsRequestEntry"/>. </param>
/// <remarks>
/// <para>
/// This message bus uses <see cref="IPayloadSerializer"/> for message body serialization,
/// which prepends a magic byte to identify the serializer format. This enables:
/// </para>
/// <list type="bullet">
///   <item>Automatic format detection during deserialization</item>
///   <item>Seamless migration between serializers</item>
///   <item>Multi-format support within the same event bus</item>
/// </list>
/// <para>
/// See the pluggable serialization architecture documentation for details.
/// </para>
/// </remarks>
internal sealed partial class AwsEventBridgeMessageBus(
	IAmazonEventBridge client,
	IPayloadSerializer serializer,
	AwsEventBridgeOptions options,
	ILogger<AwsEventBridgeMessageBus> logger,
	IEnvelopeCloudEventBridge? cloudEventBridge = null,
	ICloudEventEncoder<PutEventsRequestEntry>? cloudEventEncoder = null) : IMessageBus, IAsyncDisposable
{
	private readonly string _busName = options.EventBusName;
	private readonly SemaphoreSlim _archiveLock = new(1, 1);
	private int _archiveState;

	public async Task PublishAsync(IDispatchAction action, IMessageContext context, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(action);
		ArgumentNullException.ThrowIfNull(context);

		await EnsureArchiveAsync(cancellationToken).ConfigureAwait(false);

		if (cloudEventBridge is not null && cloudEventEncoder is not null)
		{
			await PublishWithCloudEventsAsync(action, context, LogPublishedAction, cancellationToken).ConfigureAwait(false);
			return;
		}

		// Use SerializeObject with runtime type to ensure proper concrete type serialization
		var payload = serializer.SerializeObject(action, action.GetType());
		var body = Convert.ToBase64String(payload);

		var entry = new PutEventsRequestEntry
		{
			EventBusName = _busName,
			Source = ResolveSource(context),
			DetailType = ResolveDetailType(context, action.GetType().Name),
			Detail = body,
			TraceHeader = context.GetTraceParent(),
		};

		_ = await client.PutEventsAsync(new PutEventsRequest { Entries = [entry] }, cancellationToken).ConfigureAwait(false);

		LogPublishedAction(action.GetType().Name);
	}

	public async Task PublishAsync(IDispatchEvent evt, IMessageContext context, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(evt);
		ArgumentNullException.ThrowIfNull(context);

		await EnsureArchiveAsync(cancellationToken).ConfigureAwait(false);

		if (cloudEventBridge is not null && cloudEventEncoder is not null)
		{
			await PublishWithCloudEventsAsync(evt, context, LogPublishedEvent, cancellationToken).ConfigureAwait(false);
			return;
		}

		// Use SerializeObject with runtime type to ensure proper concrete type serialization
		var payload = serializer.SerializeObject(evt, evt.GetType());
		var body = Convert.ToBase64String(payload);

		var entry = new PutEventsRequestEntry
		{
			EventBusName = _busName,
			Source = ResolveSource(context),
			DetailType = ResolveDetailType(context, evt.GetType().Name),
			Detail = body,
			TraceHeader = context.GetTraceParent(),
		};

		_ = await client.PutEventsAsync(new PutEventsRequest { Entries = [entry] }, cancellationToken).ConfigureAwait(false);

		LogPublishedEvent(evt.GetType().Name);
	}

	public async Task PublishAsync(IDispatchDocument doc, IMessageContext context, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(doc);
		ArgumentNullException.ThrowIfNull(context);

		await EnsureArchiveAsync(cancellationToken).ConfigureAwait(false);

		if (cloudEventBridge is not null && cloudEventEncoder is not null)
		{
			await PublishWithCloudEventsAsync(doc, context, LogSentDocument, cancellationToken).ConfigureAwait(false);
			return;
		}

		// Use SerializeObject with runtime type to ensure proper concrete type serialization
		var payload = serializer.SerializeObject(doc, doc.GetType());
		var body = Convert.ToBase64String(payload);

		var entry = new PutEventsRequestEntry
		{
			EventBusName = _busName,
			Source = ResolveSource(context),
			DetailType = ResolveDetailType(context, doc.GetType().Name),
			Detail = body,
			TraceHeader = context.GetTraceParent(),
		};

		_ = await client.PutEventsAsync(new PutEventsRequest { Entries = [entry] }, cancellationToken).ConfigureAwait(false);

		LogSentDocument(doc.GetType().Name);
	}

	public ValueTask DisposeAsync()
	{
		_archiveLock.Dispose();
		client.Dispose();
		return ValueTask.CompletedTask;
	}

	private static MessageEnvelope CreateEnvelope(IDispatchMessage message, IMessageContext context)
	{
		// The declared name, not the CLR FullName -- mirrors the other transports' CreateEnvelope for
		// the same reason (it becomes the outgoing CloudEvent type attribute).
		var messageClrType = message.GetType();

		var envelope = new MessageEnvelope(message)
		{
			MessageId = context.MessageId ?? Uuid7Extensions.GenerateString(),
			ExternalId = context.GetExternalId(),
			UserId = context.GetUserId(),
			CorrelationId = context.CorrelationId,
			CausationId = context.CausationId,
			TraceParent = context.GetTraceParent(),
			TenantId = context.GetTenantId(),
			MessageType = context.GetMessageType()
				?? MessageNameHelper.GetDeclaredName(messageClrType)
				?? messageClrType.FullName,
			ContentType = context.GetContentType() ?? "application/json",
			DeliveryCount = context.GetDeliveryCount(),
			ReceivedTimestampUtc = context.GetReceivedTimestampUtc() ?? DateTimeOffset.UtcNow,
			SentTimestampUtc = context.GetSentTimestampUtc(),
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
			var entry = await cloudEventBridge!
				.ToTransportAsync<PutEventsRequestEntry>(envelope, cloudEventEncoder!.Options.DefaultMode, cancellationToken)
				.ConfigureAwait(false);

			// The bridge fills the entry from the CloudEvents options, whose EventBusName defaults to the
			// literal "default" -- a different option object from the one the three native publish paths
			// read. Nothing copies one into the other, so without this line a consumer who configured a bus
			// for the native path has every CloudEvents publish land on "default" instead, silently.
			entry.EventBusName = _busName;

			_ = await client.PutEventsAsync(new PutEventsRequest { Entries = [entry] }, cancellationToken).ConfigureAwait(false);
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

	private string ResolveSource(IMessageContext context)
	{
		var source = context.GetSource();
		if (!string.IsNullOrWhiteSpace(source))
		{
			return source;
		}

		return string.IsNullOrWhiteSpace(options.DefaultSource) ? "dispatch" : options.DefaultSource;
	}

	private string ResolveDetailType(IMessageContext context, string fallbackType)
	{
		var messageType = context.GetMessageType();
		if (!string.IsNullOrWhiteSpace(messageType))
		{
			return messageType;
		}

		return string.IsNullOrWhiteSpace(options.DefaultDetailType) ? fallbackType : options.DefaultDetailType;
	}

	private async Task EnsureArchiveAsync(CancellationToken cancellationToken)
	{
		if (!options.EnableArchiving || Volatile.Read(ref _archiveState) != 0)
		{
			return;
		}

		await _archiveLock.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			if (Volatile.Read(ref _archiveState) != 0)
			{
				return;
			}

			var archiveName = string.IsNullOrWhiteSpace(options.ArchiveName)
				? $"{_busName}-dispatch-archive"
				: options.ArchiveName;

			try
			{
				var archive = await client.DescribeArchiveAsync(
						new DescribeArchiveRequest { ArchiveName = archiveName },
						cancellationToken)
					.ConfigureAwait(false);

				if (options.ArchiveRetentionDays > 0 &&
					archive.RetentionDays != options.ArchiveRetentionDays)
				{
					var updateRequest = new UpdateArchiveRequest
					{
						ArchiveName = archiveName,
						RetentionDays = options.ArchiveRetentionDays,
					};

					_ = await client.UpdateArchiveAsync(updateRequest, cancellationToken).ConfigureAwait(false);
				}
			}
			catch (ResourceNotFoundException)
			{
				var bus = await client.DescribeEventBusAsync(
						new DescribeEventBusRequest { Name = _busName },
						cancellationToken)
					.ConfigureAwait(false);

				var createRequest = new CreateArchiveRequest
				{
					ArchiveName = archiveName,
					EventSourceArn = bus.Arn,
					EventPattern = "{}",
					RetentionDays = options.ArchiveRetentionDays,
					Description = $"Dispatch archive for EventBridge bus '{_busName}'.",
				};

				_ = await client.CreateArchiveAsync(createRequest, cancellationToken).ConfigureAwait(false);
			}

			_ = Interlocked.Exchange(ref _archiveState, 1);
		}
		finally
		{
			_ = _archiveLock.Release();
		}
	}

	// Source-generated logging methods
	[LoggerMessage(AwsSqsEventId.EventBridgePublishedAction, LogLevel.Information,
		"Published action to EventBridge: {Action}")]
	private partial void LogPublishedAction(string action);

	[LoggerMessage(AwsSqsEventId.EventBridgePublishedEvent, LogLevel.Information,
		"Published event to EventBridge: {Event}")]
	private partial void LogPublishedEvent(string @event);

	[LoggerMessage(AwsSqsEventId.EventBridgeSentDocument, LogLevel.Information,
		"Sent document to EventBridge: {Doc}")]
	private partial void LogSentDocument(string doc);
}
