// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Domain;

/// <summary>
/// Represents the result of a cursor-based page request.
/// </summary>
/// <typeparam name="T"> The type of items in the page result. </typeparam>
/// <param name="items"> The collection of items in this page. </param>
/// <param name="pageSize"> The number of items per page. </param>
/// <param name="totalRecords"> The total number of records available. </param>
public sealed class CursorPageResult<T>(IEnumerable<T> items, int pageSize, long totalRecords)
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
	/// Deconstructs the page result into its component parts.
	/// </summary>
	/// <param name="items"> The collection of items in this page. </param>
	/// <param name="pageSize"> The number of items per page. </param>
	/// <param name="totalRecords"> The total number of records available. </param>
	public void Deconstruct(out IEnumerable<T> items, out int pageSize, out long totalRecords)
	{
		items = Items;
		pageSize = PageSize;
		totalRecords = TotalRecords;
	}
}
