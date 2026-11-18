// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text.Json;

using Excalibur.Compliance.Configuration;
using Excalibur.Dispatch;

using Microsoft.Extensions.Options;

using System.Diagnostics.CodeAnalysis;

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
internal sealed partial class EncryptingOutboxStoreDecorator : IsolatingOutboxStoreDecorator
{
	/// <summary>
	/// The capabilities forwarded to the decorated store unmediated, because no member of either -- inherited
	/// members included -- accepts or returns a message payload.
	/// </summary>
	/// <remarks>
	/// <see cref="IDeadLetterableOutboxStore"/> and <see cref="IBackoffSchedulableOutboxStore"/> move message
	/// identifiers, reasons, and retry counts. <see cref="IFencedOutboxStoreDiagnostics"/> moves only a
	/// fencing high-water token -- an administrative sequence number, never message content.
	/// <see cref="IClaimScopedOutboxStore"/> moves identifiers, an error string, a retry count, a next-
	/// attempt time and a claim identity -- no payload on either member.
	/// <see cref="IFencedClaimScopedOutboxStore"/> moves the same, plus a fencing token -- an administrative
	/// sequence number, never message content. None of the five derives from
	/// <see cref="IOutboxStore"/>, so none inherits its payload-bearing surface. That is why the fenced
	/// <see cref="IFencedDeadLetterableOutboxStore"/> moves an identifier, a reason and a fencing token.
	/// The fenced
	/// claim-scoped contract deliberately declares NO base interfaces: deriving it from the fencing contract
	/// would have pulled the payload-bearing surface in transitively and forced it out of this set, and a
	/// capability denied here is silently absent for precisely the consumers who enabled encryption. Every
	/// other capability is denied or wrapped.
	/// <para>
	/// <see cref="IOutboxStoreCapabilities"/> carries a single boolean and no base interface, so it clears the
	/// test above trivially. <b>It was absent, and its absence did the specific harm the sentence above
	/// predicts.</b> The capability answers whether the store tracks sent messages or deletes them on send;
	/// denied, a delete-on-sent store became indistinguishable from a sent-tracking one for any consumer who
	/// enabled crypto-shredding, so callers and the conformance kit went looking for a row that store had
	/// deleted. A capability describing the store's own SHAPE is exactly what a decorator must not silently
	/// answer for, because it reports on the thing underneath rather than on anything the decorator does.
	/// </para>
	/// </remarks>
	private static readonly HashSet<Type> ForwardableCapabilitySet =
	[
		typeof(IDeadLetterableOutboxStore),
		typeof(IBackoffSchedulableOutboxStore),
		typeof(IFencedOutboxStoreDiagnostics),
		typeof(IClaimScopedOutboxStore),
		typeof(IClaimScopedDeadLetterableOutboxStore),
		typeof(IFencedClaimScopedOutboxStore),
		typeof(IFencedDeadLetterableOutboxStore),
		typeof(IOutboxStoreCapabilities)
	];

	private readonly IEncryptionProviderRegistry _registry;
	private readonly IOptions<EncryptionOptions> _options;

