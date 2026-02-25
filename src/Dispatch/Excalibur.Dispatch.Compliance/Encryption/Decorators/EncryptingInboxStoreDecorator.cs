// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR
// AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json;

using Excalibur.Dispatch.Abstractions;

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Decorates an <see cref="IInboxStore" /> with transparent field-level encryption.
/// </summary>
/// <remarks>
/// <para>
/// This decorator provides mixed-mode read support during encryption migration. On reads, it uses
/// <see cref="EncryptedData.IsFieldEncrypted(byte[])" /> to detect encrypted data and decrypts only when needed, allowing seamless handling
/// of both plaintext and encrypted messages.
/// </para>
/// </remarks>
public sealed class EncryptingInboxStoreDecorator : IInboxStore
{
	private readonly IInboxStore _inner;
	private readonly IEncryptionProviderRegistry _registry;
	private readonly IOptions<EncryptionOptions> _options;
	private readonly EncryptionContext _defaultContext;

	/// <summary>
	/// Initializes a new instance of the <see cref="EncryptingInboxStoreDecorator" /> class.
	/// </summary>
	/// <param name="inner"> The underlying inbox store to decorate. </param>
	/// <param name="registry"> The encryption provider registry for multi-provider support. </param>
	/// <param name="options"> The encryption configuration options. </param>
	public EncryptingInboxStoreDecorator(
		IInboxStore inner,
		IEncryptionProviderRegistry registry,
		IOptions<EncryptionOptions> options)
	{
		_inner = inner ?? throw new ArgumentNullException(nameof(inner));
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
	public async ValueTask<InboxEntry> CreateEntryAsync(
		string messageId,
		string handlerType,
		string messageType,
		byte[] payload,
		IDictionary<string, object> metadata,
		CancellationToken cancellationToken)
	{
		var mode = _options.Value.Mode;

		if (mode == EncryptionMode.DecryptOnlyReadOnly)
		{
			throw new InvalidOperationException(
				Resources.EncryptingInboxStoreDecorator_ReadOnlyMode);
		}

		var processedPayload = mode is EncryptionMode.EncryptAndDecrypt or EncryptionMode.EncryptNewDecryptAll
			? await EncryptPayloadAsync(payload, cancellationToken).ConfigureAwait(false)
			: payload;

		return await _inner.CreateEntryAsync(messageId, handlerType, messageType, processedPayload, metadata, cancellationToken)
			.ConfigureAwait(false);
	}

	/// <inheritdoc />
	public ValueTask MarkProcessedAsync(string messageId, string handlerType, CancellationToken cancellationToken)
	{
		return _inner.MarkProcessedAsync(messageId, handlerType, cancellationToken);
	}

	/// <inheritdoc />
	public ValueTask<bool> TryMarkAsProcessedAsync(string messageId, string handlerType, CancellationToken cancellationToken)
	{
		return _inner.TryMarkAsProcessedAsync(messageId, handlerType, cancellationToken);
	}

	/// <inheritdoc />
	public ValueTask<bool> IsProcessedAsync(string messageId, string handlerType, CancellationToken cancellationToken)
	{
		return _inner.IsProcessedAsync(messageId, handlerType, cancellationToken);
	}

	/// <inheritdoc />
	public async ValueTask<InboxEntry?> GetEntryAsync(string messageId, string handlerType, CancellationToken cancellationToken)
	{
		var entry = await _inner.GetEntryAsync(messageId, handlerType, cancellationToken).ConfigureAwait(false);
		if (entry is null)
		{
			return null;
		}

		return await DecryptEntryAsync(entry, cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public ValueTask MarkFailedAsync(string messageId, string handlerType, string errorMessage, CancellationToken cancellationToken)
	{
		return _inner.MarkFailedAsync(messageId, handlerType, errorMessage, cancellationToken);
	}

	/// <inheritdoc />
	public async ValueTask<IEnumerable<InboxEntry>> GetFailedEntriesAsync(
		int maxRetries,
		DateTimeOffset? olderThan,
		int batchSize,
		CancellationToken cancellationToken)
	{
		var entries = await _inner.GetFailedEntriesAsync(maxRetries, olderThan, batchSize, cancellationToken)
			.ConfigureAwait(false);
		return await DecryptEntriesAsync(entries, cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async ValueTask<IEnumerable<InboxEntry>> GetAllEntriesAsync(CancellationToken cancellationToken)
	{
		var entries = await _inner.GetAllEntriesAsync(cancellationToken).ConfigureAwait(false);
		return await DecryptEntriesAsync(entries, cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public ValueTask<InboxStatistics> GetStatisticsAsync(CancellationToken cancellationToken)
	{
		return _inner.GetStatisticsAsync(cancellationToken);
	}

	/// <inheritdoc />
	public ValueTask<int> CleanupAsync(TimeSpan retentionPeriod, CancellationToken cancellationToken)
	{
		return _inner.CleanupAsync(retentionPeriod, cancellationToken);
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

	private async ValueTask<IEnumerable<InboxEntry>> DecryptEntriesAsync(
		IEnumerable<InboxEntry> entries,
		CancellationToken cancellationToken)
	{
		var results = new List<InboxEntry>();
		foreach (var entry in entries)
		{
			results.Add(await DecryptEntryAsync(entry, cancellationToken).ConfigureAwait(false));
		}

		return results;
	}

	private async ValueTask<InboxEntry> DecryptEntryAsync(InboxEntry entry, CancellationToken cancellationToken)
	{
		var mode = _options.Value.Mode;

		if (mode == EncryptionMode.Disabled)
		{
			return entry;
		}

		if (!EncryptedData.IsFieldEncrypted(entry.Payload))
		{
			return entry;
		}

		var decryptedPayload = await TryDecryptFieldAsync(entry.Payload, cancellationToken).ConfigureAwait(false);
		entry.Payload = decryptedPayload;
		return entry;
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
