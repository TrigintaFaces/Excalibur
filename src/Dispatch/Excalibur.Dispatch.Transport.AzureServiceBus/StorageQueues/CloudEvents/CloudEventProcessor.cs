// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Text;
using System.Text.Json;

using Azure.Storage.Queues.Models;

using CloudNative.CloudEvents;
using CloudNative.CloudEvents.SystemTextJson;

using Excalibur.Dispatch.Abstractions;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Transport.Azure;

/// <summary>
/// Handles CloudEvents parsing and conversion for Azure Storage Queue messages.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="CloudEventProcessor" /> class. </remarks>
/// <param name="logger"> The logger instance. </param>
public sealed class CloudEventProcessor(ILogger<CloudEventProcessor> logger) : ICloudEventProcessor
{
	private static readonly JsonEventFormatter JsonFormatter = new();

	private readonly ILogger<CloudEventProcessor> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

	/// <inheritdoc />
	public bool TryParseCloudEvent(string messageText, out CloudEvent? cloudEvent)
	{
		cloudEvent = null;

		try
		{
			if (string.IsNullOrWhiteSpace(messageText))
			{
				return false;
			}

			// Try to parse as CloudEvent JSON
			using var document = JsonDocument.Parse(messageText);
			var root = document.RootElement;

			// Check if it has CloudEvent structure
			if (!root.TryGetProperty("specversion", out _))
			{
				return false;
			}

			cloudEvent = JsonFormatter.DecodeStructuredModeMessage(Encoding.UTF8.GetBytes(messageText), contentType: null, extensionAttributes: null);
			return cloudEvent != null;
		}
		catch (JsonException ex)
		{
			_logger.LogDebug("Failed to parse CloudEvent JSON: {Error}", ex.Message);
			return false;
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Unexpected error parsing CloudEvent");
			return false;
		}
	}

	/// <inheritdoc />
	public IDispatchEvent ConvertToDispatchEvent(CloudEvent cloudEvent, QueueMessage queueMessage, IMessageContext context)
	{
		ArgumentNullException.ThrowIfNull(cloudEvent);
		ArgumentNullException.ThrowIfNull(queueMessage);
		ArgumentNullException.ThrowIfNull(context);

		var eventType = cloudEvent.Type ?? "unknown";
		var eventSource = cloudEvent.Source?.ToString() ?? "unknown";
		var eventSubject = cloudEvent.Subject;

		// Extract data
		object? eventData = null;
		if (cloudEvent.Data != null)
		{
			eventData = cloudEvent.Data;
		}

		return new StorageQueueDispatchEvent(
			eventType,
			eventSource,
			eventSubject,
			eventData,
			queueMessage.MessageId,
			queueMessage.PopReceipt,
			context);
	}

	/// <inheritdoc />
	public void UpdateContextFromCloudEvent(IMessageContext context, CloudEvent cloudEvent)
	{
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(cloudEvent);

		// Update context with CloudEvent attributes
		if (cloudEvent.Id != null)
		{
			context.CorrelationId = new CorrelationId(cloudEvent.Id).ToString();
		}

		if (cloudEvent.Source != null)
		{
			context.SetItem("cloudevent.source", cloudEvent.Source.ToString());
		}

		if (cloudEvent.Type != null)
		{
			context.SetItem("cloudevent.type", cloudEvent.Type);
		}

		if (cloudEvent.Subject != null)
		{
			context.SetItem("cloudevent.subject", cloudEvent.Subject);
		}

		if (cloudEvent.Time.HasValue)
		{
			context.SetItem("cloudevent.time", cloudEvent.Time.Value.ToString("O"));
		}

		// Add all extension attributes
		foreach (var attribute in cloudEvent.GetPopulatedAttributes())
		{
			if (attribute.Key.Name.StartsWith("ce-", StringComparison.Ordinal))
			{
				var key = $"cloudevent.{attribute.Key.Name[3..]}";
				context.SetItem(key, attribute.Value?.ToString() ?? string.Empty);
			}
		}
	}

	/// <summary>
	/// Represents a dispatch event from Azure Storage Queue with CloudEvent data.
	/// </summary>
	private sealed class StorageQueueDispatchEvent(
		string eventType,
		string source,
		string? subject,
		object? data,
		string messageId,
		string popReceipt,
		IMessageContext context)
		: IDispatchEvent
	{
		public Guid Id { get; } = Guid.NewGuid();

		public MessageKinds Kind { get; } = MessageKinds.Event;

		public IReadOnlyDictionary<string, object> Headers { get; } = new Dictionary<string, object>(StringComparer.Ordinal);

		public object Body { get; } = data ?? new object();

		public string MessageType { get; } = eventType;

		public IMessageFeatures Features { get; } = new DefaultMessageFeatures();

		public string Type { get; } = eventType;

		public string Source { get; } = source;

		public string? Subject { get; } = subject;

		public object? Data => Body;

		public string MessageId { get; } = messageId;

		public string PopReceipt { get; } = popReceipt;

		public IMessageContext Context { get; } = context;

		public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;
	}
}
