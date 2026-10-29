// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


namespace Excalibur.Data.ElasticSearch.Projections;

/// <summary>
/// Identifies where a rebuild's destination documents come from.
/// </summary>
/// <remarks>
/// <para>
/// This is DERIVED from how a <see cref="ProjectionRebuildRequest"/> was built, never chosen beside the
/// data. The distinction is not a preference: an index copy reads the source documents, so it can only
/// ever produce the fields those documents already carry. A rebuild whose purpose is to ADD or RENAME a
/// field cannot be served by a copy at all -- the copy would complete, report success, and leave the new
/// field absent from every document.
/// </para>
/// <para>
/// An event re-fold reads the event stream instead, so the documents it writes are whatever the current
/// projection produces -- including fields no stored document has ever had.
/// </para>
/// </remarks>
public enum ProjectionRebuildMechanism
{
	/// <summary>
	/// Documents are copied from a source index to the destination. Correct for a like-for-like move,
	/// and structurally incapable of populating a field the source documents do not carry.
	/// </summary>
	IndexCopy = 0,

	/// <summary>
	/// Documents are re-folded from the event stream through the projection's current handlers, so a new
	/// generation may contain fields no previous document carried.
	/// </summary>
	EventRefold = 1,
}
