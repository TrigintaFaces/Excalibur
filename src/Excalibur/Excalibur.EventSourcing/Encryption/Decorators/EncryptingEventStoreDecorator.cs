// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Compliance;
using Excalibur.Compliance.Configuration;
using Excalibur.Compliance.CryptoShredding;
using Excalibur.Dispatch;

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
public sealed class EncryptingEventStoreDecorator : IEventStore, IEventStoreErasure
{
	private readonly IEventStore _inner;
	private readonly IEncryptionProviderRegistry _registry;
	private readonly IOptions<EncryptionOptions> _options;
	private readonly SubjectFieldCryptor _subjectFieldCryptor;
	private readonly IEventSerializer _eventSerializer;
	private readonly EncryptionContext _defaultContext;

	/// <summary>
	/// Initializes a new instance of the <see cref="EncryptingEventStoreDecorator"/> class.
	/// </summary>
	/// <param name="inner">The underlying event store to decorate.</param>
	/// <param name="registry">The encryption provider registry for multi-provider support.</param>
	/// <param name="subjectFieldCryptor">
	/// The per-subject field encryptor that protects <c>[PersonalData]</c> fields of a data-subject event under the
	/// subject's own key, so destroying that key crypto-shreds only that subject's personal fields (GDPR erasure).
	/// </param>
	/// <param name="eventSerializer">
	/// The event serializer used to materialize a stored event back into its domain event on load, so its personal
	/// fields can be decrypted, and to resolve the event's runtime type from its stored name.
	/// </param>
	/// <param name="options">The encryption configuration options.</param>
	public EncryptingEventStoreDecorator(
		IEventStore inner,
		IEncryptionProviderRegistry registry,
		SubjectFieldCryptor subjectFieldCryptor,
		IEventSerializer eventSerializer,
		IOptions<EncryptionOptions> options)
	{
		_inner = inner ?? throw new ArgumentNullException(nameof(inner));
		_registry = registry ?? throw new ArgumentNullException(nameof(registry));
		_subjectFieldCryptor = subjectFieldCryptor ?? throw new ArgumentNullException(nameof(subjectFieldCryptor));
		_eventSerializer = eventSerializer ?? throw new ArgumentNullException(nameof(eventSerializer));
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

		// EncryptAndDecrypt / EncryptNewDecryptAll: encrypt event data before writing.
		// AppendAsync receives IDomainEvent (pre-serialization), so we encrypt the serialized
		// field bytes inline on each event using the registered encryption provider.
		var encryptedEvents = await EncryptEventsAsync(events, cancellationToken).ConfigureAwait(false);
		return await _inner.AppendAsync(aggregateId, aggregateType, encryptedEvents, expectedVersion, cancellationToken)
			.ConfigureAwait(false);
	}

	/// <inheritdoc/>
	/// <remarks>
	/// Erasure tombstones the stored event rows and is orthogonal to field encryption: the erase operates on the
	/// rows this decorator wrote through its inner store, so it forwards to the inner store's erasure capability
	/// unchanged. (Per-subject crypto-shredding — destroying a subject's key so their <c>[PersonalData]</c> fields
	/// decrypt to a tombstone — is a separate GDPR mechanism handled on the read path, not by this tombstoning erase.)
	/// </remarks>
	public Task<int> EraseEventsAsync(
		string aggregateId,
		string aggregateType,
		Guid erasureRequestId,
		CancellationToken cancellationToken)
		=> RequireInnerErasure().EraseEventsAsync(aggregateId, aggregateType, erasureRequestId, cancellationToken);

	/// <inheritdoc/>
	public Task<bool> IsErasedAsync(
		string aggregateId,
		string aggregateType,
		CancellationToken cancellationToken)
		=> RequireInnerErasure().IsErasedAsync(aggregateId, aggregateType, cancellationToken);

	// A decorator can only forward the capability the store it wraps supports; if the inner store does not
	// implement IEventStoreErasure, surface that explicitly rather than silently stripping erasure.
	private IEventStoreErasure RequireInnerErasure()
		=> _inner as IEventStoreErasure
			?? throw new NotSupportedException(
				$"The inner event store ({_inner.GetType().Name}) does not support GDPR erasure (IEventStoreErasure).");

	private async ValueTask<IEnumerable<IDomainEvent>> EncryptEventsAsync(
		IEnumerable<IDomainEvent> events,
		CancellationToken cancellationToken)
	{
		// Per-subject field encryption: encrypt each [PersonalData] field of a data-subject event under that
		// subject's own key, in place, BEFORE the inner store serializes it — so the bytes reach the store with
		// the subject's personal fields already ciphertext ("at rest"), and destroying the subject's key renders
		// only that subject's fields unrecoverable (GDPR crypto-shred) while non-personal structure stays readable.
		// An event with no [DataSubjectId] is left untouched (the cryptor no-ops), so non-personal events flow through.
		var result = new List<IDomainEvent>();
		foreach (var evt in events)
		{
			await _subjectFieldCryptor.EncryptFieldsAsync(evt, cancellationToken).ConfigureAwait(false);
			result.Add(evt);
		}

		return result;
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
			// 1. Legacy whole-blob decrypt (mixed-mode migration: data written by the whole-blob provider path).
			var decryptedEventData = await TryDecryptFieldAsync(evt.EventData, cancellationToken).ConfigureAwait(false);
			var decryptedMetadata = evt.Metadata is not null
				? await TryDecryptFieldAsync(evt.Metadata, cancellationToken).ConfigureAwait(false)
				: null;

			var current = ReferenceEquals(decryptedEventData, evt.EventData) && ReferenceEquals(decryptedMetadata, evt.Metadata)
				? evt
				: evt with { EventData = decryptedEventData, Metadata = decryptedMetadata };

			// 2. Per-subject field decrypt: materialize the domain event and decrypt its [PersonalData] fields under
			// the subject's key. A field whose subject key was destroyed decrypts to a null tombstone, leaving the
			// rest of the event intact (so the aggregate still loads after erasure).
			current = await DecryptSubjectFieldsAsync(current, cancellationToken).ConfigureAwait(false);

			results.Add(current);
		}

		return results;
	}

	private async ValueTask<StoredEvent> DecryptSubjectFieldsAsync(StoredEvent stored, CancellationToken cancellationToken)
	{
		// Resolve + materialize the event so its [PersonalData] fields can be decrypted in place. An event whose type
		// cannot be resolved/deserialized was not written through this decorator's field-encryption path, so it is
		// returned unchanged rather than failing the load.
		IDomainEvent? domainEvent;
		try
		{
			var eventType = _eventSerializer.ResolveType(stored.EventType);
			if (eventType is null)
			{
				return stored;
			}

			domainEvent = _eventSerializer.DeserializeEvent(stored.EventData, eventType);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			return stored;
		}

		// An unresolvable/undeserializable event was not written through this decorator's field-encryption
		// path, so there is nothing to decrypt — return it unchanged rather than materializing a null.
		if (domainEvent is null)
		{
			return stored;
		}

		await _subjectFieldCryptor.DecryptFieldsAsync(domainEvent, cancellationToken).ConfigureAwait(false);

		var reserialized = _eventSerializer.SerializeEvent(domainEvent);
		return stored with { EventData = reserialized };
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
