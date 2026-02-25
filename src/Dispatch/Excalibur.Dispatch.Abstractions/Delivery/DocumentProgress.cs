// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Abstractions.Delivery;

/// <summary>
/// Represents progress information for a document processing operation.
/// </summary>
/// <param name="PercentComplete">
/// The percentage of the operation that has been completed, from 0.0 to 100.0.
/// A value of <c>-1</c> indicates that progress is indeterminate.
/// </param>
/// <param name="ItemsProcessed">
/// The number of items or units of work that have been processed so far.
/// </param>
/// <param name="TotalItems">
/// The total number of items to process, or <c>null</c> if the total is unknown.
/// When null, consumers should display indeterminate progress indicators.
/// </param>
/// <param name="CurrentPhase">
/// A description of the current processing phase, or <c>null</c> if not applicable.
/// Use this for multi-stage operations to indicate which stage is active.
/// </param>
/// <remarks>
/// <para>
/// This record struct is designed for efficient progress reporting in high-throughput
/// scenarios. As a readonly record struct, it avoids heap allocations when passed
/// through <see cref="IProgress{T}"/>.
/// </para>
/// <para>
/// Progress information can be used to:
/// <list type="bullet">
/// <item>Display progress bars or percentage indicators to users</item>
/// <item>Log processing throughput for monitoring and diagnostics</item>
/// <item>Estimate remaining time based on items processed and total items</item>
/// <item>Provide status updates for long-running operations</item>
/// </list>
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Report progress during document processing
/// progress.Report(new DocumentProgress(
///     PercentComplete: 50.0,
///     ItemsProcessed: 500,
///     TotalItems: 1000,
///     CurrentPhase: "Processing records"));
///
/// // Report indeterminate progress when total is unknown
/// progress.Report(new DocumentProgress(
///     PercentComplete: -1,
///     ItemsProcessed: 500,
///     TotalItems: null,
///     CurrentPhase: "Streaming data"));
/// </code>
/// </example>
public readonly record struct DocumentProgress(
	double PercentComplete,
	long ItemsProcessed,
	long? TotalItems,
	string? CurrentPhase)
{
	/// <summary>
	/// Creates a progress instance representing a completed operation.
	/// </summary>
	/// <param name="totalItems">The total number of items that were processed.</param>
	/// <param name="finalPhase">Optional description of the final phase.</param>
	/// <returns>A <see cref="DocumentProgress"/> instance representing 100% completion.</returns>
	public static DocumentProgress Completed(long totalItems, string? finalPhase = null)
		=> new(100.0, totalItems, totalItems, finalPhase ?? "Completed");

	/// <summary>
	/// Creates a progress instance representing an indeterminate operation.
	/// </summary>
	/// <param name="itemsProcessed">The number of items processed so far.</param>
	/// <param name="currentPhase">Optional description of the current phase.</param>
	/// <returns>A <see cref="DocumentProgress"/> instance with indeterminate progress.</returns>
	public static DocumentProgress Indeterminate(long itemsProcessed, string? currentPhase = null)
		=> new(-1, itemsProcessed, null, currentPhase);

	/// <summary>
	/// Creates a progress instance for a specific percentage.
	/// </summary>
	/// <param name="itemsProcessed">The number of items processed.</param>
	/// <param name="totalItems">The total number of items to process.</param>
	/// <param name="currentPhase">Optional description of the current phase.</param>
	/// <returns>A <see cref="DocumentProgress"/> instance with calculated percentage.</returns>
	public static DocumentProgress FromItems(long itemsProcessed, long totalItems, string? currentPhase = null)
		=> new(
			totalItems > 0 ? (double)itemsProcessed / totalItems * 100.0 : 0.0,
			itemsProcessed,
			totalItems,
			currentPhase);
}
