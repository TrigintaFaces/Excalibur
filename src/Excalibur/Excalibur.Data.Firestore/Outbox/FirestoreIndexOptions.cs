// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.Data.Firestore.Outbox;

/// <summary>
/// Configuration options for Firestore composite index optimization.
/// </summary>
/// <remarks>
/// <para>
/// Firestore queries that use both an equality filter and an ordering clause
/// (e.g., <c>status == Staged ORDER BY createdAt</c>) require composite indexes.
/// This options class documents the required indexes and provides validation
/// to ensure the outbox collection has optimal query performance.
/// </para>
/// <para>
/// Required composite indexes for the outbox collection:
/// <list type="bullet">
/// <item><c>status ASC, createdAt ASC</c> — Used by <c>GetUnsentMessagesAsync</c> for FIFO ordering of staged messages.</item>
/// <item><c>status ASC, scheduledAt ASC</c> — Used by <c>GetScheduledMessagesAsync</c> for scheduled message queries.</item>
/// <item><c>status ASC, retryCount ASC</c> — Used by <c>GetFailedMessagesAsync</c> for retry ordering.</item>
/// <item><c>status ASC, sentAt ASC</c> — Used by <c>CleanupSentMessagesAsync</c> for cleanup queries.</item>
/// </list>
/// </para>
/// </remarks>
public sealed class FirestoreIndexOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether composite index validation is enabled.
	/// </summary>
	/// <value>Defaults to <see langword="true"/>.</value>
	public bool EnableCompositeIndexValidation { get; set; } = true;

	/// <summary>
	/// Gets or sets the collection name to validate indexes for.
	/// </summary>
	/// <value>Defaults to "outbox_messages".</value>
	[Required]
	public string CollectionName { get; set; } = "outbox_messages";

	/// <summary>
	/// Gets the required composite indexes for optimal outbox query performance.
	/// </summary>
	/// <value>A read-only list of composite index definitions.</value>
	public IReadOnlyList<CompositeIndexDefinition> RequiredIndexes { get; } =
	[
		new("status_createdAt", ["status", "createdAt"], "GetUnsentMessagesAsync — FIFO ordering of staged messages"),
		new("status_scheduledAt", ["status", "scheduledAt"], "GetScheduledMessagesAsync — scheduled message queries"),
		new("status_retryCount", ["status", "retryCount"], "GetFailedMessagesAsync — retry ordering"),
		new("status_sentAt", ["status", "sentAt"], "CleanupSentMessagesAsync — cleanup queries"),
	];

	/// <summary>
	/// Gets or sets additional custom composite index definitions.
	/// </summary>
	/// <value>Defaults to an empty list.</value>
	public IList<CompositeIndexDefinition> AdditionalIndexes { get; set; } = [];
}

/// <summary>
/// Represents a composite index definition for Firestore.
/// </summary>
/// <param name="Name"> A descriptive name for the index. </param>
/// <param name="Fields"> The ordered list of field names in the composite index. </param>
/// <param name="Description"> A description of which query uses this index. </param>
public sealed record CompositeIndexDefinition(
	string Name,
	IReadOnlyList<string> Fields,
	string Description);