	/// <summary>
	/// Initializes a new instance of the <see cref="EncryptingOutboxStoreDecorator" /> class.
	/// </summary>
	/// <param name="inner"> The underlying outbox store to decorate. </param>
	/// <param name="registry"> The encryption provider registry for multi-provider support. </param>
	/// <param name="options"> The encryption configuration options. </param>
	/// <remarks>
	/// The tenant bound into each payload's Additional Authenticated Data is the tenant the MESSAGE
	/// carries, never the ambient tenant: the outbox is drained for every tenant from outside any tenant
	/// scope, so only the record's own tenant can be re-derived at drain time.
	/// </remarks>
	public EncryptingOutboxStoreDecorator(
		IOutboxStore inner,
		IEncryptionProviderRegistry registry,
		IOptions<EncryptionOptions> options)
		: base(inner)
	{
		_registry = registry ?? throw new ArgumentNullException(nameof(registry));
		_options = options ?? throw new ArgumentNullException(nameof(options));
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
        if (serviceType == typeof(ITransactionalOutboxPreparer))
        {
            return Inner.GetService(typeof(ITransactionalOutboxPreparer)) is ITransactionalOutboxPreparer prepare
                && Inner.GetService(typeof(ITransactionalOutboxPayloadTransformPreparer)) is ITransactionalOutboxPayloadTransformPreparer transform
                ? new EncryptedTransactionalPreparation(prepare, transform, this) : null;
        }
        // The raw transform facet is not forwarded: that would bypass this encryption layer.
        if (serviceType == typeof(ITransactionalOutboxPayloadTransformPreparer)) { return null; }

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

		var mode = GetWriteMode();
		if (mode is EncryptionMode.Disabled or EncryptionMode.DecryptOnlyWritePlaintext)
		{
			await Inner.StageMessageAsync(message, cancellationToken).ConfigureAwait(false);
			return;
		}

		// Encrypt an owned snapshot, never a payload temporarily installed on the caller's envelope.
		var owned = SnapshotMessage(message);
		owned.Payload = await EncryptPayloadAsync(owned.Payload, owned.TenantId, cancellationToken).ConfigureAwait(false);
		await Inner.StageMessageAsync(owned, cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc />
	[RequiresUnreferencedCode("Outbox stores serialize the message payload reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Outbox stores serialize the message payload reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	public override async ValueTask EnqueueAsync(IDispatchMessage message, IMessageContext context, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentNullException.ThrowIfNull(context);
		var mode = GetWriteMode();
		if (mode is EncryptionMode.Disabled or EncryptionMode.DecryptOnlyWritePlaintext)
		{
			await Inner.EnqueueAsync(message, context, cancellationToken).ConfigureAwait(false);
			return;
		}

		var transform = Inner.GetService(typeof(IOutboxPayloadTransformStore)) as IOutboxPayloadTransformStore
			?? throw new InvalidOperationException(
				"Encrypted legacy EnqueueAsync requires the owning outbox provider's IOutboxPayloadTransformStore capability. "
				+ "Use a supporting provider or serialize with the provider's contract and call StageMessageAsync; plaintext fallback is refused.");
		await transform.EnqueueWithPayloadTransformAsync(message, context, EncryptEnqueuePayloadAsync, cancellationToken).ConfigureAwait(false);
	}

	private ValueTask<byte[]> EncryptEnqueuePayloadAsync(ReadOnlyMemory<byte> payload, string? tenantId, CancellationToken cancellationToken) =>
		EncryptPayloadAsync(payload.ToArray(), tenantId, cancellationToken);

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
		public async ValueTask<IEnumerable<OutboundMessage>> GetAllTenantsFailedMessagesAsync(
			int maxRetries,
			DateTimeOffset? olderThan,
			int batchSize,
			CancellationToken cancellationToken)
		{
			var messages = await inner.GetAllTenantsFailedMessagesAsync(maxRetries, olderThan, batchSize, cancellationToken).ConfigureAwait(false);
			return await owner.DecryptMessagesAsync(messages, cancellationToken).ConfigureAwait(false);
		}

		public async ValueTask<IEnumerable<OutboundMessage>> GetAllTenantsScheduledMessagesAsync(
			DateTimeOffset scheduledBefore,
			int batchSize,
			CancellationToken cancellationToken)
		{
			var messages = await inner.GetAllTenantsScheduledMessagesAsync(scheduledBefore, batchSize, cancellationToken).ConfigureAwait(false);
			return await owner.DecryptMessagesAsync(messages, cancellationToken).ConfigureAwait(false);
		}

		public ValueTask<int> CleanupAllTenantsSentMessagesAsync(DateTimeOffset olderThan, int batchSize, CancellationToken cancellationToken) =>
			inner.CleanupAllTenantsSentMessagesAsync(olderThan, batchSize, cancellationToken);

		public ValueTask<OutboxStatistics> GetAllTenantsStatisticsAsync(CancellationToken cancellationToken) =>
			inner.GetAllTenantsStatisticsAsync(cancellationToken);
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

			var mode = owner.GetWriteMode();
			if (mode is EncryptionMode.Disabled or EncryptionMode.DecryptOnlyWritePlaintext)
			{
				return await inner.TryMarkSentAndReceivedAsync(messageId, inboxEntry, cancellationToken).ConfigureAwait(false);
			}

			var owned = SnapshotInboxEntry(inboxEntry);
			owned.Payload = await owner.EncryptPayloadAsync(owned.Payload, owned.TenantId, cancellationToken).ConfigureAwait(false);
			return await inner.TryMarkSentAndReceivedAsync(messageId, owned, cancellationToken).ConfigureAwait(false);
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

			var mode = owner.GetWriteMode();
			if (mode is EncryptionMode.Disabled or EncryptionMode.DecryptOnlyWritePlaintext)
			{
				await inner.StageMessageWithTransportsAsync(message, transports, cancellationToken).ConfigureAwait(false);
				return;
			}

			// Both the embedded deliveries and this independent sequence are fixed before encryption awaits.
			var owned = SnapshotMessage(message);
			var ownedTransports = transports.Select(SnapshotTransport).ToArray();
			owned.Payload = await owner.EncryptPayloadAsync(owned.Payload, owned.TenantId, cancellationToken).ConfigureAwait(false);
			await inner.StageMessageWithTransportsAsync(owned, ownedTransports, cancellationToken).ConfigureAwait(false);
		}

		// Carries transport identity and status, never a payload.
		public Task<IEnumerable<OutboundMessageTransport>> GetTransportDeliveriesAsync(string messageId, string? tenantId, CancellationToken cancellationToken) =>
			inner.GetTransportDeliveriesAsync(messageId, tenantId, cancellationToken);

		public Task MarkTransportSentAsync(string messageId, string transportName, CancellationToken cancellationToken) =>
			inner.MarkTransportSentAsync(messageId, transportName, cancellationToken);

		public Task MarkTransportFailedAsync(string messageId, string transportName, string errorMessage, CancellationToken cancellationToken) =>
			inner.MarkTransportFailedAsync(messageId, transportName, errorMessage, cancellationToken);

		public Task MarkTransportSkippedAsync(string messageId, string transportName, string? reason, CancellationToken cancellationToken) =>
			inner.MarkTransportSkippedAsync(messageId, transportName, reason, cancellationToken);

		public ValueTask StageMessageAsync(OutboundMessage message, CancellationToken cancellationToken) =>
			owner.StageMessageAsync(message, cancellationToken);

		[RequiresUnreferencedCode("Outbox stores serialize the message payload reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
		[RequiresDynamicCode("Outbox stores serialize the message payload reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
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
		// Carries transport identity and status, never a payload.
		public Task<IEnumerable<OutboundMessageTransport>> GetAllTenantsTransportDeliveriesAsync(string messageId, CancellationToken cancellationToken) =>
			inner.GetAllTenantsTransportDeliveriesAsync(messageId, cancellationToken);

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

		public ValueTask<OutboxCompletionOutcome> MarkSentAsync(string messageId, long fencingToken, CancellationToken cancellationToken) =>
			inner.MarkSentAsync(messageId, fencingToken, cancellationToken);

		public ValueTask StageMessageAsync(OutboundMessage message, CancellationToken cancellationToken) =>
			owner.StageMessageAsync(message, cancellationToken);

		[RequiresUnreferencedCode("Outbox stores serialize the message payload reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
		[RequiresDynamicCode("Outbox stores serialize the message payload reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
		public ValueTask EnqueueAsync(IDispatchMessage message, IMessageContext context, CancellationToken cancellationToken) =>
			owner.EnqueueAsync(message, context, cancellationToken);

		public ValueTask<IEnumerable<OutboundMessage>> GetUnsentMessagesAsync(int batchSize, CancellationToken cancellationToken) =>
			owner.GetUnsentMessagesAsync(batchSize, cancellationToken);

		public ValueTask MarkSentAsync(string messageId, CancellationToken cancellationToken) =>
			owner.MarkSentAsync(messageId, cancellationToken);

		public ValueTask MarkFailedAsync(string messageId, string errorMessage, int retryCount, CancellationToken cancellationToken) =>
			owner.MarkFailedAsync(messageId, errorMessage, retryCount, cancellationToken);
	}

	/// <summary>Captures and validates one write's mode before touching mutable caller data.</summary>
	private EncryptionMode GetWriteMode()
	{
		var mode = _options.Value.Mode;
		return mode switch
		{
			EncryptionMode.EncryptAndDecrypt or EncryptionMode.EncryptNewDecryptAll
				or EncryptionMode.DecryptOnlyWritePlaintext or EncryptionMode.Disabled => mode,
			EncryptionMode.DecryptOnlyReadOnly => throw new InvalidOperationException(Resources.EncryptingOutboxStoreDecorator_ReadOnlyMode),
			_ => throw new InvalidOperationException(
				$"Unsupported outbox encryption mode '{(int)mode}'. Configure a declared EncryptionMode before writing."),
		};
	}

	// Copy containers and payload bytes, retaining arbitrary nested metadata values and serializer semantics.
	// Direct assignment preserves IDs/status/explicit correlation values; convenience constructors or
	// AddTransport would derive fields rather than snapshot the caller's current values.
	private static OutboundMessage SnapshotMessage(OutboundMessage message)
	{
		var owned = new OutboundMessage
		{
			Id = message.Id,
			MessageType = message.MessageType,
			Payload = (byte[])message.Payload.Clone(),
			Headers = new Dictionary<string, object>(message.Headers, StringComparer.Ordinal),
			Destination = message.Destination,
			CreatedAt = message.CreatedAt,
			ScheduledAt = message.ScheduledAt,
			SentAt = message.SentAt,
			Status = message.Status,
			RetryCount = message.RetryCount,
			LastError = message.LastError,
			LastAttemptAt = message.LastAttemptAt,
			DispatcherId = message.DispatcherId,
			CorrelationId = message.CorrelationId,
			CausationId = message.CausationId,
			TenantId = message.TenantId,
			Priority = message.Priority,
			PartitionKey = message.PartitionKey,
			GroupKey = message.GroupKey,
			SequenceNumber = message.SequenceNumber,
			TargetTransports = message.TargetTransports,
			IsMultiTransport = message.IsMultiTransport,
		};
		foreach (var delivery in message.TransportDeliveries)
		{
			owned.TransportDeliveries.Add(SnapshotTransport(delivery));
		}
		return owned;
	}

	private static InboxEntry SnapshotInboxEntry(InboxEntry entry) => new()
	{
		MessageId = entry.MessageId,
		HandlerType = entry.HandlerType,
		MessageType = entry.MessageType,
		Payload = (byte[])entry.Payload.Clone(),
		Metadata = new Dictionary<string, object>(entry.Metadata, StringComparer.Ordinal),
		ReceivedAt = entry.ReceivedAt,
		ProcessedAt = entry.ProcessedAt,
		Status = entry.Status,
		LastError = entry.LastError,
		RetryCount = entry.RetryCount,
		LastAttemptAt = entry.LastAttemptAt,
		NextAttemptAt = entry.NextAttemptAt,
		CorrelationId = entry.CorrelationId,
		CausationId = entry.CausationId,
		TenantId = entry.TenantId,
		Source = entry.Source,
	};

	private static OutboundMessageTransport SnapshotTransport(OutboundMessageTransport delivery) => new()
	{
		Id = delivery.Id,
		MessageId = delivery.MessageId,
		TransportName = delivery.TransportName,
		Destination = delivery.Destination,
		Status = delivery.Status,
		CreatedAt = delivery.CreatedAt,
		AttemptedAt = delivery.AttemptedAt,
		SentAt = delivery.SentAt,
		RetryCount = delivery.RetryCount,
		LastError = delivery.LastError,
		TransportMetadata = delivery.TransportMetadata,
	};

	private async ValueTask<OutboundMessage> DecryptMessageAsync(OutboundMessage message, CancellationToken cancellationToken)
	{
		if (EncryptedData.IsFieldEncrypted(message.Payload))
		{
			message.Payload = await TryDecryptFieldAsync(message.Payload, message.TenantId, cancellationToken).ConfigureAwait(false);
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

	private async ValueTask<byte[]> EncryptPayloadAsync(byte[] data, string? tenantId, CancellationToken cancellationToken)
	{
		var provider = _registry.GetPrimary();
		var encryptedData = await provider.EncryptAsync(data, ContextFor(tenantId), cancellationToken).ConfigureAwait(false);
		return SerializeEncryptedData(encryptedData);
	}

	private async ValueTask<byte[]> TryDecryptFieldAsync(byte[] data, string? tenantId, CancellationToken cancellationToken)
	{
		if (!EncryptedData.IsFieldEncrypted(data))
		{
			return data;
		}

		var encryptedData = DeserializeEncryptedData(data);
		var provider = _registry.FindDecryptionProvider(encryptedData)
			?? throw new EncryptionException(
				Resources.Encryption_NoProviderCanDecryptKeyRemoved);

		return await provider.DecryptAsync(encryptedData, ContextFor(tenantId), cancellationToken).ConfigureAwait(false);
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

	/// <summary>
	/// Builds the encryption context for one record, binding the tenant that RECORD belongs to.
	/// </summary>
	/// <remarks>
	/// The tenant is normalised through the stores' own read-back conversion, so a message staged with no
	/// tenant and drained carrying the untenanted sentinel binds the same value on both sides. A different
	/// tenant on the row than the one the ciphertext was written under fails the tag check.
	/// </remarks>
	private EncryptionContext ContextFor(string? tenantId) => new()
	{
		Purpose = _options.Value.DefaultPurpose,
		TenantId = KeyedTenantPartition.FromStoredValue(tenantId).TenantId,
		RequireFipsCompliance = _options.Value.RequireFipsCompliance
	};
}
