// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Query parameters for searching audit events by their annotation criteria.
/// </summary>
public sealed record AuditAnnotationQuery
{
	/// <summary>
	/// Gets the tags to match. Events must have at least one of these tags.
	/// </summary>
	public IReadOnlyList<string>? Tags { get; init; }

	/// <summary>
	/// Gets a value indicating whether to filter by bookmarked status.
	/// </summary>
	public bool? IsBookmarked { get; init; }

	/// <summary>
	/// Gets a value indicating whether to filter by presence of notes.
	/// </summary>
	public bool? HasNotes { get; init; }

	/// <summary>
	/// Gets the actor ID to filter annotations by creator.
	/// </summary>
	public string? ActorId { get; init; }

	/// <summary>
	/// Gets the earliest annotation creation time to include.
	/// </summary>
	public DateTimeOffset? Since { get; init; }

	/// <summary>
	/// Gets the number of results to skip for pagination.
	/// </summary>
	public int Skip { get; init; }

	/// <summary>
	/// Gets the maximum number of event IDs to return. Default is 100.
	/// </summary>
	public int MaxResults { get; init; } = 100;
}
