// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Text.Json;

using CloudNative.CloudEvents;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Features;

namespace Excalibur.Dispatch.CloudEvents;

/// <summary>
/// Extension methods for working with CloudEvents and Dispatch types.
/// </summary>
public static class CloudEventExtensions
{
	/// <summary>
	/// Converts a dispatch event to a CloudEvent.
	/// </summary>
	/// <param name="evt"> The dispatch event to convert. </param>
	/// <param name="context"> The message context containing metadata. </param>
	/// <returns> A CloudEvent representation of the dispatch event. </returns>
	public static CloudEvent ToCloudEvent(this IDispatchEvent evt, IMessageContext context)
	{
		ArgumentNullException.ThrowIfNull(evt);
		ArgumentNullException.ThrowIfNull(context);

		var source = context.GetSource();
		var sentTimestamp = context.GetSentTimestampUtc();

		var ce = new CloudEvent(CloudEventsSpecVersion.V1_0)
		{
			Id = context.MessageId ?? Guid.NewGuid().ToString(),
			Source = !string.IsNullOrWhiteSpace(source) ? new Uri(source) : new Uri("urn:dispatch"),
			Type = evt.GetType().FullName ?? "unknown",
			Time = sentTimestamp ?? DateTimeOffset.UtcNow,
			Data = evt,
		};

		// Set DataContentType separately to ensure it's valid
		var contentType = context.GetContentType();
		var resolvedContentType = !string.IsNullOrWhiteSpace(contentType) ? contentType : "application/json";
		ce.DataContentType = resolvedContentType;

		if (!string.IsNullOrWhiteSpace(context.CorrelationId))
		{
			ce["correlationid"] = context.CorrelationId;
		}

		var traceParent = context.GetTraceParent();
		if (!string.IsNullOrWhiteSpace(traceParent))
		{
			ce["traceparent"] = traceParent;
		}

		foreach (var kvp in context.Items)
		{
			ce[kvp.Key] = kvp.Value?.ToString();
		}

		return ce;
	}

	/// <summary>
	/// Converts a CloudEvent back to a dispatch event.
	/// </summary>
	/// <param name="cloudEvent"> The CloudEvent to convert. </param>
	/// <returns> A dispatch event if conversion is successful; otherwise, null. </returns>
	public static IDispatchEvent? ToDispatchEvent(this CloudEvent cloudEvent)
	{
		ArgumentNullException.ThrowIfNull(cloudEvent);

		// JsonElement cannot be deserialized to IDispatchEvent without a custom converter
		// or type discriminator. Callers should use CloudEvent.Type to resolve a concrete
		// .NET type, then deserialize with their own JsonSerializerContext.
		return cloudEvent.Data switch
		{
			IDispatchEvent dispatchEvent => dispatchEvent,
			JsonElement => null,
			_ => cloudEvent.Data as IDispatchEvent,
		};
	}
}
