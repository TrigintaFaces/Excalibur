// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Globalization;
using System.Text;

using CloudNative.CloudEvents;

using Excalibur.Dispatch.Options.CloudEvents;

using MessageEnvelope = Excalibur.Dispatch.Abstractions.MessageEnvelope;

namespace Excalibur.Dispatch.CloudEvents;

// DUPLICATE REMOVED: Vibe AI generated MessageEnvelope class removed - use canonical Excalibur.Dispatch.Abstractions.MessageEnvelope<T> instead

/// <summary>
/// Default implementation of ICloudEventEnvelopeConverter for DoD-compliant envelope conversion.
/// </summary>
/// <remarks> Initializes a new instance of the CloudEventEnvelopeConverter. </remarks>
/// <param name="options"> CloudEvent configuration options. </param>
public sealed class CloudEventEnvelopeConverter(CloudEventOptions options) : ICloudEventEnvelopeConverter
{
	private readonly CloudEventOptions _options = options ?? throw new ArgumentNullException(nameof(options));

	/// <inheritdoc />
	public Task<CloudEvent> FromEnvelopeAsync(MessageEnvelope envelope, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(envelope);

		var message = envelope.Message;

		var cloudEvent = CreateBaseCloudEvent(envelope, message);
		AddDoradRequiredAttributes(cloudEvent, envelope);
		AddCustomContextItems(cloudEvent, envelope);

		return Task.FromResult(cloudEvent);
	}

	/// <inheritdoc />
	public Task<MessageEnvelope> ToEnvelopeAsync(CloudEvent cloudEvent, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(cloudEvent);

		var envelope = CreateBaseEnvelope(cloudEvent);
		RestoreDoradRequiredProperties(cloudEvent, envelope);
		RestoreCustomHeaders(cloudEvent, envelope);

		return Task.FromResult(envelope);
	}

	/// <summary>
	/// Creates the base MessageEnvelope from CloudEvent core properties.
	/// </summary>
	private static MessageEnvelope CreateBaseEnvelope(CloudEvent cloudEvent)
	{
		// Create a simple message wrapper for the CloudEvent data
		var message = new CloudEventMessage { MessageId = cloudEvent.Id!, Type = cloudEvent.Type!, Data = cloudEvent.Data };

		// Create envelope directly since it implements IMessageContext
		return new MessageEnvelope(message)
		{
			MessageId = cloudEvent.Id,
			MessageType = cloudEvent.Type,
			Source = cloudEvent.Source?.ToString(),
			ContentType = cloudEvent.DataContentType ?? "application/json",
			ReceivedTimestampUtc = DateTimeOffset.UtcNow,
			SentTimestampUtc = cloudEvent.Time,
			Subject = cloudEvent.Subject,
			Body = cloudEvent.Data switch
			{
				string s => s,
				ReadOnlyMemory<byte> mem => Encoding.UTF8.GetString(mem.Span),
				byte[] bytes => Encoding.UTF8.GetString(bytes),
				_ => cloudEvent.Data?.ToString(),
			},
		};
	}

	/// <summary>
	/// Creates the base CloudEvent with core properties.
	/// </summary>
	private CloudEvent CreateBaseCloudEvent(MessageEnvelope? context, object? message)
	{
		var ce = new CloudEvent
		{
			Id = context?.MessageId ?? Guid.NewGuid().ToString(),
			Type = context?.MessageType ?? message?.GetType().FullName ?? "UnknownEvent",
			Source = _options.DefaultSource,
			Subject = context?.Subject ?? context?.ExternalId,
			Time = context?.SentTimestampUtc ?? context?.ReceivedTimestampUtc,
			DataContentType = context?.ContentType ?? "application/json",
		};

		// Prefer explicit envelope Body if provided; otherwise use message object
		ce.Data = context?.Body ?? message;
		return ce;
	}

	/// <summary>
	/// Adds DoD-required extension attributes to the CloudEvent.
	/// </summary>
	private void AddDoradRequiredAttributes(CloudEvent cloudEvent, MessageEnvelope? context)
	{
		SetAttributeIfNotEmpty(cloudEvent, "correlationid", context?.CorrelationId);
		SetAttributeIfNotEmpty(cloudEvent, "tenantid", context?.TenantId);
		SetAttributeIfNotEmpty(cloudEvent, "userid", context?.UserId);
		SetAttributeIfNotEmpty(cloudEvent, "traceid", context?.TraceParent);
		SetAttributeIfNotEmpty(cloudEvent, "messagetype", context?.MessageType);

		if (context?.DeliveryCount > 0)
		{
			cloudEvent.SetAttributeFromString(
				$"{_options.DispatchExtensionPrefix}retrycount",
				context.DeliveryCount.ToString(CultureInfo.InvariantCulture));
		}

		if (context?.ReceivedTimestampUtc != null)
		{
			cloudEvent.SetAttributeFromString(
				$"{_options.DispatchExtensionPrefix}timestamp",
				context.ReceivedTimestampUtc.ToString("O"));
		}
	}

