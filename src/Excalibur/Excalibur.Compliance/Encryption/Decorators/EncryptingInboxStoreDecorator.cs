// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json;

using Excalibur.Compliance.Configuration;
using Excalibur.Dispatch;

using Microsoft.Extensions.Options;

namespace Excalibur.Compliance.Encryption.Decorators;

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
internal sealed class EncryptingInboxStoreDecorator : IInboxStore, IProcessingTrackingInboxStore, IClaimableInboxStore, ILeasedInboxStore, IInboxStoreAdmin, IBackoffSchedulableInboxStore, IInboxStoreCapabilities, ITransactionalInboxStore, IScopedTransactionalInboxStore
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
	/// <remarks>
	/// Reports the EFFECTIVE atomic-claim capability and composes through chains: the decorator can forward a
	/// claim only when its inner store is itself claim-capable (directly via <see cref="IClaimableInboxStore"/>
	/// or transitively via a nested <see cref="IInboxStoreCapabilities"/>). This is what lets the startup
	/// presence-guard reject a decorator-over-non-claimable-inner instead of passing on the statically-declared
	/// <see cref="IClaimableInboxStore"/> and throwing at first claim.
	/// </remarks>
	public bool SupportsClaim =>
		_inner is IClaimableInboxStore || (_inner is IInboxStoreCapabilities capabilities && capabilities.SupportsClaim);

	/// <inheritdoc/>
	/// <remarks>
	/// Reports the EFFECTIVE lease capability and composes through chains (see <see cref="SupportsClaim"/>).
	/// Tracked separately from <see cref="SupportsClaim"/> because the two are different protocols: an inner
	/// store may offer the caller-governed claim and no lease, and forwarding a lease into it would fail.
	/// </remarks>
	public bool SupportsLeasedClaim =>
		_inner is ILeasedInboxStore || (_inner is IInboxStoreCapabilities capabilities && capabilities.SupportsLeasedClaim);

	/// <inheritdoc />
	/// <remarks>
	/// Reports the EFFECTIVE durable Processing-tracking capability and composes through chains (see
	/// <see cref="SupportsClaim"/>).
	/// </remarks>
	public bool SupportsProcessingTracking =>
		_inner is IProcessingTrackingInboxStore || (_inner is IInboxStoreCapabilities capabilities && capabilities.SupportsProcessingTracking);

	/// <inheritdoc />
	/// <remarks>
	/// <para>
	/// Reports the EFFECTIVE transactional handler+mark capability across BOTH transactional seams — the
	/// relational <see cref="ITransactionalInboxStore"/> and the document-store
	/// <see cref="IScopedTransactionalInboxStore"/> — and composes through chains (see
	/// <see cref="SupportsClaim"/>). A store capable of either seam is transactional-capable, because this
	/// decorator can forward the scoped seam over either one.
	/// </para>
	/// <para>
	/// An inner store that reports its own effective capability is AUTHORITATIVE and takes precedence over
	/// the static interface test: a store may implement a transactional seam yet be configured such that it
	/// cannot honour the atomic contract (for example a document store lacking the shared partition key its
	/// batch requires), and it reports <see langword="false"/> for exactly that case. Trusting the interface
	/// test over that report would re-advertise an atomicity guarantee the store has disclaimed.
	/// </para>
	/// </remarks>
	public bool SupportsTransactional =>
		_inner is IInboxStoreCapabilities capabilities
			? capabilities.SupportsTransactional
			: _inner is ITransactionalInboxStore or IScopedTransactionalInboxStore;

	/// <inheritdoc/>
	/// <remarks>
	/// <para>
	/// Reports the EFFECTIVE scoped transactional capability, composing through chains (see
	/// <see cref="SupportsClaim"/>). This decorator declares <see cref="IScopedTransactionalInboxStore"/>
	/// in order to FORWARD it, so a bare type check on the decorator reports the seam unconditionally;
	/// reporting it here answers for what the chain can actually execute.
	/// </para>
	/// <para>
	/// Either inner seam satisfies it, which is why this tracks <see cref="SupportsTransactional"/> rather
	/// than narrowing to the scoped one. The decorator BRIDGES the scoped seam onto a relational-only inner
	/// store -- wrapping its transaction in a scope -- so through this decorator a relational store really
	/// does offer the scoped protocol. Narrowing here would report the seam absent while the decorator
	/// stands ready to serve it, sending the caller to the weaker claim protocol for no reason.
	/// </para>
	/// </remarks>
	public bool SupportsScopedTransactional =>
		_inner is IInboxStoreCapabilities capabilities
			? capabilities.SupportsScopedTransactional || capabilities.SupportsTransactional
			: _inner is ITransactionalInboxStore or IScopedTransactionalInboxStore;

	/// <inheritdoc/>
	/// <remarks>
	/// Reports the EFFECTIVE backoff-schedule capability and composes through chains (see
	/// <see cref="SupportsClaim"/>). This decorator declares
	/// <see cref="IBackoffSchedulableInboxStore"/> so it can FORWARD the schedule, which makes a bare type
	/// check on this decorator report a capability the inner store may not have. Answering here is what
	/// keeps the caller's own fallback decision observable rather than absorbed.
	/// </remarks>
	public bool SupportsBackoffScheduling =>
		_inner is IBackoffSchedulableInboxStore
		|| (_inner is IInboxStoreCapabilities capabilities && capabilities.SupportsBackoffScheduling);

	/// <summary>
	/// Resolves the inner store's administrative surface, refusing with a stated reason when it has none.
	/// </summary>
	/// <value>The inner store's <see cref="IInboxStoreAdmin"/> implementation.</value>
	/// <exception cref="NotSupportedException">
	/// The decorated store does not provide the administrative surface. The message names the store that
	/// does not, because a decorated chain gives the caller no other way to find out which one it was.
	/// </exception>
	/// <remarks>
	/// <para>
	/// This decorator implements <see cref="IInboxStoreAdmin"/> so the retry processor reaches the inner
	/// store through it, and an inner store without that surface is a genuine misconfiguration. What the
	/// cast got wrong was the REPORT, not the refusal: a hard cast raises
	/// <see cref="InvalidCastException"/>, which <see cref="IInboxStoreAdmin"/> does not document and which
	/// names neither the capability that was missing nor the store that was missing it. A caller reading it
	/// learns only that some cast failed somewhere inside a decorator chain.
	/// </para>
	/// <para>
	/// The refusal is deliberately not softened into a silent no-op. Dropping an administrative call would
	/// leave the retry processor believing it had queried or mutated entries it never reached.
	/// </para>
	/// </remarks>
	private IInboxStoreAdmin Admin =>
		_inner as IInboxStoreAdmin
		?? throw new NotSupportedException(
			$"The inbox store this decorator wraps ({_inner.GetType().Name}) does not implement "
			+ "IInboxStoreAdmin, so the administrative surface (bulk queries, statistics, cleanup and the "
			+ "retry processor's failed-entry sweep) cannot be forwarded to it. Configure an admin-capable "
			+ "inbox store, or do not register the components that require one.");

	/// <inheritdoc />
	public ValueTask<bool> TryProcessTransactionallyAsync(
		string messageId,
		string handlerType,
		Func<System.Data.IDbTransaction, CancellationToken, ValueTask> handler,
		CancellationToken cancellationToken)
	{
		// Forward the transactional handler+mark to the inner store. Fail LOUD (never a silent no-op) if the
		// inner store cannot enlist a transaction — a silent fallback would downgrade exactly-once to
		// at-least-once undetected. The SupportsTransactional presence-guard makes this path unreachable at
		// runtime for a correctly-validated configuration.
		if (_inner is not ITransactionalInboxStore transactional)
		{
			throw new NotSupportedException(
				$"The decorated inbox store '{_inner.GetType().FullName}' does not implement ITransactionalInboxStore; " +
				"transactional handler+mark cannot be forwarded through the encrypting decorator.");
		}

		return transactional.TryProcessTransactionallyAsync(messageId, handlerType, handler, cancellationToken);
	}

	/// <inheritdoc cref="IScopedTransactionalInboxStore.TryProcessTransactionallyAsync" />
	/// <remarks>
	/// <para>
	/// Forwards the scoped exactly-once seam — the highest-precedence atomic path, selected by a type test on
	/// the OUTERMOST store instance. A decorator that omitted this member would make the seam invisible
	/// through decoration and silently downgrade a document store's atomicity to the at-least-once claim
	/// protocol, so it is forwarded here rather than left to the inner store's static type.
	/// </para>
	/// <para>
	/// Two forwarding routes, both preserving the atomic contract. An inner store implementing the scoped
	/// seam is forwarded directly. An inner store implementing only the relational
	/// <see cref="ITransactionalInboxStore"/> is bridged onto it by wrapping the active transaction in
	/// <see cref="SqlInboxTransactionScope"/> — the same adaptation the relational providers apply to expose
	/// this seam, so the handler still enlists its writes atomically with the processed-mark.
	/// </para>
	/// <para>
	/// No message payload crosses this path, so encryption semantics are unchanged: the handler receives the
	/// opaque transaction scope, not a stored entry. Payload encryption and decryption remain confined to the
	/// entry-carrying members (<see cref="CreateEntryAsync"/> on write, <see cref="GetEntryAsync"/> and the
	/// bulk admin reads on read), exactly as for the relational overload above.
	/// </para>
	/// </remarks>
	public ValueTask<bool> TryProcessTransactionallyAsync(
		string messageId,
		string handlerType,
		Func<IInboxTransactionScope, CancellationToken, ValueTask> handler,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(handler);

		if (_inner is IScopedTransactionalInboxStore scoped)
		{
			return scoped.TryProcessTransactionallyAsync(messageId, handlerType, handler, cancellationToken);
		}

		// Fail LOUD (never a silent fallback) if the inner store can enlist neither seam — a silent downgrade
		// of exactly-once to at-least-once is the defect this forward exists to prevent. The
		// SupportsTransactional presence-guard makes this unreachable for a validated configuration.
		if (_inner is not ITransactionalInboxStore relational)
		{
			throw new NotSupportedException(
				$"The decorated inbox store '{_inner.GetType().FullName}' implements neither IScopedTransactionalInboxStore " +
				"nor ITransactionalInboxStore; scoped transactional handler+mark cannot be forwarded through the encrypting decorator.");
		}

		return relational.TryProcessTransactionallyAsync(
			messageId,
			handlerType,
			(transaction, ct) => handler(new SqlInboxTransactionScope(transaction), ct),
			cancellationToken);
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

	/// <inheritdoc/>
	public ValueTask MarkProcessingAsync(string messageId, string handlerType, CancellationToken cancellationToken)
	{
		// Forward the Processing-tracking capability to the inner store. The Processing status carries no
		// encrypted payload, so no transformation is needed. Fail LOUD (never a silent no-op) if the inner
		// store cannot persist Processing — a silent skip would re-create the at-most-once silent-degrade.
		if (_inner is not IProcessingTrackingInboxStore tracker)
		{
			throw new NotSupportedException(
				$"The decorated inbox store '{_inner.GetType().FullName}' does not implement IProcessingTrackingInboxStore; " +
				"durable Processing tracking cannot be forwarded through the encrypting decorator.");
		}

		return tracker.MarkProcessingAsync(messageId, handlerType, cancellationToken);
	}

	/// <inheritdoc />
	public ValueTask<bool> TryMarkAsProcessedAsync(string messageId, string handlerType, CancellationToken cancellationToken)
	{
		return _inner.TryMarkAsProcessedAsync(messageId, handlerType, cancellationToken);
	}

	/// <inheritdoc/>
	/// <inheritdoc/>
	public ValueTask<LeaseToken?> TryAcquireLeaseAsync(string messageId, string handlerType, TimeSpan leaseDuration, CancellationToken cancellationToken)
	{
		// Forward the lease acquisition to the inner store. It carries no encrypted payload (only the
		// message/handler identifiers), so no transformation is needed. Fail LOUD if the inner store has no
		// lease path — a silent fallback would re-create the race.
		if (_inner is not ILeasedInboxStore leased)
		{
			throw new NotSupportedException(
				$"The decorated inbox store '{_inner.GetType().FullName}' does not implement ILeasedInboxStore; " +
				"a lease cannot be acquired through the encrypting decorator.");
		}

		return leased.TryAcquireLeaseAsync(messageId, handlerType, leaseDuration, cancellationToken);
	}

	/// <inheritdoc/>
	public ValueTask<bool> CompleteAsync(string messageId, string handlerType, LeaseToken lease, CancellationToken cancellationToken)
	{
		// The lease term is opaque and carries no payload, so it crosses the encryption boundary unchanged.
		// Fail LOUD, as the acquire path does.
		if (_inner is not ILeasedInboxStore leased)
		{
			throw new NotSupportedException(
				$"The decorated inbox store '{_inner.GetType().FullName}' does not implement ILeasedInboxStore; " +
				"a leased entry cannot be completed through the encrypting decorator.");
		}

		return leased.CompleteAsync(messageId, handlerType, lease, cancellationToken);
	}

	/// <inheritdoc/>
	public ValueTask<bool> FailAsync(string messageId, string handlerType, LeaseToken lease, string errorMessage, CancellationToken cancellationToken)
	{
		if (_inner is not ILeasedInboxStore leased)
		{
			throw new NotSupportedException(
				$"The decorated inbox store '{_inner.GetType().FullName}' does not implement ILeasedInboxStore; " +
				"a leased entry cannot be failed through the encrypting decorator.");
		}

		return leased.FailAsync(messageId, handlerType, lease, errorMessage, cancellationToken);
	}

	/// <inheritdoc/>
	public ValueTask<bool> TryClaimAsync(string messageId, string handlerType, CancellationToken cancellationToken)
	{
		// Forward the atomic-claim capability to the inner store. The claim carries no encrypted payload
		// (only the message/handler identifiers), so no transformation is needed. Fail LOUD (never a silent
		// no-op) if the inner store cannot claim atomically — a silent fallback would re-create the race.
		if (_inner is not IClaimableInboxStore claimable)
		{
			throw new NotSupportedException(
				$"The decorated inbox store '{_inner.GetType().FullName}' does not implement IClaimableInboxStore; " +
				"atomic claiming cannot be forwarded through the encrypting decorator.");
		}

		return claimable.TryClaimAsync(messageId, handlerType, cancellationToken);
	}

	/// <inheritdoc/>
	public ValueTask ReleaseAsync(string messageId, string handlerType, CancellationToken cancellationToken)
	{
		if (_inner is not IClaimableInboxStore claimable)
		{
			throw new NotSupportedException(
				$"The decorated inbox store '{_inner.GetType().FullName}' does not implement IClaimableInboxStore; " +
				"claim release cannot be forwarded through the encrypting decorator.");
		}

		return claimable.ReleaseAsync(messageId, handlerType, cancellationToken);
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
	public ValueTask MarkFailedAsync(string messageId, string handlerType, string errorMessage, int retryCount, CancellationToken cancellationToken)
	{
		// errorMessage is not encrypted by the core MarkFailedAsync path; delegate the admin overload likewise.
		return Admin.MarkFailedAsync(messageId, handlerType, errorMessage, retryCount, cancellationToken);
	}

	/// <inheritdoc />
	public ValueTask MarkFailedWithBackoffAsync(
		string messageId,
		string handlerType,
		string errorMessage,
		int retryCount,
		DateTimeOffset nextAttemptAt,
		CancellationToken cancellationToken)
	{
		// Forward the optional backoff-schedule capability. Unlike dead-lettering (a mandatory terminal
		// transition that fails LOUD), backoff is an optimization: if the inner store doesn't support it,
		// fall back to the plain failed status (fail-open) so the decorator never regresses behavior.
		// errorMessage is not encrypted on the failure path, so no field encryption is needed here.
		if (_inner is IBackoffSchedulableInboxStore schedulable)
		{
			return schedulable.MarkFailedWithBackoffAsync(messageId, handlerType, errorMessage, retryCount, nextAttemptAt, cancellationToken);
		}

		return _inner.MarkFailedAsync(messageId, handlerType, errorMessage, cancellationToken);
	}

	/// <inheritdoc />
	public async ValueTask<IEnumerable<InboxEntry>> GetAllTenantsFailedEntriesAsync(
		int maxRetries,
		DateTimeOffset? olderThan,
		int batchSize,
		CancellationToken cancellationToken)
	{
		var admin = Admin;
		var entries = await admin.GetAllTenantsFailedEntriesAsync(maxRetries, olderThan, batchSize, cancellationToken)
			.ConfigureAwait(false);
		return await DecryptEntriesAsync(entries, cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async ValueTask<IEnumerable<InboxEntry>> GetAllTenantsEntriesAsync(CancellationToken cancellationToken)
	{
		var admin = Admin;
		var entries = await admin.GetAllTenantsEntriesAsync(cancellationToken).ConfigureAwait(false);
		return await DecryptEntriesAsync(entries, cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public ValueTask<InboxStatistics> GetAllTenantsStatisticsAsync(CancellationToken cancellationToken)
	{
		return Admin.GetAllTenantsStatisticsAsync(cancellationToken);
	}

	/// <inheritdoc />
	public ValueTask<int> CleanupAllTenantsProcessedEntriesAsync(DateTimeOffset olderThan, CancellationToken cancellationToken)
	{
		return Admin.CleanupAllTenantsProcessedEntriesAsync(olderThan, cancellationToken);
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
