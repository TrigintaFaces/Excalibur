// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using CloudNative.CloudEvents;

using Excalibur.Dispatch.Abstractions;

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

		var ce = new CloudEvent(CloudEventsSpecVersion.V1_0)
		{
			Id = context.MessageId ?? Guid.NewGuid().ToString(),
			Source = !string.IsNullOrWhiteSpace(context.Source) ? new Uri(context.Source) : new Uri("urn:dispatch"),
			Type = evt.GetType().FullName ?? "unknown",
			Time = context.SentTimestampUtc ?? DateTimeOffset.UtcNow,
			Data = evt,
		};

		// Set DataContentType separately to ensure it's valid
		var contentType = !string.IsNullOrWhiteSpace(context.ContentType) ? context.ContentType : "application/json";
		ce.DataContentType = contentType;

		if (!string.IsNullOrWhiteSpace(context.CorrelationId))
		{
			ce["correlationid"] = context.CorrelationId;
		}

		if (!string.IsNullOrWhiteSpace(context.TraceParent))
		{
			ce["traceparent"] = context.TraceParent;
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
	[UnconditionalSuppressMessage(
		"AOT",
		"IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
		Justification = "Deserializing to interface type IDispatchEvent will fail and is caught. This is a fallback path.")]
	[UnconditionalSuppressMessage(
		"AOT",
		"IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
		Justification = "Deserializing to interface type IDispatchEvent will fail and is caught. This is a fallback path.")]
	public static IDispatchEvent? ToDispatchEvent(this CloudEvent cloudEvent)
	{
		ArgumentNullException.ThrowIfNull(cloudEvent);

		switch (cloudEvent.Data)
		{
			case IDispatchEvent dispatchEvent:
				return dispatchEvent;

			case JsonElement element:
				try
				{
					var json = element.GetRawText();

					// This will fail for interface types, but we catch it and return null
					return JsonSerializer.Deserialize<IDispatchEvent>(json);
				}
				catch (NotSupportedException)
				{
					// Cannot deserialize to interface type
					return null;
				}

			default:
				return cloudEvent.Data as IDispatchEvent;
		}
	}
}
