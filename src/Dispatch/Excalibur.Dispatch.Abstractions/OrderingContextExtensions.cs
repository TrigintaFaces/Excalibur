// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch;

/// <summary>
/// Extension methods for stamping and reading the per-message ordering sequence used by the
/// ordering-validation middleware.
/// </summary>
/// <remarks>
/// A first-party transport receive path stamps the transport's native monotonic sequence (Kafka
/// partition offset, Azure Service Bus <c>SequenceNumber</c>, Pub/Sub ordering) via
/// <see cref="SetOrderingSequence(IMessageContext, long, string?)"/> at the receive boundary; where no
/// native sequence exists the consumer stamps it explicitly (opt-in). The ordering-validation
/// middleware reads it via <see cref="TryGetOrderingSequence(IMessageContext, out long, out string)"/>
/// and enforces strictly-increasing order per key.
/// </remarks>
public static class OrderingContextExtensions
{
	internal const string OrderingSequenceKey = "excalibur.ordering.sequence";
	internal const string OrderingKeyKey = "excalibur.ordering.key";

	/// <summary>
	/// The ordering key used when a message is stamped with a sequence but no explicit key — a single
	/// global ordering stream.
	/// </summary>
	public const string DefaultOrderingKey = "__default__";

	/// <summary>
	/// Stamps the message context with a monotonic ordering sequence (and optional ordering key) so the
	/// ordering-validation middleware can enforce strictly-increasing delivery per key.
	/// </summary>
	/// <param name="context"> The message context. </param>
	/// <param name="sequence"> The transport-native monotonic sequence (e.g. Kafka offset, ASB SequenceNumber). </param>
	/// <param name="orderingKey">
	/// The ordering key (partition/stream) the sequence is monotonic within. When <see langword="null"/>
	/// or empty, <see cref="DefaultOrderingKey"/> is used.
	/// </param>
	/// <exception cref="ArgumentNullException"> <paramref name="context"/> is <see langword="null"/>. </exception>
	public static void SetOrderingSequence(this IMessageContext context, long sequence, string? orderingKey = null)
	{
		ArgumentNullException.ThrowIfNull(context);

		context.Items[OrderingSequenceKey] = sequence;
		context.Items[OrderingKeyKey] = string.IsNullOrEmpty(orderingKey) ? DefaultOrderingKey : orderingKey;
	}

	/// <summary>
	/// Attempts to read the ordering sequence and key stamped on the context.
	/// </summary>
	/// <param name="context"> The message context. </param>
	/// <param name="sequence"> When this returns <see langword="true"/>, the stamped sequence. </param>
	/// <param name="orderingKey"> When this returns <see langword="true"/>, the ordering key (never empty). </param>
	/// <returns>
	/// <see langword="true"/> if an ordering sequence was stamped (ordering enforcement applies);
	/// otherwise <see langword="false"/> (the message is unordered and passes through).
	/// </returns>
	/// <exception cref="ArgumentNullException"> <paramref name="context"/> is <see langword="null"/>. </exception>
	public static bool TryGetOrderingSequence(this IMessageContext context, out long sequence, out string orderingKey)
	{
		ArgumentNullException.ThrowIfNull(context);

		sequence = 0;
		orderingKey = DefaultOrderingKey;

		if (!context.Items.TryGetValue(OrderingSequenceKey, out var rawSequence) || rawSequence is not long seq)
		{
			return false;
		}

		sequence = seq;
		if (context.Items.TryGetValue(OrderingKeyKey, out var rawKey) && rawKey is string key && !string.IsNullOrEmpty(key))
		{
			orderingKey = key;
		}

		return true;
	}
}
