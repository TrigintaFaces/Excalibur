// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.DataProcessing;

/// <summary>
/// Represents the result of a cursor-based fetch operation, containing a page of records
/// and an opaque cursor pointing to the next page.
/// </summary>
/// <typeparam name="TRecord"> The type of records returned by the fetch. </typeparam>
/// <remarks>
/// <para>
/// The cursor is an opaque token that the framework passes back to
/// <see cref="IRecordFetcher{TRecord}.FetchBatchAsync"/> on subsequent calls.
/// Implementations define the cursor format (e.g., a database primary key,
/// a timestamp, or a composite hash).
/// </para>
/// <para>
/// A <see langword="null"/> <see cref="NextCursor"/> signals that there are no more
/// pages to fetch. An empty <see cref="Records"/> collection with a non-null cursor
/// is valid (the page was empty but more data may follow).
/// </para>
/// </remarks>
/// <param name="Records"> The records in this page. </param>
/// <param name="NextCursor">
/// An opaque cursor token pointing to the start of the next page,
/// or <see langword="null"/> if there are no more pages.
/// </param>
public sealed record CursorFetchResult<TRecord>(
	IReadOnlyList<TRecord> Records,
	string? NextCursor);
