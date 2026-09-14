// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch;

/// <summary>
/// Extension methods for stamping and reading the per-message ordering sequence used by the
/// ordering-validation middleware.
/// </summary>
/// <remarks>
/// <para>
/// The framework does not stamp this for you. Deserializing a transport payload into a concrete
/// message type is the consumer's bridge between receiving and dispatching, and that bridge is the
/// only place holding both the received message and the context, so it is what stamps the sequence.
/// Call <c>TransportOrderingMetadata.TryStampOrdering</c> there to lift a transport's native sequence
/// (a Kafka partition offset, an Azure Service Bus <c>SequenceNumber</c>) out of the received message,
/// or call <see cref="SetOrderingSequence(IMessageContext, long, string?)"/> directly where the
/// transport has no native monotonic sequence -- Pub/Sub among them.
/// </para>
/// <para>
/// The ordering-validation middleware reads the stamp via
/// <see cref="TryGetOrderingSequence(IMessageContext, out long, out string)"/> and enforces
/// strictly-increasing order per key. It only enforces on messages that entered an ordered receive
/// path, which <see cref="MarkOrderingEnforced(IMessageContext)"/> records; anything else -- an
/// outbound send, an in-process command -- passes through untouched.
/// </para>
/// </remarks>
public static class OrderingContextExtensions
{
	internal const string OrderingSequenceKey = "excalibur.ordering.sequence";
	internal const string OrderingKeyKey = "excalibur.ordering.key";
	internal const string OrderingEnforcedKey = "excalibur.ordering.enforced";

	/// <summary>
	/// Records that this message arrived through a receive path where ordering is enforced.
	/// </summary>
	/// <remarks>
	/// This is what bounds the fail-closed check to the messages it is about. The middleware is
	/// registered once for the whole process and sees every dispatch, so without a marker "no sequence
	/// present" cannot be told apart from "this message was never supposed to carry one" -- and an
	/// outbound send or a plain in-process command would be rejected for lacking a sequence nothing
	/// was ever going to give it. Marking happens when the receive bridge attempts the stamp, so it
	/// records the ATTEMPT rather than its success: a message that entered an ordered path and arrived
	/// without a usable sequence is still a fault, and still fails closed.
	/// </remarks>
	/// <param name="context"> The message context. </param>
	/// <exception cref="ArgumentNullException"> <paramref name="context"/> is <see langword="null"/>. </exception>
	public static void MarkOrderingEnforced(this IMessageContext context)
	{
		ArgumentNullException.ThrowIfNull(context);

		context.Items[OrderingEnforcedKey] = true;
	}

	/// <summary>
	/// Indicates whether this message arrived through a receive path where ordering is enforced.
	/// </summary>
	/// <param name="context"> The message context. </param>
	/// <returns>
	/// <see langword="true"/> if <see cref="MarkOrderingEnforced(IMessageContext)"/> was called for this
	/// message; otherwise <see langword="false"/>, meaning ordering does not apply to it.
	/// </returns>
	/// <exception cref="ArgumentNullException"> <paramref name="context"/> is <see langword="null"/>. </exception>
	public static bool IsOrderingEnforced(this IMessageContext context)
	{
		ArgumentNullException.ThrowIfNull(context);

		return context.Items.TryGetValue(OrderingEnforcedKey, out var raw) && raw is true;
	}

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
