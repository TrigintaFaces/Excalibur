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
/// <see langword="true"/> when the event is being re-delivered from history rather than arriving live.
/// A catch-up read of the global stream sets this; the save path does not, because an event it has just
/// committed is by definition live. Handlers that suppress side effects on replay depend on it, so it
/// must never be hard-coded.
/// </param>
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
