// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json;

using Excalibur.Compliance.Configuration;
using Excalibur.Dispatch;

using Microsoft.Extensions.Options;

namespace Excalibur.Compliance.Encryption.Decorators;

/// <summary>
/// Decorates an <see cref="IOutboxStore" /> with transparent field-level encryption.
/// </summary>
/// <remarks>
/// <para>
/// This decorator provides mixed-mode read support during encryption migration. On reads, it uses
/// <see cref="EncryptedData.IsFieldEncrypted(byte[])" /> to detect encrypted data and decrypts only when needed, allowing seamless handling
/// of both plaintext and encrypted messages.
/// </para>
/// </remarks>
internal sealed class EncryptingOutboxStoreDecorator : IsolatingOutboxStoreDecorator
{
	/// <summary>
	/// The capabilities forwarded to the decorated store unmediated, because no member of either -- inherited
	/// members included -- accepts or returns a message payload.
	/// </summary>
	/// <remarks>
	/// <see cref="IDeadLetterableOutboxStore"/> and <see cref="IBackoffSchedulableOutboxStore"/> move message
	/// identifiers, reasons, and retry counts. Neither derives from <see cref="IOutboxStore"/>, so neither
	/// inherits its payload-bearing surface. Every other capability is denied or wrapped.
	/// </remarks>
	private static readonly HashSet<Type> ForwardableCapabilitySet =
	[
		typeof(IDeadLetterableOutboxStore),
		typeof(IBackoffSchedulableOutboxStore)
	];

	private readonly IEncryptionProviderRegistry _registry;
	private readonly IOptions<EncryptionOptions> _options;
	private readonly EncryptionContext _defaultContext;

	/// <summary>
	/// Initializes a new instance of the <see cref="EncryptingOutboxStoreDecorator" /> class.
	/// </summary>
	/// <param name="inner"> The underlying outbox store to decorate. </param>
	/// <param name="registry"> The encryption provider registry for multi-provider support. </param>
	/// <param name="options"> The encryption configuration options. </param>
	public EncryptingOutboxStoreDecorator(
		IOutboxStore inner,
		IEncryptionProviderRegistry registry,
		IOptions<EncryptionOptions> options)
		: base(inner)
	{
		_registry = registry ?? throw new ArgumentNullException(nameof(registry));
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_defaultContext = new EncryptionContext
		{
			Purpose = options.Value.DefaultPurpose,
			TenantId = options.Value.DefaultTenantId,
			RequireFipsCompliance = options.Value.RequireFipsCompliance
		};
	}

	/// <inheritdoc />
	protected override IReadOnlySet<Type> ForwardableCapabilities => ForwardableCapabilitySet;

	/// <summary>
	/// Wraps every capability of the decorated store whose surface carries a message payload, so that payloads
	/// are encrypted on the way in and decrypted on the way out no matter which capability a caller reaches for.
	/// </summary>
	/// <param name="serviceType"> The capability interface being resolved. </param>
	/// <returns> A decrypting/encrypting view, or <see langword="null"/> when the decorated store lacks it. </returns>
	/// <remarks>
	/// Five capabilities carry payloads. <see cref="IOutboxStoreBatch"/> is the subtle one: two of its three
	/// members move identifiers, and the third accepts an <see cref="InboxEntry"/> whose
	/// <see cref="InboxEntry.Payload"/> is a <see cref="byte"/> array. A per-interface allowlist cannot express
	/// its safety -- only a wrapper can. <see cref="IFencedOutboxStore"/> and
	/// <see cref="IMultiTransportOutboxStore"/> each derive from <see cref="IOutboxStore"/> and so inherit its
	/// payload-bearing surface; their views route those inherited members back through this decorator.
	/// </remarks>
	protected override object? WrapCapability(Type serviceType)
	{
		if (serviceType == typeof(IOutboxStoreAdmin))
		{
			return Inner.GetService(typeof(IOutboxStoreAdmin)) is IOutboxStoreAdmin admin
				? new DecryptingAdmin(admin, this)
				: null;
		}

		if (serviceType == typeof(IOutboxStoreBatch))
		{
			return Inner.GetService(typeof(IOutboxStoreBatch)) is IOutboxStoreBatch batch
				? new EncryptingBatch(batch, this)
				: null;
		}

		if (serviceType == typeof(IMultiTransportOutboxStore))
		{
			return Inner.GetService(typeof(IMultiTransportOutboxStore)) is IMultiTransportOutboxStore multi
				? new EncryptingMultiTransportStore(multi, this)
				: null;
		}

		if (serviceType == typeof(IMultiTransportOutboxStoreAdmin))
		{
			return Inner.GetService(typeof(IMultiTransportOutboxStoreAdmin)) is IMultiTransportOutboxStoreAdmin multiAdmin
				? new DecryptingMultiTransportAdmin(multiAdmin, this)
				: null;
		}

		if (serviceType == typeof(IFencedOutboxStore))
		{
			return Inner.GetService(typeof(IFencedOutboxStore)) is IFencedOutboxStore fenced
				? new DecryptingFencedStore(fenced, this)
				: null;
		}

		return null;
	}

