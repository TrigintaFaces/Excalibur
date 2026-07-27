// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Well-known <see cref="TransportReceivedMessage.ProviderData"/> keys that carry a transport's native
/// monotonic ordering sequence, forming the single convention the receive→context pump reads to stamp
/// the ordering-validation context.
/// </summary>
/// <remarks>
/// Transports with a native monotonic sequence emit it into
/// <see cref="TransportReceivedMessage.ProviderData"/> under one of these keys (a <see cref="long"/>):
/// Kafka partition offset under <see cref="KafkaOffsetKey"/>, Azure Service Bus <c>SequenceNumber</c>
/// under <see cref="AzureServiceBusSequenceKey"/>. The ordering key the sequence is monotonic within is
/// taken from <see cref="TransportReceivedMessage.MessageGroupId"/> (falling back to
/// <see cref="TransportReceivedMessage.PartitionKey"/>). Centralising these as constants in one contract
/// — rather than magic strings scattered per transport — keeps the convention discoverable and prevents
/// per-transport drift (the fork Amendment 6 forbids). Transports without a native monotonic sequence
/// (e.g. Google Pub/Sub, whose ordering key is a service-enforced string, not a sequence) do not
/// populate these; ordering there is consumer-stamped (opt-in).
/// </remarks>
public static class TransportOrderingMetadata
{
	/// <summary>
	/// <see cref="TransportReceivedMessage.ProviderData"/> key for the Azure Service Bus
	/// <c>SequenceNumber</c> (a monotonic <see cref="long"/> per session/partition).
	/// </summary>
	public const string AzureServiceBusSequenceKey = "azure.sequence_number";

	/// <summary>
	/// <see cref="TransportReceivedMessage.ProviderData"/> key for the Kafka partition offset (a monotonic
	/// <see cref="long"/> per topic-partition).
	/// </summary>
	public const string KafkaOffsetKey = "kafka.offset";

	/// <summary>
	/// Stamps the ordering-validation sequence onto <paramref name="context"/> from a received message's
	/// native transport sequence, if present. This is the single shared stamping seam the per-transport
	/// receive→context bridges call, so the extraction logic cannot drift between transports.
	/// </summary>
	/// <remarks>
	/// Reads the native monotonic sequence from <see cref="TransportReceivedMessage.ProviderData"/> under
	/// <see cref="AzureServiceBusSequenceKey"/> or <see cref="KafkaOffsetKey"/> and the ordering key from
	/// <see cref="TransportReceivedMessage.MessageGroupId"/> (falling back to
	/// <see cref="TransportReceivedMessage.PartitionKey"/>), then calls
	/// <see cref="OrderingContextExtensions.SetOrderingSequence(IMessageContext, long, string?)"/>. It is a
	/// no-op for transports that carry no native sequence (e.g. Google Pub/Sub) — ordering there is
	/// consumer-stamped (opt-in). A missing stamp is <em>not</em> silently safe: if ordering validation is
	/// active for the message, the middleware fails closed on an unstamped message, so a forgotten call
	/// site surfaces loudly rather than passing unordered.
	/// </remarks>
	/// <param name="received"> The received transport message carrying the native sequence in provider data. </param>
	/// <param name="context"> The message context to stamp. </param>
	/// <returns>
	/// <see langword="true"/> if a native sequence was found and stamped; otherwise <see langword="false"/>.
	/// </returns>
	/// <exception cref="ArgumentNullException"><paramref name="received"/> or <paramref name="context"/> is <see langword="null"/>.</exception>
	public static bool TryStampOrdering(TransportReceivedMessage received, IMessageContext context)
	{
		ArgumentNullException.ThrowIfNull(received);
		ArgumentNullException.ThrowIfNull(context);

		long? sequence = null;
		if (received.ProviderData.TryGetValue(AzureServiceBusSequenceKey, out var asbValue) && asbValue is long asbSequence)
		{
			sequence = asbSequence;
		}
		else if (received.ProviderData.TryGetValue(KafkaOffsetKey, out var kafkaValue) && kafkaValue is long kafkaOffset)
		{
			sequence = kafkaOffset;
		}

		if (sequence is null)
		{
			return false;
		}

		var orderingKey = !string.IsNullOrEmpty(received.MessageGroupId) ? received.MessageGroupId : received.PartitionKey;
		context.SetOrderingSequence(sequence.Value, orderingKey);
		return true;
	}
}
