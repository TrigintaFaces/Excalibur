// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.EventSourcing.Abstractions;

/// <summary>
/// Provides context about the aggregate whose events are being notified,
/// passed to <see cref="IEventNotificationBroker.NotifyAsync"/> and
/// <see cref="IEventNotificationHandler{TEvent}.HandleAsync"/>.
/// </summary>
/// <param name="AggregateId">The unique identifier of the aggregate that produced the events.</param>
/// <param name="AggregateType">The type name of the aggregate.</param>
/// <param name="CommittedVersion">The aggregate version after the events were committed.</param>
/// <param name="Timestamp">The UTC timestamp when the notification was created.</param>
public sealed record EventNotificationContext(
	string AggregateId,
	string AggregateType,
	long CommittedVersion,
	DateTimeOffset Timestamp);