	/// <inheritdoc />
	public override async ValueTask StageMessageAsync(OutboundMessage message, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);

		message.Payload = await EncryptForWriteAsync(message.Payload, cancellationToken).ConfigureAwait(false);

		await Inner.StageMessageAsync(message, cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public override async ValueTask EnqueueAsync(IDispatchMessage message, IMessageContext context, CancellationToken cancellationToken)
	{
		var mode = _options.Value.Mode;

		if (mode == EncryptionMode.DecryptOnlyReadOnly)
		{
			throw new InvalidOperationException(
				Resources.EncryptingOutboxStoreDecorator_ReadOnlyMode);
		}

		// Encryption of the message payload happens at serialization time, not here
		await Inner.EnqueueAsync(message, context, cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public override async ValueTask<IEnumerable<OutboundMessage>> GetUnsentMessagesAsync(int batchSize, CancellationToken cancellationToken)
	{
		var messages = await Inner.GetUnsentMessagesAsync(batchSize, cancellationToken).ConfigureAwait(false);
		return await DecryptMessagesAsync(messages, cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public override ValueTask MarkSentAsync(string messageId, CancellationToken cancellationToken)
	{
		return Inner.MarkSentAsync(messageId, cancellationToken);
	}

	/// <inheritdoc />
	public override ValueTask MarkFailedAsync(string messageId, string errorMessage, int retryCount, CancellationToken cancellationToken) =>
		Inner.MarkFailedAsync(messageId, errorMessage, retryCount, cancellationToken);

	/// <summary>
	/// An <see cref="IOutboxStoreAdmin"/> view over the decorated store's own admin capability, whose query
	/// results are decrypted before they reach the caller.
	/// </summary>
	private sealed class DecryptingAdmin(IOutboxStoreAdmin inner, EncryptingOutboxStoreDecorator owner) : IOutboxStoreAdmin
	{
		public async ValueTask<IEnumerable<OutboundMessage>> GetFailedMessagesAsync(
			int maxRetries,
			DateTimeOffset? olderThan,
			int batchSize,
			CancellationToken cancellationToken)
		{
			var messages = await inner.GetFailedMessagesAsync(maxRetries, olderThan, batchSize, cancellationToken).ConfigureAwait(false);
			return await owner.DecryptMessagesAsync(messages, cancellationToken).ConfigureAwait(false);
		}

		public async ValueTask<IEnumerable<OutboundMessage>> GetScheduledMessagesAsync(
			DateTimeOffset scheduledBefore,
			int batchSize,
			CancellationToken cancellationToken)
		{
			var messages = await inner.GetScheduledMessagesAsync(scheduledBefore, batchSize, cancellationToken).ConfigureAwait(false);
			return await owner.DecryptMessagesAsync(messages, cancellationToken).ConfigureAwait(false);
		}

		public ValueTask<int> CleanupAllTenantsSentMessagesAsync(DateTimeOffset olderThan, int batchSize, CancellationToken cancellationToken) =>
			inner.CleanupAllTenantsSentMessagesAsync(olderThan, batchSize, cancellationToken);

		public ValueTask<OutboxStatistics> GetStatisticsAsync(CancellationToken cancellationToken) =>
			inner.GetStatisticsAsync(cancellationToken);
	}

	/// <summary>
	/// An <see cref="IOutboxStoreBatch"/> view whose inbox-entry payload is encrypted before it is written.
	/// </summary>
	private sealed class EncryptingBatch(IOutboxStoreBatch inner, EncryptingOutboxStoreDecorator owner) : IOutboxStoreBatch
	{
		public ValueTask MarkBatchSentAsync(IReadOnlyList<string> messageIds, CancellationToken cancellationToken) =>
			inner.MarkBatchSentAsync(messageIds, cancellationToken);

		public ValueTask MarkBatchFailedAsync(IReadOnlyList<string> messageIds, string reason, int retryCount, CancellationToken cancellationToken) =>
			inner.MarkBatchFailedAsync(messageIds, reason, retryCount, cancellationToken);

		public async ValueTask<bool> TryMarkSentAndReceivedAsync(string messageId, InboxEntry inboxEntry, CancellationToken cancellationToken)
		{
			ArgumentNullException.ThrowIfNull(inboxEntry);

			inboxEntry.Payload = await owner.EncryptForWriteAsync(inboxEntry.Payload, cancellationToken).ConfigureAwait(false);

			return await inner.TryMarkSentAndReceivedAsync(messageId, inboxEntry, cancellationToken).ConfigureAwait(false);
		}
	}

	/// <summary>
	/// An <see cref="IMultiTransportOutboxStore"/> view that encrypts staged payloads and routes the members it
	/// inherits from <see cref="IOutboxStore"/> back through the decorator.
	/// </summary>
	private sealed class EncryptingMultiTransportStore(IMultiTransportOutboxStore inner, EncryptingOutboxStoreDecorator owner)
		: IMultiTransportOutboxStore
	{
		public async Task StageMessageWithTransportsAsync(
			OutboundMessage message,
			IEnumerable<OutboundMessageTransport> transports,
			CancellationToken cancellationToken)
		{
			ArgumentNullException.ThrowIfNull(message);

			message.Payload = await owner.EncryptForWriteAsync(message.Payload, cancellationToken).ConfigureAwait(false);

			await inner.StageMessageWithTransportsAsync(message, transports, cancellationToken).ConfigureAwait(false);
		}

		// Carries transport identity and status, never a payload.
		public Task<IEnumerable<OutboundMessageTransport>> GetTransportDeliveriesAsync(string messageId, CancellationToken cancellationToken) =>
			inner.GetTransportDeliveriesAsync(messageId, cancellationToken);

		public Task MarkTransportSentAsync(string messageId, string transportName, CancellationToken cancellationToken) =>
			inner.MarkTransportSentAsync(messageId, transportName, cancellationToken);

		public Task MarkTransportFailedAsync(string messageId, string transportName, string errorMessage, CancellationToken cancellationToken) =>
			inner.MarkTransportFailedAsync(messageId, transportName, errorMessage, cancellationToken);

		public Task MarkTransportSkippedAsync(string messageId, string transportName, string? reason, CancellationToken cancellationToken) =>
			inner.MarkTransportSkippedAsync(messageId, transportName, reason, cancellationToken);

		public ValueTask StageMessageAsync(OutboundMessage message, CancellationToken cancellationToken) =>
			owner.StageMessageAsync(message, cancellationToken);

		public ValueTask EnqueueAsync(IDispatchMessage message, IMessageContext context, CancellationToken cancellationToken) =>
			owner.EnqueueAsync(message, context, cancellationToken);

		public ValueTask<IEnumerable<OutboundMessage>> GetUnsentMessagesAsync(int batchSize, CancellationToken cancellationToken) =>
			owner.GetUnsentMessagesAsync(batchSize, cancellationToken);

		public ValueTask MarkSentAsync(string messageId, CancellationToken cancellationToken) =>
			owner.MarkSentAsync(messageId, cancellationToken);

		public ValueTask MarkFailedAsync(string messageId, string errorMessage, int retryCount, CancellationToken cancellationToken) =>
			owner.MarkFailedAsync(messageId, errorMessage, retryCount, cancellationToken);
	}

	/// <summary>
	/// An <see cref="IMultiTransportOutboxStoreAdmin"/> view whose query results are decrypted before they reach
	/// the caller.
	/// </summary>
	private sealed class DecryptingMultiTransportAdmin(IMultiTransportOutboxStoreAdmin inner, EncryptingOutboxStoreDecorator owner)
		: IMultiTransportOutboxStoreAdmin
	{
		public async Task<IEnumerable<(OutboundMessage Message, OutboundMessageTransport Transport)>> GetPendingTransportDeliveriesAsync(
			string transportName,
			int batchSize,
			CancellationToken cancellationToken)
		{
			var deliveries = await inner.GetPendingTransportDeliveriesAsync(transportName, batchSize, cancellationToken).ConfigureAwait(false);
			return await owner.DecryptDeliveriesAsync(deliveries, cancellationToken).ConfigureAwait(false);
		}

		public async Task<IEnumerable<(OutboundMessage Message, OutboundMessageTransport Transport)>> GetFailedTransportDeliveriesAsync(
			string transportName,
			int maxRetries,
			DateTimeOffset? olderThan,
			int batchSize,
			CancellationToken cancellationToken)
		{
			var deliveries = await inner
				.GetFailedTransportDeliveriesAsync(transportName, maxRetries, olderThan, batchSize, cancellationToken)
				.ConfigureAwait(false);
			return await owner.DecryptDeliveriesAsync(deliveries, cancellationToken).ConfigureAwait(false);
		}

		public Task UpdateAggregateStatusAsync(string messageId, CancellationToken cancellationToken) =>
			inner.UpdateAggregateStatusAsync(messageId, cancellationToken);

		public Task<TransportDeliveryStatistics> GetTransportStatisticsAsync(string? transportName, CancellationToken cancellationToken) =>
			inner.GetTransportStatisticsAsync(transportName, cancellationToken);
	}

	/// <summary>
	/// An <see cref="IFencedOutboxStore"/> view that decrypts fenced reads and routes the members it inherits from
	/// <see cref="IOutboxStore"/> back through the decorator.
	/// </summary>
	private sealed class DecryptingFencedStore(IFencedOutboxStore inner, EncryptingOutboxStoreDecorator owner) : IFencedOutboxStore
	{
		public async ValueTask<IEnumerable<OutboundMessage>> GetUnsentMessagesAsync(
			int batchSize,
			long fencingToken,
			CancellationToken cancellationToken)
		{
			var messages = await inner.GetUnsentMessagesAsync(batchSize, fencingToken, cancellationToken).ConfigureAwait(false);
			return await owner.DecryptMessagesAsync(messages, cancellationToken).ConfigureAwait(false);
		}

		public ValueTask MarkSentAsync(string messageId, long fencingToken, CancellationToken cancellationToken) =>
			inner.MarkSentAsync(messageId, fencingToken, cancellationToken);

		public ValueTask StageMessageAsync(OutboundMessage message, CancellationToken cancellationToken) =>
			owner.StageMessageAsync(message, cancellationToken);

		public ValueTask EnqueueAsync(IDispatchMessage message, IMessageContext context, CancellationToken cancellationToken) =>
			owner.EnqueueAsync(message, context, cancellationToken);

		public ValueTask<IEnumerable<OutboundMessage>> GetUnsentMessagesAsync(int batchSize, CancellationToken cancellationToken) =>
			owner.GetUnsentMessagesAsync(batchSize, cancellationToken);

		public ValueTask MarkSentAsync(string messageId, CancellationToken cancellationToken) =>
			owner.MarkSentAsync(messageId, cancellationToken);

		public ValueTask MarkFailedAsync(string messageId, string errorMessage, int retryCount, CancellationToken cancellationToken) =>
			owner.MarkFailedAsync(messageId, errorMessage, retryCount, cancellationToken);
	}

	/// <summary>
	/// Encrypts a payload on its way to the decorated store, honouring the configured <see cref="EncryptionMode"/>.
	/// </summary>
	/// <exception cref="InvalidOperationException"> Thrown when the store is configured read-only. </exception>
	private async ValueTask<byte[]> EncryptForWriteAsync(byte[] payload, CancellationToken cancellationToken)
	{
		var mode = _options.Value.Mode;

		if (mode == EncryptionMode.DecryptOnlyReadOnly)
		{
			throw new InvalidOperationException(
				Resources.EncryptingOutboxStoreDecorator_ReadOnlyMode);
		}

		return mode is EncryptionMode.EncryptAndDecrypt or EncryptionMode.EncryptNewDecryptAll
			? await EncryptPayloadAsync(payload, cancellationToken).ConfigureAwait(false)
			: payload;
	}

	private async ValueTask<OutboundMessage> DecryptMessageAsync(OutboundMessage message, CancellationToken cancellationToken)
	{
		if (EncryptedData.IsFieldEncrypted(message.Payload))
		{
			message.Payload = await TryDecryptFieldAsync(message.Payload, cancellationToken).ConfigureAwait(false);
		}

		return message;
	}

	private async ValueTask<IEnumerable<(OutboundMessage Message, OutboundMessageTransport Transport)>> DecryptDeliveriesAsync(
		IEnumerable<(OutboundMessage Message, OutboundMessageTransport Transport)> deliveries,
		CancellationToken cancellationToken)
	{
		if (_options.Value.Mode == EncryptionMode.Disabled)
		{
			return deliveries;
		}

		var results = new List<(OutboundMessage, OutboundMessageTransport)>();
		foreach (var (message, transport) in deliveries)
		{
			results.Add((await DecryptMessageAsync(message, cancellationToken).ConfigureAwait(false), transport));
		}

		return results;
	}

	private async ValueTask<IEnumerable<OutboundMessage>> DecryptMessagesAsync(
		IEnumerable<OutboundMessage> messages,
		CancellationToken cancellationToken)
	{
		if (_options.Value.Mode == EncryptionMode.Disabled)
		{
			return messages;
		}

		var results = new List<OutboundMessage>();
		foreach (var message in messages)
		{
			results.Add(await DecryptMessageAsync(message, cancellationToken).ConfigureAwait(false));
		}

		return results;
	}

	private async ValueTask<byte[]> EncryptPayloadAsync(byte[] data, CancellationToken cancellationToken)
	{
		var provider = _registry.GetPrimary();
		var encryptedData = await provider.EncryptAsync(data, _defaultContext, cancellationToken).ConfigureAwait(false);
		return SerializeEncryptedData(encryptedData);
	}

	private async ValueTask<byte[]> TryDecryptFieldAsync(byte[] data, CancellationToken cancellationToken)
	{
		if (!EncryptedData.IsFieldEncrypted(data))
		{
			return data;
		}

		var encryptedData = DeserializeEncryptedData(data);
		var provider = _registry.FindDecryptionProvider(encryptedData)
			?? throw new EncryptionException(
				Resources.Encryption_NoProviderCanDecryptKeyRemoved);

		return await provider.DecryptAsync(encryptedData, _defaultContext, cancellationToken).ConfigureAwait(false);
	}
		private static byte[] SerializeEncryptedData(EncryptedData encryptedData)
	{
		var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(
			encryptedData,
			EncryptionJsonContext.Default.EncryptedData);
		var result = new byte[EncryptedData.MagicBytes.Length + jsonBytes.Length];
		EncryptedData.MagicBytes.CopyTo(result.AsSpan());
		jsonBytes.CopyTo(result, EncryptedData.MagicBytes.Length);
		return result;
	}

	private static EncryptedData DeserializeEncryptedData(byte[] data)
	{
		var envelopeData = data.AsSpan(EncryptedData.MagicBytes.Length);
		return JsonSerializer.Deserialize(
			envelopeData,
			EncryptionJsonContext.Default.EncryptedData)
			?? throw new EncryptionException(Resources.Encryption_EncryptedDataEnvelopeDeserializeFailed);
	}
}
