// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data;
using System.Diagnostics.CodeAnalysis;

using Excalibur.Compliance;
using Excalibur.Compliance.Configuration;
using Excalibur.Compliance.CryptoShredding;
using Excalibur.Dispatch;
using Excalibur.EventSourcing.Decorators;

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
public sealed class EncryptingEventStoreDecorator : IsolatingEventStoreDecorator, IEventStoreErasure
{
	private readonly IEventStore _inner;
	private readonly IEncryptionProviderRegistry _registry;
	private readonly IOptions<EncryptionOptions> _options;
	private readonly SubjectFieldCryptor _subjectFieldCryptor;
	private readonly IEventSerializer _eventSerializer;
	/// <summary>
	/// Resolves the tenant of the operation in flight. Consulted PER CALL, never captured.
	/// </summary>
	/// <remarks>
	/// The AES-GCM provider binds this into the Additional Authenticated Data, and its own comment
	/// names cross-tenant decryption as the thing that prevents. Stamping it once at construction from
	/// a process-wide option made every record in a multi-tenant host carry the SAME tenant, so the
	/// component advertised as the cross-tenant control contributed nothing on exactly the paths that
	/// encrypt stored data. A construction-time context cannot carry a per-operation value, so the
	/// cached field is removed rather than corrected -- a constant stamp now has nowhere to live.
	/// </remarks>
	private readonly ITenantContext _tenantContext;

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
	/// <param name="tenantContext">
	/// Resolves the tenant each operation runs as, so the AAD binds the DATA's tenant rather than a
	/// process-wide constant. Required: a single-tenant host receives the framework's single-tenant default.
	/// </param>
	public EncryptingEventStoreDecorator(
		IEventStore inner,
		IEncryptionProviderRegistry registry,
		SubjectFieldCryptor subjectFieldCryptor,
		IEventSerializer eventSerializer,
		IOptions<EncryptionOptions> options,
		ITenantContext tenantContext)
		: base(inner)
	{
		_inner = Inner;
		_registry = registry ?? throw new ArgumentNullException(nameof(registry));
		_subjectFieldCryptor = subjectFieldCryptor ?? throw new ArgumentNullException(nameof(subjectFieldCryptor));
		_eventSerializer = eventSerializer ?? throw new ArgumentNullException(nameof(eventSerializer));
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
	}

