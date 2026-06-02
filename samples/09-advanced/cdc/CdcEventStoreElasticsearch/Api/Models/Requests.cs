// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

// ============================================================================
// API Request DTOs — Controller input contracts
// ============================================================================
//
// These types define the API surface for incoming requests. They are decoupled
// from the internal query objects (which carry pipeline metadata like
// CorrelationId, TenantId, ActivityType) and from the persistence models.
//
// The controller maps Request DTOs → Query objects → Dispatch pipeline.
// ============================================================================

using Excalibur.EventSourcing;

namespace CdcEventStoreElasticsearch.Api.Models;

/// <summary>
/// Request for searching orders with dictionary-based filters.
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

/// <summary>
/// Request for full-text search with cursor-based pagination.
/// </summary>
public sealed class FullTextSearchRequest
{
	/// <summary>Gets or sets the search text (required).</summary>
	public string Q { get; set; } = string.Empty;

	/// <summary>Gets or sets the maximum results per page.</summary>
	public int Limit { get; set; } = 20;

	/// <summary>
	/// Gets or sets the opaque cursor from a previous response.
	/// Omit for the first page.
	/// </summary>
	public string? Cursor { get; set; }

	/// <summary>
	/// Gets or sets the navigation direction.
	/// Defaults to <see cref="PageNavigation.Next"/>.
	/// Use <see cref="PageNavigation.First"/> to reset to the first page,
	/// <see cref="PageNavigation.Previous"/> to go back, or
	/// <see cref="PageNavigation.Last"/> to jump to the last page.
	/// </summary>
	public PageNavigation Navigation { get; set; } = PageNavigation.Next;
}

/// <summary>
/// Request for advanced search combining full-text and structured filters
/// with cursor-based pagination.
/// </summary>
public sealed class AdvancedSearchRequest
{
	/// <summary>Gets or sets the optional full-text search query.</summary>
	public string? Q { get; set; }

	/// <summary>Gets or sets the customer ID filter.</summary>
	public Guid? CustomerId { get; set; }

	/// <summary>Gets or sets the optional status filter.</summary>
	public string? Status { get; set; }

	/// <summary>Gets or sets the optional minimum order amount.</summary>
	public decimal? MinAmount { get; set; }

	/// <summary>Gets or sets the optional maximum order amount.</summary>
	public decimal? MaxAmount { get; set; }

	/// <summary>Gets or sets the minimum order date.</summary>
	public DateTime? FromDate { get; set; }

	/// <summary>Gets or sets the maximum order date.</summary>
	public DateTime? ToDate { get; set; }

	/// <summary>Gets or sets the tags filter (any match).</summary>
	public string[]? Tags { get; set; }

	/// <summary>Gets or sets the page size.</summary>
	public int PageSize { get; set; } = 20;

	/// <summary>
	/// Gets or sets the opaque cursor from a previous response.
	/// Omit for the first page.
	/// </summary>
	public string? Cursor { get; set; }

	/// <summary>
	/// Gets or sets the navigation direction.
	/// Defaults to <see cref="PageNavigation.Next"/>.
	/// Use <see cref="PageNavigation.First"/> to reset to the first page,
	/// <see cref="PageNavigation.Previous"/> to go back, or
	/// <see cref="PageNavigation.Last"/> to jump to the last page.
	/// </summary>
	public PageNavigation Navigation { get; set; } = PageNavigation.Next;
}

/// <summary>
/// Request for searching customers.
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
