// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.EventSourcing;

/// <summary>
/// Provides context about the aggregate whose events are being notified,
/// passed to <see cref="IEventNotificationBroker.NotifyAsync"/> and
/// <see cref="IEventNotificationHandler{TEvent}.HandleAsync"/>.
/// </summary>
/// <param name="AggregateId">The unique identifier of the aggregate that produced the events.</param>
/// <param name="AggregateType">The type name of the aggregate.</param>
/// <param name="CommittedVersion">The aggregate version after the events were committed.</param>
/// <param name="Timestamp">The UTC timestamp when the notification was created.</param>
/// <param name="IsReplay">
/// <see langword="true"/> when the delivery is part of a DELIBERATE re-fold of stored history — a
/// rebuild, a recovery, or an on-demand build. It is a property of the operation, set by whatever asked
/// for the re-fold.
/// </param>
/// <remarks>
/// <b>CORRECTED.</b> This parameter was previously documented as "a catch-up read of the global stream
/// sets this … it must never be hard-coded", and both halves were wrong in ways that cost a consumer
/// their side effects.
/// <list type="bullet">
/// <item>
/// A catch-up read is NOT a replay. Events committed before a subscription started are ones it has
/// never applied, so no effect has been performed for them; classifying them as re-deliveries told
/// handlers to skip effects that never happened.
/// </item>
/// <item>
/// The live subscription DOES hard-code it, to <see langword="false"/>, and that is the only sound
/// value: it reads strictly above its own checkpoint, so nothing it delivers can be something its own
/// durable mark says it already applied. The re-fold paths hard-code <see langword="true"/> for the
/// same reason — replay is a property of the operation, not a position to compare.
/// </item>
/// <item>
/// It is NOT a duplicate-suppression mechanism. It is <see langword="false"/> on a redelivery after a
/// crash or a reader overlap, because such a delivery is not a re-fold of history. A handler with
/// effects outside the projection state must be idempotent on the event identifier; no flag the
/// framework computes can substitute for that.
/// </item>
/// </list>
/// </remarks>
/// <param name="GlobalPosition">
/// The global-stream position of the last event in this notification, or <see langword="null"/> when
/// the events do not come from a global-stream read.
/// </param>
/// <remarks>
/// <b>Why the position is carried.</b> A projection write that is conditional on which prefix of the
/// stream is already folded into it needs that prefix as a number, and the apply path is the only place
/// that knows which events it just folded. Without it the store can express the guarantee and no caller
/// can supply the argument. It is <see langword="null"/> on the save path, where the events are being
/// committed and no global position has been assigned yet.
/// </remarks>
public sealed record EventNotificationContext(
	string AggregateId,
	string AggregateType,
	long CommittedVersion,
	DateTimeOffset Timestamp,
	bool IsReplay = false,
	long? GlobalPosition = null);
