// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.EventSourcing.Abstractions;

/// <summary>
/// Persists cursor maps for multi-stream projections, tracking the last-processed
/// event position per source stream.
/// </summary>
/// <remarks>
/// <para>
/// A cursor map is a <c>Dictionary&lt;string, long&gt;</c> mapping
/// <c>streamId → lastProcessedPosition</c> for a named projection.
/// This enables correct resumption after restart without reprocessing
/// or skipping events across streams.
/// </para>
/// <para>
/// Single-stream projections use <c>ISubscriptionCheckpointStore</c> instead.
/// Cursor maps are only used when a projection has multiple sources
/// via <c>FromStream()</c> or <c>FromCategory()</c>.
/// </para>
/// </remarks>
public interface ICursorMapStore
{
	/// <summary>
	/// Gets the cursor map for a named projection.
	/// </summary>
	/// <param name="projectionName">The unique projection identifier.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>
	/// A dictionary mapping stream identifiers to their last-processed positions.
	/// Returns an empty dictionary if no cursor map exists.
	/// </returns>
	Task<IReadOnlyDictionary<string, long>> GetCursorMapAsync(
		string projectionName,
		CancellationToken cancellationToken);

	/// <summary>
	/// Saves the cursor map for a named projection atomically.
	/// All stream positions are updated together or not at all.
	/// </summary>
	/// <param name="projectionName">The unique projection identifier.</param>
	/// <param name="cursorMap">The stream positions to persist.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	Task SaveCursorMapAsync(
		string projectionName,
		IReadOnlyDictionary<string, long> cursorMap,
		CancellationToken cancellationToken);

	/// <summary>
	/// Resets the cursor map for a named projection, removing all stored positions.
	/// Used during projection rebuild to restart from the beginning.
	/// </summary>
	/// <param name="projectionName">The unique projection identifier.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	Task ResetCursorMapAsync(
		string projectionName,
		CancellationToken cancellationToken);
}
