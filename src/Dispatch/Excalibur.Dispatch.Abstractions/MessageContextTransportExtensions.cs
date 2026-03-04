// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Extension methods for <see cref="IMessageContext" /> to support transport metadata.
/// </summary>
public static class MessageContextTransportExtensions
{
	/// <summary>
	/// Constants for well-known context item keys.
	/// </summary>
	private const string ScheduledDeliveryTimeKey = "__ScheduledDeliveryTime";

	private const string TimeToLiveKey = "__TimeToLive";
	private const string TraceContextKey = "__TraceContext";

	/// <summary>
	/// Gets the scheduled delivery time from the message context.
	/// </summary>
	/// <param name="context"> The message context. </param>
	/// <returns> The scheduled delivery time or null if not set. </returns>
	public static DateTimeOffset? GetScheduledDeliveryTime(this IMessageContext context)
	{
		ArgumentNullException.ThrowIfNull(context);

		return context.GetItem<DateTimeOffset?>(ScheduledDeliveryTimeKey);
	}

	/// <summary>
	/// Gets the time to live from the message context.
	/// </summary>
	/// <param name="context"> The message context. </param>
	/// <returns> The time to live or null if not set. </returns>
	public static TimeSpan? GetTimeToLive(this IMessageContext context)
	{
		ArgumentNullException.ThrowIfNull(context);

		return context.GetItem<TimeSpan?>(TimeToLiveKey);
	}

	/// <summary>
	/// Gets the trace context from the message context.
	/// </summary>
	/// <param name="context"> The message context. </param>
	/// <returns> The trace context or null if not set. </returns>
	public static object? GetTraceContext(this IMessageContext context)
	{
		ArgumentNullException.ThrowIfNull(context);

		return context.GetItem<object>(TraceContextKey);
	}

	/// <summary>
	/// Gets all headers from the message context Items dictionary.
	/// </summary>
	/// <param name="context"> The message context. </param>
	/// <returns> A dictionary of headers. </returns>
	public static IDictionary<string, object> GetHeaders(this IMessageContext context)
	{
		ArgumentNullException.ThrowIfNull(context);

		return ExtractUserItems(context.Items);
	}

	/// <summary>
	/// Gets all attributes from the message context Items dictionary.
	/// </summary>
	/// <param name="context"> The message context. </param>
	/// <returns> A dictionary of attributes. </returns>
	public static IDictionary<string, object> GetAttributes(this IMessageContext context)
	{
		ArgumentNullException.ThrowIfNull(context);

		return ExtractUserItems(context.Items);
	}

	private static Dictionary<string, object> ExtractUserItems(IDictionary<string, object> items)
	{
		var userCount = 0;
		foreach (var item in items)
		{
			var key = item.Key;
			if (key.Length >= 2 && key[0] == '_' && key[1] == '_')
			{
				continue;
			}

			userCount++;
		}

		if (userCount == 0)
		{
			return new Dictionary<string, object>(0, StringComparer.Ordinal);
		}

		var result = new Dictionary<string, object>(userCount, StringComparer.Ordinal);
		foreach (var item in items)
		{
			var key = item.Key;
			if (key.Length >= 2 && key[0] == '_' && key[1] == '_')
			{
				continue;
			}

			result[key] = item.Value;
		}

		return result;
	}
}