	/// <inheritdoc/>
	public override async ValueTask<IReadOnlyList<StoredEvent>> LoadAsync(
		string aggregateId,
		string aggregateType,
		CancellationToken cancellationToken)
	{
		var events = await _inner.LoadAsync(aggregateId, aggregateType, cancellationToken).ConfigureAwait(false);
		return await DecryptEventsAsync(events, cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public override async ValueTask<IReadOnlyList<StoredEvent>> LoadAsync(
		string aggregateId,
		string aggregateType,
		long fromVersion,
		CancellationToken cancellationToken)
	{
		var events = await _inner.LoadAsync(aggregateId, aggregateType, fromVersion, cancellationToken).ConfigureAwait(false);
		return await DecryptEventsAsync(events, cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public override async ValueTask<AppendResult> AppendAsync(
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
	public override Task<int> EraseEventsAsync(
		string aggregateId,
		string aggregateType,
		Guid erasureRequestId,
		CancellationToken cancellationToken)
		=> RequireInnerErasure().EraseEventsAsync(aggregateId, aggregateType, erasureRequestId, cancellationToken);

	/// <inheritdoc/>
	public override Task<bool> IsErasedAsync(
		string aggregateId,
		string aggregateType,
		CancellationToken cancellationToken)
		=> RequireInnerErasure().IsErasedAsync(aggregateId, aggregateType, cancellationToken);

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
			// A tombstoned event carries no payload: there is nothing to decrypt and nothing to rewrite, so
			// it passes through untouched. Without this the load path dereferenced the absent payload.
			if (evt.EventData is null)
			{
				results.Add(evt);
				continue;
			}

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
		if (stored.EventData is null)
		{
			return stored;
		}

		Type eventType;
		try
		{
			eventType = _eventSerializer.ResolveType(stored.EventType);
		}
		catch (UnknownEventTypeException)
		{
			// An event whose type name is not registered was not written through this decorator's
			// field-encryption path, so there is nothing here to decrypt and it is returned unchanged.
			// Only THIS outcome is swallowed: a decryption or deserialization fault on a KNOWN type is a
			// real failure and must surface, not be handed back to the caller as if it were untouched data.
			return stored;
		}

		// Byte-identical passthrough. An event type declaring no [PersonalData] members has nothing to
		// decrypt, and materializing it only to re-emit it would hand the caller bytes that are merely
		// EQUIVALENT to the stored ones: property order, string escaping, and number and date formatting all
		// differ between the stored form and a re-serialization. A consumer that hashes, signs, compares or
		// diffs the stored payload reads that difference as tampering. The decision is a property of the
		// TYPE and the plan behind it is cached, so this is a dictionary lookup, not a per-event walk.
		if (!SubjectFieldCryptor.HasPersonalDataFields(eventType))
		{
			return stored;
		}

		var domainEvent = _eventSerializer.DeserializeEvent(stored.EventData, eventType);

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

		return await provider.DecryptAsync(encryptedData, CurrentEncryptionContext(), cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Wraps a capability of the decorated store so events still pass through encryption on their way to it.
	/// </summary>
	/// <param name="serviceType">The capability interface being resolved.</param>
	/// <returns>An encrypting view over the capability, or <see langword="null"/> when the inner store lacks it.</returns>
	/// <remarks>
	/// The transactional append is an append: handing it over unwrapped would write the caller's events to
	/// the store in plaintext, through the very object that exists to prevent that. The view therefore
	/// applies the same encryption the ordinary append path applies, and honours the same modes.
	/// </remarks>
	protected override object? WrapCapability(Type serviceType)
	{
		ArgumentNullException.ThrowIfNull(serviceType);

		if (serviceType == typeof(ITransactionalEventStore)
			&& Inner.GetService(typeof(ITransactionalEventStore)) is ITransactionalEventStore transactional)
		{
			return new EncryptingTransactionalView(this, transactional);
		}

		return null;
	}

	private sealed class EncryptingTransactionalView(
		EncryptingEventStoreDecorator outer,
		ITransactionalEventStore capability)
		: EventStoreCapabilityView(outer), ITransactionalEventStore
	{
		public async ValueTask<AppendResult> AppendWithOutboxStagingAsync(
			string aggregateId,
			string aggregateType,
			IEnumerable<IDomainEvent> events,
			long expectedVersion,
			Func<IDbTransaction, CancellationToken, ValueTask> stageOutbox,
			CancellationToken cancellationToken)
		{
			var mode = outer._options.Value.Mode;

			if (mode == EncryptionMode.DecryptOnlyReadOnly)
			{
				throw new InvalidOperationException(Resources.Encryption_ReadOnlyEventStore);
			}

			var toAppend = mode is EncryptionMode.Disabled or EncryptionMode.DecryptOnlyWritePlaintext
				? events
				: await outer.EncryptEventsAsync(events, cancellationToken).ConfigureAwait(false);

			return await capability.AppendWithOutboxStagingAsync(
				aggregateId, aggregateType, toAppend, expectedVersion, stageOutbox, cancellationToken)
				.ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Builds the encryption context for the operation in flight, binding the tenant it is running as.
	/// </summary>
	/// <remarks>
	/// Falls back to the configured default ONLY when no tenant context is registered at all, which is
	/// the single-tenant composition -- there a constant is the correct answer and always was, so this
	/// keeps existing single-tenant ciphertext decryptable. A multi-tenant host always resolves a tenant
	/// and therefore always gets real separation, which is the case the defect was about.
	/// </remarks>
	private EncryptionContext CurrentEncryptionContext() => new()
	{
		Purpose = _options.Value.DefaultPurpose,
		TenantId = _tenantContext.TenantId,
		RequireFipsCompliance = _options.Value.RequireFipsCompliance
	};
}
