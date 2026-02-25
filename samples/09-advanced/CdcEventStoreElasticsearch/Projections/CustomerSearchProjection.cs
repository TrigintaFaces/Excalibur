// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Serialization;

namespace CdcEventStoreElasticsearch.Projections;

/// <summary>
/// Elasticsearch projection for customer search and display.
/// This read model is optimized for full-text search and filtering.
/// </summary>
public sealed class CustomerSearchProjection
{
	/// <summary>Gets or sets the unique identifier.</summary>
	[JsonPropertyName("id")]
	public string Id { get; set; } = string.Empty;

	/// <summary>Gets or sets the customer ID (aggregate ID).</summary>
	[JsonPropertyName("customerId")]
	public Guid CustomerId { get; set; }

	/// <summary>Gets or sets the external ID from the legacy system.</summary>
	[JsonPropertyName("externalId")]
	public string ExternalId { get; set; } = string.Empty;

	/// <summary>Gets or sets the customer name (searchable).</summary>
	[JsonPropertyName("name")]
	public string Name { get; set; } = string.Empty;

	/// <summary>Gets or sets the email address (searchable).</summary>
	[JsonPropertyName("email")]
	public string Email { get; set; } = string.Empty;

	/// <summary>Gets or sets the phone number.</summary>
	[JsonPropertyName("phone")]
	public string? Phone { get; set; }

	/// <summary>Gets or sets the number of orders.</summary>
	[JsonPropertyName("orderCount")]
	public int OrderCount { get; set; }

	/// <summary>Gets or sets the total amount spent.</summary>
	[JsonPropertyName("totalSpent")]
	public decimal TotalSpent { get; set; }

	/// <summary>Gets or sets the customer tier.</summary>
	[JsonPropertyName("tier")]
	public string Tier { get; set; } = "Bronze";

	/// <summary>Gets or sets whether the customer is active.</summary>
	[JsonPropertyName("isActive")]
	public bool IsActive { get; set; }

	/// <summary>Gets or sets when the customer was created.</summary>
	[JsonPropertyName("createdAt")]
	public DateTimeOffset CreatedAt { get; set; }

	/// <summary>Gets or sets when the customer was last updated.</summary>
	[JsonPropertyName("lastUpdatedAt")]
	public DateTimeOffset? LastUpdatedAt { get; set; }

	/// <summary>Gets or sets searchable tags for filtering.</summary>
	[JsonPropertyName("tags")]
	public List<string> Tags { get; set; } = [];
}

/// <summary>
/// Elasticsearch projection for customer analytics and reporting.
/// This materialized view aggregates customer metrics by tier.
/// </summary>
public sealed class CustomerTierSummaryProjection
{
	/// <summary>Gets or sets the unique identifier (tier name).</summary>
	[JsonPropertyName("id")]
	public string Id { get; set; } = string.Empty;

	/// <summary>Gets or sets the tier name.</summary>
	[JsonPropertyName("tier")]
	public string Tier { get; set; } = string.Empty;

	/// <summary>Gets or sets the total number of customers in this tier.</summary>
	[JsonPropertyName("customerCount")]
	public int CustomerCount { get; set; }

	/// <summary>Gets or sets the number of active customers.</summary>
	[JsonPropertyName("activeCount")]
	public int ActiveCount { get; set; }

	/// <summary>Gets or sets the total orders across all customers in this tier.</summary>
	[JsonPropertyName("totalOrders")]
	public int TotalOrders { get; set; }

	/// <summary>Gets or sets the total revenue from this tier.</summary>
	[JsonPropertyName("totalRevenue")]
	public decimal TotalRevenue { get; set; }

	/// <summary>Gets or sets the average spend per customer.</summary>
	[JsonPropertyName("averageSpend")]
	public decimal AverageSpend { get; set; }

	/// <summary>Gets or sets when this summary was last updated.</summary>
	[JsonPropertyName("lastUpdatedAt")]
	public DateTimeOffset LastUpdatedAt { get; set; }
}
