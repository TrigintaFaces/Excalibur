// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text.Json;

using Excalibur.Dispatch;

namespace Excalibur.EventSourcing.TieredStorage;

/// <summary>Copies and publishes a recoverable typed archive under an external legacy-writer fence.</summary>
/// <remarks>
/// The receipt is not a lock, authentication proof, or permission to delete retained objects.
/// Typed writers must conditionally update the exact destination revision returned by ReadTypedAsync.
/// No caller may substitute a later metadata revision for the bytes that passed validation.
/// </remarks>
internal sealed class ColdArchiveMigrationCoordinator
{
	private readonly IColdArchiveMigrationStorage _storage;

	internal ColdArchiveMigrationCoordinator(IColdArchiveMigrationStorage storage)
	{
		ArgumentNullException.ThrowIfNull(storage);
		ArgumentException.ThrowIfNullOrEmpty(storage.NamespaceId);
		_storage = storage;
	}

	/// <summary>Explicitly activates this entire namespace after the operator drains legacy operations.</summary>
	/// <remarks>
	/// Retain the marker permanently, including after failure or cancellation. Activation does not migrate
	/// streams and cannot stop an old writer that passed an earlier absence check; the external fence must
	/// remain enforced. Ordinary reads never publish this namespace-wide availability change.
	/// </remarks>
	internal async Task ActivateTypedLayoutAsync(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		var marker = new ColdArchiveLayoutMarker(1, _storage.NamespaceId, "TypedV2");
		var bytes = JsonSerializer.SerializeToUtf8Bytes(marker, ColdArchiveMigrationJsonContext.Default.ColdArchiveLayoutMarker);
		_ = await _storage.TryCreateAsync(_storage.GetLayoutKey(), bytes, cancellationToken).ConfigureAwait(false);
		await RequireTypedLayoutAsync(cancellationToken).ConfigureAwait(false);
	}

	internal async Task RequireTypedLayoutAsync(CancellationToken cancellationToken)
	{
		var stored = await _storage.ReadAsync(_storage.GetLayoutKey(), cancellationToken).ConfigureAwait(false)
			?? throw new InvalidOperationException("The archive namespace has not been explicitly activated for typed storage.");
		var marker = JsonSerializer.Deserialize(stored.CopyContent(), ColdArchiveMigrationJsonContext.Default.ColdArchiveLayoutMarker);
		if (marker != new ColdArchiveLayoutMarker(1, _storage.NamespaceId, "TypedV2"))
		{
			throw new InvalidOperationException("The archive namespace layout marker is unsupported or belongs to another namespace.");
		}

		cancellationToken.ThrowIfCancellationRequested();
	}

	internal async Task MigrateAsync(KeyedTenantPartition tenant, string aggregateId, string aggregateType,
		CancellationToken cancellationToken)
	{
		ValidateIdentity(tenant, aggregateId, aggregateType, cancellationToken);
		await RequireTypedLayoutAsync(cancellationToken).ConfigureAwait(false);
		var receipt = await ReadReceiptAsync(tenant, aggregateId, cancellationToken).ConfigureAwait(false);
		if (receipt is not null)
		{
			await VerifyMigrationRetryAsync(receipt, tenant, aggregateId, aggregateType, cancellationToken).ConfigureAwait(false);
			return;
		}

		var source = await RequireObjectAsync(_storage.GetLegacyKey(tenant, aggregateId), cancellationToken).ConfigureAwait(false);
		var sourceEvents = await _storage.DecodeAsync(source, cancellationToken).ConfigureAwait(false);
		_ = ColdArchiveMigrationBaseline.Capture(tenant, aggregateId, aggregateType, sourceEvents);
		var expected = CreateReceipt(tenant, aggregateId, aggregateType, source);
		var created = await _storage.TryCreateAsync(expected.TypedKey, source.CopyContent(), cancellationToken).ConfigureAwait(false);
		if (!created && await TryVerifyPublishedRetryAsync(tenant, aggregateId, aggregateType, cancellationToken).ConfigureAwait(false))
		{
			return;
		}

		var destination = await RequireObjectAsync(expected.TypedKey, cancellationToken).ConfigureAwait(false);
		if (!source.HasSameContent(destination))
		{
			// Another migrator may have published and a typed writer appended since our absence read.
			if (await TryVerifyPublishedRetryAsync(tenant, aggregateId, aggregateType, cancellationToken).ConfigureAwait(false))
			{
				return;
			}

			throw new InvalidOperationException("An unpublished typed archive conflicts with the legacy copy.");
		}

		var currentSource = await RequireObjectAsync(expected.LegacyKey, cancellationToken).ConfigureAwait(false);
		if (source.Revision != currentSource.Revision || !source.HasSameContent(currentSource))
		{
			throw new InvalidOperationException("The legacy archive changed during migration.");
		}

		cancellationToken.ThrowIfCancellationRequested();
		var bytes = JsonSerializer.SerializeToUtf8Bytes(expected, ColdArchiveMigrationJsonContext.Default.ColdArchiveMigrationReceipt);
		_ = await _storage.TryCreateAsync(_storage.GetReceiptKey(tenant, aggregateId), bytes, cancellationToken).ConfigureAwait(false);
		receipt = await ReadReceiptAsync(tenant, aggregateId, cancellationToken).ConfigureAwait(false);
		if (receipt != expected)
		{
			throw new InvalidOperationException("The published migration receipt is missing or conflicting.");
		}

		await VerifyMigrationRetryAsync(receipt, tenant, aggregateId, aggregateType, cancellationToken).ConfigureAwait(false);
	}

