// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR
// AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json;

using Excalibur.Dispatch.Abstractions;

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Compliance;

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
public sealed class EncryptingOutboxStoreDecorator : IOutboxStore, IOutboxStoreAdmin
{
	private readonly IOutboxStore _inner;
	private readonly IOutboxStoreAdmin? _innerAdmin;
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
	{
		_inner = inner ?? throw new ArgumentNullException(nameof(inner));
		_innerAdmin = inner as IOutboxStoreAdmin;
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
	public async ValueTask StageMessageAsync(OutboundMessage message, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);

		var mode = _options.Value.Mode;

		if (mode == EncryptionMode.DecryptOnlyReadOnly)
		{
			throw new InvalidOperationException(
				Resources.EncryptingOutboxStoreDecorator_ReadOnlyMode);
		}

		if (mode is EncryptionMode.EncryptAndDecrypt or EncryptionMode.EncryptNewDecryptAll)
		{
			message.Payload = await EncryptPayloadAsync(message.Payload, cancellationToken).ConfigureAwait(false);
		}

		await _inner.StageMessageAsync(message, cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async ValueTask EnqueueAsync(IDispatchMessage message, IMessageContext context, CancellationToken cancellationToken)
	{
		var mode = _options.Value.Mode;

		if (mode == EncryptionMode.DecryptOnlyReadOnly)
		{
			throw new InvalidOperationException(
				Resources.EncryptingOutboxStoreDecorator_ReadOnlyMode);
		}

		// Encryption of the message payload happens at serialization time, not here
		await _inner.EnqueueAsync(message, context, cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async ValueTask<IEnumerable<OutboundMessage>> GetUnsentMessagesAsync(int batchSize, CancellationToken cancellationToken)
	{
		var messages = await _inner.GetUnsentMessagesAsync(batchSize, cancellationToken).ConfigureAwait(false);
		return await DecryptMessagesAsync(messages, cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public ValueTask MarkSentAsync(string messageId, CancellationToken cancellationToken)
	{
		return _inner.MarkSentAsync(messageId, cancellationToken);
	}

	/// <inheritdoc />
	public ValueTask MarkFailedAsync(string messageId, string errorMessage, int retryCount, CancellationToken cancellationToken)
	{
		return _inner.MarkFailedAsync(messageId, errorMessage, retryCount, cancellationToken);
	}

	/// <inheritdoc />
	public async ValueTask<IEnumerable<OutboundMessage>> GetFailedMessagesAsync(
		int maxRetries,
		DateTimeOffset? olderThan,
		int batchSize,
		CancellationToken cancellationToken)
	{
		if (_innerAdmin is null)
		{
			return [];
		}

		var messages = await _innerAdmin.GetFailedMessagesAsync(maxRetries, olderThan, batchSize, cancellationToken)
			.ConfigureAwait(false);
		return await DecryptMessagesAsync(messages, cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async ValueTask<IEnumerable<OutboundMessage>> GetScheduledMessagesAsync(
		DateTimeOffset scheduledBefore,
		int batchSize,
		CancellationToken cancellationToken)
	{
		if (_innerAdmin is null)
		{
			return [];
		}

		var messages = await _innerAdmin.GetScheduledMessagesAsync(scheduledBefore, batchSize, cancellationToken)
			.ConfigureAwait(false);
		return await DecryptMessagesAsync(messages, cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public ValueTask<int> CleanupSentMessagesAsync(DateTimeOffset olderThan, int batchSize, CancellationToken cancellationToken)
	{
		return _innerAdmin?.CleanupSentMessagesAsync(olderThan, batchSize, cancellationToken)
			?? ValueTask.FromResult(0);
	}

	/// <inheritdoc />
	public ValueTask<OutboxStatistics> GetStatisticsAsync(CancellationToken cancellationToken)
	{
		return _innerAdmin?.GetStatisticsAsync(cancellationToken)
			?? ValueTask.FromResult(new OutboxStatistics());
	}

	/// <inheritdoc />
	public ValueTask<bool> TryMarkSentAndReceivedAsync(string messageId, InboxEntry inboxEntry, CancellationToken cancellationToken)
	{
		return _inner.TryMarkSentAndReceivedAsync(messageId, inboxEntry, cancellationToken);
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

	private async ValueTask<IEnumerable<OutboundMessage>> DecryptMessagesAsync(
		IEnumerable<OutboundMessage> messages,
		CancellationToken cancellationToken)
	{
		var mode = _options.Value.Mode;

		if (mode == EncryptionMode.Disabled)
		{
			return messages;
		}

		var results = new List<OutboundMessage>();
		foreach (var message in messages)
		{
			if (EncryptedData.IsFieldEncrypted(message.Payload))
			{
				message.Payload = await TryDecryptFieldAsync(message.Payload, cancellationToken).ConfigureAwait(false);
			}

			results.Add(message);
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
}
