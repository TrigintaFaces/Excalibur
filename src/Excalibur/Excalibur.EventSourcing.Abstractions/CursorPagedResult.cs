// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.EventSourcing;

/// <summary>
/// Represents the result of a cursor-based page request.
/// </summary>
/// <typeparam name="T"> The type of items in the page result. </typeparam>
/// <param name="items"> The collection of items in this page. </param>
/// <param name="pageSize"> The number of items per page. </param>
/// <param name="totalRecords"> The total number of records available. </param>
/// <param name="nextCursor">
/// An opaque continuation token for retrieving the next page.
/// <see langword="null"/> when there are no more results.
/// </param>
/// <remarks>
/// <para>
/// The <paramref name="nextCursor"/> is an opaque string that the consumer passes
/// back on the next request to retrieve the subsequent page. The format of the cursor
/// is determined by the underlying store (e.g., Base64url-encoded Elasticsearch
/// <c>search_after</c> sort values, a database row key, or a page token from a
/// cloud API).
/// </para>
/// <para>
/// This follows the continuation-token pattern used by Microsoft Azure SDKs
/// (<c>ContinuationToken</c>) and Google Cloud APIs (<c>nextPageToken</c>).
/// </para>
/// </remarks>
public sealed class CursorPagedResult<T>(IEnumerable<T> items, int pageSize, long totalRecords, string? nextCursor = null)
{
	/// <summary>
	/// Gets the collection of items in this page.
	/// </summary>
	/// <value>
	/// The collection of items in this page.
	/// </value>
	public IEnumerable<T> Items { get; init; } = items;

	/// <summary>
	/// Gets the number of items per page.
	/// </summary>
	/// <value>
	/// The number of items per page.
	/// </value>
	public int PageSize { get; init; } = pageSize;

	/// <summary>
	/// Gets the total number of records available across all pages.
	/// </summary>
	/// <value>
	/// The total number of records available across all pages.
	/// </value>
	public long TotalRecords { get; init; } = totalRecords;

	/// <summary>
	/// Gets the total number of pages based on the page size and total records.
	/// </summary>
	/// <value>
	/// The total number of pages based on the page size and total records.
	/// </value>
	public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalRecords / (double)PageSize);

	/// <summary>
	/// Gets the opaque continuation token for retrieving the next page.
	/// <see langword="null"/> when there are no more results.
	/// </summary>
	/// <value>
	/// The continuation token, or <see langword="null"/> if this is the last page.
	/// </value>
	public string? NextCursor { get; init; } = nextCursor;

	/// <summary>
	/// Gets a value indicating whether more results are available.
	/// </summary>
	/// <value>
	/// <see langword="true"/> if <see cref="NextCursor"/> is not <see langword="null"/>;
	/// otherwise, <see langword="false"/>.
	/// </value>
	public bool HasMore => NextCursor is not null;

	/// <summary>
	/// Deconstructs the page result into its component parts.
	/// </summary>
	/// <param name="items"> The collection of items in this page. </param>
	/// <param name="pageSize"> The number of items per page. </param>
	/// <param name="totalRecords"> The total number of records available. </param>
	/// <param name="nextCursor"> The continuation token for the next page. </param>
	public void Deconstruct(out IEnumerable<T> items, out int pageSize, out long totalRecords, out string? nextCursor)
	{
		items = Items;
		pageSize = PageSize;
		totalRecords = TotalRecords;
		nextCursor = NextCursor;
	}
}
