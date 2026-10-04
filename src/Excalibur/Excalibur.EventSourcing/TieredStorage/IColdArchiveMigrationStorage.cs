// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;

namespace Excalibur.EventSourcing.TieredStorage;

/// <summary>Provider operations required by the offline cold archive cutover protocol.</summary>
/// <remarks>
/// Namespace and keys are deterministic, exact, and include the provider endpoint/container/bucket/prefix
/// scope. The receipt key belongs to the legacy tenant/ID slot, independent of aggregate type.
/// Reads return complete bytes and their revision from one observation. Null means confirmed absence,
/// never an authorization, transport, parsing, or cancellation failure. Create returns false only for
/// a conditional-create conflict, true only after durable acknowledgement; ambiguous outcomes throw.
/// Adapters must retain receipts and both archives, and require an externally enforced, drained legacy
/// writer fence across migration and typed operation. These methods do not establish that fence.
/// Decoding must faithfully consume the complete archive and reject unsupported fields or formats;
/// silently dropping an unknown event field would let a later rewrite destroy part of the baseline.
/// Namespace identity must survive credential renewal and exclude credentials. Legacy, typed, and
/// receipt key spaces must be disjoint; typed keys must be injective across tenant/type/ID.
/// Missing containers or buckets are failures, not absent objects. Reads must disable transparent
/// content transcoding and return original stored bytes with their opaque conditional-write token.
/// A token need not identify a unique object incarnation; never fetch a newer token separately.
/// S3 ConditionalRequestConflict is retryable or thrown, not a false create result proving existence.
/// Decoders must reject truncated gzip, trailing or malformed content, and JSON null as well as
/// unsupported fields; no partial baseline is acceptable.
/// The namespace layout marker is disjoint from event and receipt keys, immutable and retained.
/// Its activation requires draining every legacy operation and retry before publication; its presence
/// is not itself a lock. Completed marker publication must be visible to later operations.
/// </remarks>
internal interface IColdArchiveMigrationStorage
{
	string NamespaceId { get; }
	string GetLayoutKey();
	string GetLegacyKey(KeyedTenantPartition tenant, string aggregateId);
	string GetTypedKey(KeyedTenantPartition tenant, string aggregateId, string aggregateType);
	string GetReceiptKey(KeyedTenantPartition tenant, string aggregateId);
	Task<ColdArchiveMigrationObject?> ReadAsync(string key, CancellationToken cancellationToken);
	Task<bool> TryCreateAsync(string key, ReadOnlyMemory<byte> content, CancellationToken cancellationToken);
	Task<IReadOnlyList<StoredEvent>> DecodeAsync(ColdArchiveMigrationObject archive, CancellationToken cancellationToken);
}