	internal async Task EnsureLegacyAllowedAsync(KeyedTenantPartition tenant, string aggregateId,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(tenant);
		ArgumentException.ThrowIfNullOrEmpty(aggregateId);
		cancellationToken.ThrowIfCancellationRequested();
		if (await _storage.ReadAsync(_storage.GetLayoutKey(), cancellationToken).ConfigureAwait(false) is not null)
		{
			throw new InvalidOperationException("A namespace layout marker exists; legacy archive operations are disabled.");
		}

		// Even a malformed receipt stops legacy operations; it must never be interpreted as absence.
		if (await _storage.ReadAsync(_storage.GetReceiptKey(tenant, aggregateId), cancellationToken).ConfigureAwait(false) is not null)
		{
			throw new InvalidOperationException("A migration receipt exists; legacy archive operations are disabled.");
		}
	}

	internal async Task<ColdArchiveMigrationObject?> ReadTypedAsync(KeyedTenantPartition tenant, string aggregateId,
		string aggregateType, CancellationToken cancellationToken)
	{
		ValidateIdentity(tenant, aggregateId, aggregateType, cancellationToken);
		await RequireTypedLayoutAsync(cancellationToken).ConfigureAwait(false);
		var receipt = await ReadReceiptAsync(tenant, aggregateId, cancellationToken).ConfigureAwait(false);
		if (receipt is not null)
		{
			return await ReadPublishedAsync(receipt, tenant, aggregateId, aggregateType, cancellationToken).ConfigureAwait(false);
		}

		if (await _storage.ReadAsync(_storage.GetLegacyKey(tenant, aggregateId), cancellationToken).ConfigureAwait(false) is not null)
		{
			throw new InvalidOperationException("The legacy archive has no completed migration receipt.");
		}

		return await ReadValidatedTypedAsync(tenant, aggregateId, aggregateType, cancellationToken).ConfigureAwait(false);
	}

	private async Task<bool> TryVerifyPublishedRetryAsync(KeyedTenantPartition tenant, string aggregateId,
		string aggregateType, CancellationToken cancellationToken)
	{
		var receipt = await ReadReceiptAsync(tenant, aggregateId, cancellationToken).ConfigureAwait(false);
		if (receipt is null)
		{
			return false;
		}

		await VerifyMigrationRetryAsync(receipt, tenant, aggregateId, aggregateType, cancellationToken).ConfigureAwait(false);
		return true;
	}

	private async Task VerifyMigrationRetryAsync(ColdArchiveMigrationReceipt receipt, KeyedTenantPartition tenant,
		string aggregateId, string aggregateType, CancellationToken cancellationToken)
	{
		if (!string.Equals(receipt.AggregateType, aggregateType, StringComparison.Ordinal))
		{
			throw new InvalidOperationException("The legacy slot was migrated for a different aggregate type.");
		}

		_ = await ReadPublishedAsync(receipt, tenant, aggregateId, aggregateType, cancellationToken).ConfigureAwait(false);
	}

