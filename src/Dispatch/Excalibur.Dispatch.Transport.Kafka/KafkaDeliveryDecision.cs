// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Dispatch.Transport.Kafka;

/// <summary>
/// What a consumer must do with a record it has just fetched, as decided by <see cref="KafkaPartitionProgress"/>.
/// </summary>
/// <param name="Deliver">
/// <see langword="true"/> when the record must be handed to the caller; <see langword="false"/> when it must be
/// dropped, either because it was already handed out or because a seek back is required first.
/// </param>
/// <param name="Generation">The assignment generation the caller presents when settling a delivered record.</param>
/// <param name="SeekTo">
/// When set, the partition must be sought to this offset before the next fetch: the fetch position has passed
/// an offset that is still owed, so the seek that should have replayed it was lost or overtaken.
/// </param>
/// <param name="AbandonedOffset">
/// When set, an owed offset was found to no longer exist in the log (removed by compaction after it was
/// requeued), so it was released: nothing can redeliver it, and holding the position on it would stall the
/// partition forever. The caller logs it.
/// </param>
internal readonly record struct KafkaDeliveryDecision(bool Deliver, long Generation, long? SeekTo, long? AbandonedOffset);
