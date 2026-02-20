// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Extension methods for <see cref="MessageEnvelope" /> to support Azure/AWS transport metadata.
/// </summary>
public static class MessageEnvelopeTransportExtensions
{
	/// <summary>
	/// Constants for well-known envelope item keys.
	/// </summary>
	private const string ScheduledDeliveryTimeKey = "__ScheduledDeliveryTime";

	private const string TimeToLiveKey = "__TimeToLive";
	private const string TraceContextKey = "__TraceContext";

	/// <summary>
	/// Sets the scheduled delivery time for the message.
	/// </summary>
	/// <param name="envelope"> The message envelope. </param>
	/// <param name="value"> The scheduled delivery time. </param>
	public static void SetScheduledDeliveryTime(this MessageEnvelope envelope, DateTimeOffset? value)
	{
		ArgumentNullException.ThrowIfNull(envelope);

		if (value.HasValue)
		{
			envelope.SetItem(ScheduledDeliveryTimeKey, value.Value);
		}
	}

	/// <summary>
	/// Gets the scheduled delivery time from the message envelope.
	/// </summary>
	/// <param name="envelope"> The message envelope. </param>
	/// <returns> The scheduled delivery time or null if not set. </returns>
	public static DateTimeOffset? GetScheduledDeliveryTime(this MessageEnvelope envelope)
	{
		ArgumentNullException.ThrowIfNull(envelope);

		return envelope.GetItem<DateTimeOffset?>(ScheduledDeliveryTimeKey);
	}

	/// <summary>
	/// Sets the time to live for the message.
	/// </summary>
	/// <param name="envelope"> The message envelope. </param>
	/// <param name="value"> The time to live. </param>
	public static void SetTimeToLive(this MessageEnvelope envelope, TimeSpan? value)
	{
		ArgumentNullException.ThrowIfNull(envelope);

		if (value.HasValue)
		{
			envelope.SetItem(TimeToLiveKey, value.Value);
		}
	}

	/// <summary>
	/// Gets the time to live from the message envelope.
	/// </summary>
	/// <param name="envelope"> The message envelope. </param>
	/// <returns> The time to live or null if not set. </returns>
	public static TimeSpan? GetTimeToLive(this MessageEnvelope envelope)
	{
		ArgumentNullException.ThrowIfNull(envelope);

		return envelope.GetItem<TimeSpan?>(TimeToLiveKey);
	}

	/// <summary>
	/// Sets the session ID for the message envelope.
	/// </summary>
	/// <param name="envelope"> The message envelope. </param>
	/// <param name="value"> The session ID. </param>
	public static void SetSessionId(this MessageEnvelope envelope, string? value)
	{
		ArgumentNullException.ThrowIfNull(envelope);

		if (!string.IsNullOrEmpty(value))
		{
			envelope.SessionId = value;
		}
	}

	/// <summary>
	/// Sets the trace context for the message envelope.
	/// </summary>
	/// <param name="envelope"> The message envelope. </param>
	/// <param name="value"> The trace context. </param>
	public static void SetTraceContext(this MessageEnvelope envelope, object? value)
	{
		ArgumentNullException.ThrowIfNull(envelope);

		if (value is not null)
		{
			envelope.SetItem(TraceContextKey, value);
		}
	}

	/// <summary>
	/// Gets the trace context from the message envelope.
	/// </summary>
	/// <param name="envelope"> The message envelope. </param>
	/// <returns> The trace context or null if not set. </returns>
	public static object? GetTraceContext(this MessageEnvelope envelope)
	{
		ArgumentNullException.ThrowIfNull(envelope);

		return envelope.GetItem<object>(TraceContextKey);
	}
}
