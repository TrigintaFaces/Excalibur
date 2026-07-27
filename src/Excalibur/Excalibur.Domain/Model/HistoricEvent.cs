// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;

namespace Excalibur.Domain.Model;

/// <summary>
/// Pairs a persisted domain event with the authoritative stream version assigned to it by the event store.
/// </summary>
/// <param name="Event">The domain event to replay.</param>
/// <param name="Version">
/// The durable, zero-based position of <paramref name="Event"/> in its aggregate's stream, as assigned by the
/// store when the event was appended.
/// </param>
/// <remarks>
/// <para>
/// Replay reads the version from this envelope, never from the event payload. An event's position in a stream
/// is a persistence fact, established at append time; it is not a property the event can know about itself. A
/// payload that carries its own version can only ever restate what the store already recorded, and cannot be
/// trusted to do so &#8212; nothing in the type system compels a domain event to report the position it was
/// actually written at.
/// </para>
/// <para>
/// Because replay never consults the payload, an event whose own version field is absent, defaulted, or wrong
/// replays correctly, and a replay that has lost its versions cannot be constructed: there is no
/// <see cref="HistoricEvent"/> without a <see cref="Version"/>.
/// </para>
/// </remarks>
public readonly record struct HistoricEvent(IDomainEvent Event, long Version);
