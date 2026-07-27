// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch;

/// <summary>
/// Reports data-shaped capabilities of an <see cref="IOutboxStore"/> that a conformance/behaviour check must
/// know to assert the correct contract, rather than assuming one uniform storage model.
/// </summary>
/// <remarks>
/// <para>
/// Outbox stores fall into two storage models for a successfully-sent message. <b>Tracking</b> stores retain
/// the row and transition it to a terminal <see cref="OutboxStatus.Sent"/> state (a status column), so a sent
/// message remains countable and cleanup-eligible. <b>Delete-on-sent</b> stores (the relational Postgres and
/// Oracle providers) delete the row the instant it is marked sent — there is no sent row to count, and cleanup
/// is a no-op because the row is already gone.
/// </para>
/// <para>
/// This is a data-shaped capability in the BCL idiom (<c>Stream.CanSeek</c>,
/// <c>HttpClientHandler.SupportsAutomaticDecompression</c>, and the sibling
/// <see cref="IInboxStoreCapabilities"/>) — a property intrinsic to the store, not a separately-registered
/// marker and not an inheritance-buried virtual. A store that does not implement this interface is treated as
/// a tracking store (the default), so only delete-on-sent stores need declare it.
/// </para>
/// </remarks>
public interface IOutboxStoreCapabilities
{
	/// <summary>
	/// Gets a value indicating whether the store retains a successfully-sent message as a countable,
	/// cleanup-eligible <see cref="OutboxStatus.Sent"/> row.
	/// </summary>
	/// <value>
	/// <see langword="true"/> for a tracking store that keeps the sent row (statistics count it and cleanup
	/// removes it); <see langword="false"/> for a delete-on-sent store that removes the row on mark-sent (no
	/// sent row to count, cleanup is a no-op).
	/// </value>
	bool SupportsSentTracking { get; }
}