	private async Task<ColdArchiveMigrationObject?> ReadPublishedAsync(ColdArchiveMigrationReceipt receipt,
		KeyedTenantPartition tenant, string aggregateId, string requestedType, CancellationToken cancellationToken)
	{
		if (receipt.FormatVersion != 1 || receipt.DigestAlgorithm != "SHA256"
			|| receipt.NamespaceId != _storage.NamespaceId || receipt.TenantId != tenant.TenantId
			|| receipt.AggregateId != aggregateId || string.IsNullOrEmpty(receipt.AggregateType)
			|| receipt.LegacyKey != _storage.GetLegacyKey(tenant, aggregateId)
			|| receipt.TypedKey != _storage.GetTypedKey(tenant, aggregateId, receipt.AggregateType))
		{
			throw new InvalidOperationException("The migration receipt does not match this storage namespace and stream.");
		}

		// Keys have been derived and checked above; do not follow arbitrary references from receipt JSON.
		var source = await RequireObjectAsync(receipt.LegacyKey, cancellationToken).ConfigureAwait(false);
		if (receipt.SourceRevision != source.Revision || receipt.SourceSha256 != source.Sha256)
		{
			throw new InvalidOperationException("The retained legacy archive does not match the migration receipt.");
		}

		var sourceEvents = await _storage.DecodeAsync(source, cancellationToken).ConfigureAwait(false);
		var baseline = ColdArchiveMigrationBaseline.Capture(tenant, aggregateId, receipt.AggregateType, sourceEvents);
		var migrated = await RequireObjectAsync(receipt.TypedKey, cancellationToken).ConfigureAwait(false);
		baseline.VerifyPreservedBy(await _storage.DecodeAsync(migrated, cancellationToken).ConfigureAwait(false));
		return string.Equals(requestedType, receipt.AggregateType, StringComparison.Ordinal)
			? migrated
			: await ReadValidatedTypedAsync(tenant, aggregateId, requestedType, cancellationToken).ConfigureAwait(false);
	}

	private async Task<ColdArchiveMigrationObject?> ReadValidatedTypedAsync(KeyedTenantPartition tenant,
		string aggregateId, string aggregateType, CancellationToken cancellationToken)
	{
		var result = await _storage.ReadAsync(_storage.GetTypedKey(tenant, aggregateId, aggregateType), cancellationToken).ConfigureAwait(false);
		if (result is not null)
		{
			var events = await _storage.DecodeAsync(result, cancellationToken).ConfigureAwait(false);
			ColdArchiveBatch.ValidateStream(events, tenant, aggregateId, aggregateType);
		}

		return result;
	}

	private async Task<ColdArchiveMigrationReceipt?> ReadReceiptAsync(KeyedTenantPartition tenant, string aggregateId,
		CancellationToken cancellationToken)
	{
		var value = await _storage.ReadAsync(_storage.GetReceiptKey(tenant, aggregateId), cancellationToken).ConfigureAwait(false);
		return value is null ? null : JsonSerializer.Deserialize(value.CopyContent(),
			ColdArchiveMigrationJsonContext.Default.ColdArchiveMigrationReceipt)
			?? throw new InvalidOperationException("A migration receipt cannot contain null.");
	}

	private async Task<ColdArchiveMigrationObject> RequireObjectAsync(string key, CancellationToken cancellationToken) =>
		await _storage.ReadAsync(key, cancellationToken).ConfigureAwait(false)
		?? throw new InvalidOperationException("A required migration object is missing.");

	private ColdArchiveMigrationReceipt CreateReceipt(KeyedTenantPartition tenant, string aggregateId,
		string aggregateType, ColdArchiveMigrationObject source) =>
		new(1, "SHA256", _storage.NamespaceId, _storage.GetLegacyKey(tenant, aggregateId),
			_storage.GetTypedKey(tenant, aggregateId, aggregateType), tenant.TenantId, aggregateId, aggregateType,
			source.Revision, source.Sha256);

	private static void ValidateIdentity(KeyedTenantPartition tenant, string aggregateId, string aggregateType,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(tenant);
		ArgumentException.ThrowIfNullOrEmpty(aggregateId);
		ArgumentException.ThrowIfNullOrEmpty(aggregateType);
		cancellationToken.ThrowIfCancellationRequested();
	}
}