	/// <summary>
	/// Sets a CloudEvent attribute if the value is not null or empty.
	/// </summary>
	private void SetAttributeIfNotEmpty(CloudEvent cloudEvent, string attributeName, string? value)
	{
		if (!string.IsNullOrEmpty(value))
		{
			cloudEvent.SetAttributeFromString(_options.DispatchExtensionPrefix + attributeName, value);
		}
	}

	/// <summary>
	/// Adds custom context items as extension attributes.
	/// </summary>
	private void AddCustomContextItems(CloudEvent cloudEvent, MessageEnvelope? context)
	{
		if (context?.Items != null)
		{
			foreach (var item in context.Items)
			{
				var headerKey = $"{_options.DispatchExtensionPrefix}header{item.Key.ToUpperInvariant()}";
				cloudEvent.SetAttributeFromString(headerKey, item.Value?.ToString() ?? string.Empty);
			}
		}
	}

	/// <summary>
	/// Restores DoD-required properties from CloudEvent extension attributes.
	/// </summary>
	private void RestoreDoradRequiredProperties(CloudEvent cloudEvent, MessageEnvelope envelope)
	{
		RestoreStringProperty(cloudEvent, envelope, "correlationid", (env, value) => env.CorrelationId = value);
		RestoreStringProperty(cloudEvent, envelope, "tenantid", (env, value) => env.TenantId = value);
		RestoreStringProperty(cloudEvent, envelope, "userid", (env, value) => env.UserId = value);
		RestoreStringProperty(cloudEvent, envelope, "traceid", (env, value) => env.TraceParent = value);

		RestoreRetryCount(cloudEvent, envelope);
		RestoreTimestamp(cloudEvent, envelope);
	}

	/// <summary>
	/// Restores a string property from CloudEvent extension attributes.
	/// </summary>
	private void RestoreStringProperty(CloudEvent cloudEvent, MessageEnvelope envelope, string attributeName,
		Action<MessageEnvelope, string> setter)
	{
		var value = cloudEvent[_options.DispatchExtensionPrefix + attributeName]?.ToString();
		if (!string.IsNullOrEmpty(value))
		{
			setter(envelope, value);
		}
	}

	/// <summary>
	/// Restores retry count from CloudEvent extension attributes.
	/// </summary>
	private void RestoreRetryCount(CloudEvent cloudEvent, MessageEnvelope envelope)
	{
		var retryCountStr = cloudEvent[$"{_options.DispatchExtensionPrefix}retrycount"]?.ToString();
		if (!string.IsNullOrEmpty(retryCountStr) && int.TryParse(retryCountStr, out var retryCount))
		{
			envelope.DeliveryCount = retryCount;
		}
	}

	/// <summary>
	/// Restores timestamp from CloudEvent extension attributes.
	/// </summary>
	private void RestoreTimestamp(CloudEvent cloudEvent, MessageEnvelope envelope)
	{
		var timestampStr = cloudEvent[$"{_options.DispatchExtensionPrefix}timestamp"]?.ToString();
		if (!string.IsNullOrEmpty(timestampStr) && DateTimeOffset.TryParse(timestampStr, CultureInfo.InvariantCulture,
				DateTimeStyles.RoundtripKind, out var timestamp))
		{
			envelope.ReceivedTimestampUtc = timestamp;
		}
	}

	/// <summary>
	/// Restores custom headers from CloudEvent extension attributes.
	/// </summary>
	private void RestoreCustomHeaders(CloudEvent cloudEvent, MessageEnvelope envelope)
	{
		var headerPrefix = $"{_options.DispatchExtensionPrefix}header";
		foreach (var attribute in cloudEvent.GetPopulatedAttributes())
		{
			if (attribute.Key.Name.StartsWith(headerPrefix, StringComparison.Ordinal))
			{
				var headerKey = attribute.Key.Name.Substring(headerPrefix.Length);
				if (!string.IsNullOrEmpty(headerKey))
				{
					envelope.Items[headerKey] = attribute.Value ?? string.Empty;
				}
			}
		}
	}
}
