// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Compliance;

using Excalibur.EventSourcing.Abstractions;

using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.Encryption.Decorators;

/// <summary>
/// Decorates an <see cref="IEventStore"/> with transparent field-level encryption.
/// </summary>
/// <remarks>
/// <para>
/// This decorator provides mixed-mode read support during encryption migration.
/// On reads, it uses <see cref="EncryptedData.IsFieldEncrypted(byte[])"/> to detect encrypted data
/// and decrypts only when needed, allowing seamless handling of both plaintext and encrypted events.
/// </para>
/// <para>
/// On writes, the behavior is controlled by <see cref="EncryptionMode"/>:
/// <list type="bullet">
/// <item><see cref="EncryptionMode.EncryptAndDecrypt"/>: Encrypt all event data</item>
/// <item><see cref="EncryptionMode.EncryptNewDecryptAll"/>: Encrypt with primary provider</item>
/// <item><see cref="EncryptionMode.DecryptOnlyWritePlaintext"/>: Write plaintext (migration mode)</item>
/// <item><see cref="EncryptionMode.DecryptOnlyReadOnly"/>: Reject writes</item>
/// <item><see cref="EncryptionMode.Disabled"/>: Pass through without transformation</item>
/// </list>
/// </para>
/// </remarks>
public sealed class EncryptingEventStoreDecorator : IEventStore
{
	private readonly IEventStore _inner;
	private readonly IEncryptionProviderRegistry _registry;
	private readonly IOptions<EncryptionOptions> _options;
	private readonly EncryptionContext _defaultContext;

	/// <summary>
	/// Initializes a new instance of the <see cref="EncryptingEventStoreDecorator"/> class.
	/// </summary>
	/// <param name="inner">The underlying event store to decorate.</param>
	/// <param name="registry">The encryption provider registry for multi-provider support.</param>
	/// <param name="options">The encryption configuration options.</param>
	public EncryptingEventStoreDecorator(
		IEventStore inner,
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

	/// <inheritdoc/>
	public async ValueTask<IReadOnlyList<StoredEvent>> LoadAsync(
		string aggregateId,
		string aggregateType,
		CancellationToken cancellationToken)
	{
		var events = await _inner.LoadAsync(aggregateId, aggregateType, cancellationToken).ConfigureAwait(false);
		return await DecryptEventsAsync(events, cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public async ValueTask<IReadOnlyList<StoredEvent>> LoadAsync(
		string aggregateId,
		string aggregateType,
		long fromVersion,
		CancellationToken cancellationToken)
	{
		var events = await _inner.LoadAsync(aggregateId, aggregateType, fromVersion, cancellationToken).ConfigureAwait(false);
		return await DecryptEventsAsync(events, cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public async ValueTask<AppendResult> AppendAsync(
		string aggregateId,
		string aggregateType,
		IEnumerable<IDomainEvent> events,
		long expectedVersion,
		CancellationToken cancellationToken)
	{
		var mode = _options.Value.Mode;

		if (mode == EncryptionMode.DecryptOnlyReadOnly)
		{
			throw new InvalidOperationException(
				Resources.Encryption_ReadOnlyEventStore);
		}

		if (mode is EncryptionMode.Disabled or EncryptionMode.DecryptOnlyWritePlaintext)
		{
			return await _inner.AppendAsync(aggregateId, aggregateType, events, expectedVersion, cancellationToken)
				.ConfigureAwait(false);
		}

		// For EncryptAndDecrypt and EncryptNewDecryptAll, encryption is handled by the serializer layer
		// The event store decorator operates on StoredEvent, not IDomainEvent
		// Encryption of IDomainEvent payload happens during serialization before reaching the store
		return await _inner.AppendAsync(aggregateId, aggregateType, events, expectedVersion, cancellationToken)
			.ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public async ValueTask<IReadOnlyList<StoredEvent>> GetUndispatchedEventsAsync(
		int batchSize,
		CancellationToken cancellationToken)
	{
		var events = await _inner.GetUndispatchedEventsAsync(batchSize, cancellationToken).ConfigureAwait(false);
		return await DecryptEventsAsync(events, cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public ValueTask MarkEventAsDispatchedAsync(string eventId, CancellationToken cancellationToken)
	{
		return _inner.MarkEventAsDispatchedAsync(eventId, cancellationToken);
	}

	[UnconditionalSuppressMessage(
		"ReflectionAnalysis",
		"IL2026:RequiresUnreferencedCode",
		Justification =
			"Encryption envelope deserialization uses JsonSerializer for a known type at runtime.")]
	[UnconditionalSuppressMessage(
		"AOT",
		"IL3050:Using RequiresDynamicCode member in AOT",
		Justification =
			"Encryption envelope deserialization uses JsonSerializer for a known type at runtime.")]
	private static EncryptedData DeserializeEncryptedData(byte[] data)
	{
		// Skip magic bytes and deserialize the encrypted data envelope
		// The format is: [EXCR magic (4 bytes)][JSON envelope]
		var envelopeData = data.AsSpan(EncryptedData.MagicBytes.Length);
		return System.Text.Json.JsonSerializer.Deserialize<EncryptedData>(envelopeData)
			   ?? throw new EncryptionException(
				   Resources.Encryption_FailedToDeserializeEncryptedDataEnvelope);
	}

	private async ValueTask<IReadOnlyList<StoredEvent>> DecryptEventsAsync(
		IReadOnlyList<StoredEvent> events,
		CancellationToken cancellationToken)
	{
		var mode = _options.Value.Mode;

		if (mode == EncryptionMode.Disabled)
		{
			return events;
		}

		var results = new List<StoredEvent>(events.Count);

		foreach (var evt in events)
		{
			var decryptedEventData = await TryDecryptFieldAsync(evt.EventData, cancellationToken).ConfigureAwait(false);
			var decryptedMetadata = evt.Metadata is not null
				? await TryDecryptFieldAsync(evt.Metadata, cancellationToken).ConfigureAwait(false)
				: null;

			if (ReferenceEquals(decryptedEventData, evt.EventData) &&
				ReferenceEquals(decryptedMetadata, evt.Metadata))
			{
				results.Add(evt);
			}
			else
			{
				results.Add(evt with { EventData = decryptedEventData, Metadata = decryptedMetadata });
			}
		}

		return results;
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
						   Resources.Encryption_NoProviderCanDecrypt);

		return await provider.DecryptAsync(encryptedData, _defaultContext, cancellationToken).ConfigureAwait(false);
	}
}
