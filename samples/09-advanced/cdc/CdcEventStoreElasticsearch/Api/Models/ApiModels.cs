// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

namespace CdcEventStoreElasticsearch.Api.Models;

/// <summary>
/// Generic paged result for API responses.
/// </summary>
/// <typeparam name="T">The type of items in the result.</typeparam>
public sealed class PagedResult<T>
{
	/// <summary>Gets or sets the items in the current page.</summary>
	public IReadOnlyList<T> Items { get; set; } = [];

	/// <summary>Gets or sets the total count of all items.</summary>
	public int TotalCount { get; set; }

	/// <summary>Gets or sets the current page number (1-based).</summary>
	public int Page { get; set; } = 1;

	/// <summary>Gets or sets the page size.</summary>
	public int PageSize { get; set; } = 20;

	/// <summary>Gets the total number of pages.</summary>
	public int TotalPages => PageSize > 0 ? (int)Math.Ceiling(TotalCount / (double)PageSize) : 0;

	/// <summary>Gets whether there is a next page.</summary>
	public bool HasNextPage => Page < TotalPages;

	/// <summary>Gets whether there is a previous page.</summary>
	public bool HasPreviousPage => Page > 1;
}

/// <summary>
/// Search request for customers.
/// </summary>
public sealed class CustomerSearchRequest
{
	/// <summary>Gets or sets the full-text search query.</summary>
	public string? Query { get; set; }

	/// <summary>Gets or sets the customer tier filter.</summary>
	public string? Tier { get; set; }

	/// <summary>Gets or sets the active status filter.</summary>
	public bool? IsActive { get; set; }

	/// <summary>Gets or sets the minimum total spent.</summary>
	public decimal? MinTotalSpent { get; set; }

	/// <summary>Gets or sets the maximum total spent.</summary>
	public decimal? MaxTotalSpent { get; set; }

	/// <summary>Gets or sets the tags filter (any match).</summary>
	public string[]? Tags { get; set; }

	/// <summary>Gets or sets the page number (1-based).</summary>
	public int Page { get; set; } = 1;

	/// <summary>Gets or sets the page size.</summary>
	public int PageSize { get; set; } = 20;
}

/// <summary>
/// Search request for orders.
/// </summary>
public sealed class OrderSearchRequest
{
	/// <summary>Gets or sets the full-text search query.</summary>
	public string? Query { get; set; }

	/// <summary>Gets or sets the customer ID filter.</summary>
	public Guid? CustomerId { get; set; }

	/// <summary>Gets or sets the status filter.</summary>
	public string? Status { get; set; }

	/// <summary>Gets or sets the minimum order amount.</summary>
	public decimal? MinAmount { get; set; }

	/// <summary>Gets or sets the maximum order amount.</summary>
	public decimal? MaxAmount { get; set; }

	/// <summary>Gets or sets the minimum order date.</summary>
	public DateTime? FromDate { get; set; }

	/// <summary>Gets or sets the maximum order date.</summary>
	public DateTime? ToDate { get; set; }

	/// <summary>Gets or sets the tags filter (any match).</summary>
	public string[]? Tags { get; set; }

	/// <summary>Gets or sets the page number (1-based).</summary>
	public int Page { get; set; } = 1;

	/// <summary>Gets or sets the page size.</summary>
	public int PageSize { get; set; } = 20;
}
