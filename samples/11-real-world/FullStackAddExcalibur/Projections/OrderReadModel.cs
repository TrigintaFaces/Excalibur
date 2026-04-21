// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Serialization;

namespace FullStackAddExcalibur.Projections;

/// <summary>
/// ElasticSearch read-model (inline projection) for order search.
/// </summary>
public sealed class OrderReadModel
{
	/// <summary>Gets or sets the document identifier.</summary>
	[JsonPropertyName("id")]
	public string Id { get; set; } = string.Empty;

	/// <summary>Gets or sets the order aggregate id.</summary>
	[JsonPropertyName("orderId")]
	public Guid OrderId { get; set; }

	/// <summary>Gets or sets the external order id from the CDC source.</summary>
	[JsonPropertyName("externalOrderId")]
	public string ExternalOrderId { get; set; } = string.Empty;

	/// <summary>Gets or sets the customer id.</summary>
	[JsonPropertyName("customerId")]
	public Guid CustomerId { get; set; }

	/// <summary>Gets or sets the current status.</summary>
	[JsonPropertyName("status")]
	public string Status { get; set; } = "Pending";

	/// <summary>Gets or sets the total amount.</summary>
	[JsonPropertyName("totalAmount")]
	public decimal TotalAmount { get; set; }

	/// <summary>Gets or sets the number of line items.</summary>
	[JsonPropertyName("itemCount")]
	public int ItemCount { get; set; }
}
