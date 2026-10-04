// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;

namespace Excalibur.EventSourcing;

/// <summary>
/// Requires a resolved payload after the caller has handled an explicit erasure marker.
/// </summary>
internal static class StoredEventPayload
{
	internal static IDomainEvent RequireDecoded(IDomainEvent? domainEvent, StoredEvent storedEvent) =>
		domainEvent ?? throw new InvalidOperationException(
			$"Event '{storedEvent.EventId}' at global position {storedEvent.GlobalPosition} deserialized to null; refusing to skip it.");

	internal static byte[] Require(StoredEvent storedEvent) =>
		storedEvent.EventData ?? throw new InvalidOperationException(
			$"Event '{storedEvent.EventId}' at global position {storedEvent.GlobalPosition} has no payload "
			+ "and is not an explicit erasure. Restore its archived payload or repair the store before advancing.");
}
