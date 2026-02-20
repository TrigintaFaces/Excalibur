// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Integration.Tests.Observability.Projections;

/// <summary>
/// Test projection model for IProjectionStore conformance tests.
/// </summary>
/// <remarks>
/// This model provides a variety of property types to test different filter operators:
/// <list type="bullet">
/// <item><c>Status</c> - String for equality/contains filters</item>
/// <item><c>Amount</c> - Decimal for numeric comparison filters</item>
/// <item><c>Quantity</c> - Integer for numeric filters</item>
/// <item><c>CreatedAt</c> - DateTimeOffset for date comparison filters</item>
/// <item><c>Tags</c> - List for IN filter testing</item>
/// </list>
/// </remarks>
public sealed class TestOrderProjection
{
	/// <summary>
	/// Gets or sets the unique identifier for the order.
	/// </summary>
	public string Id { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the customer identifier.
	/// </summary>
	public string CustomerId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the order status.
	/// </summary>
	/// <remarks>
	/// Common values: "Pending", "Active", "Shipped", "Delivered", "Cancelled", "Deleted"
	/// </remarks>
	public string Status { get; set; } = "Pending";

	/// <summary>
	/// Gets or sets the order amount.
	/// </summary>
	public decimal Amount { get; set; }

	/// <summary>
	/// Gets or sets the quantity of items in the order.
	/// </summary>
	public int Quantity { get; set; }

	/// <summary>
	/// Gets or sets when the order was created.
	/// </summary>
	public DateTimeOffset CreatedAt { get; set; }

	/// <summary>
	/// Gets or sets the tags associated with this order.
	/// </summary>
	public List<string> Tags { get; set; } = [];

	/// <summary>
	/// Gets or sets the product name.
	/// </summary>
	public string ProductName { get; set; } = string.Empty;

	/// <summary>
	/// Creates a test projection with specified values.
	/// </summary>
	public static TestOrderProjection Create(
		string id,
		string customerId,
		string status = "Pending",
		decimal amount = 0m,
		int quantity = 1,
		DateTimeOffset? createdAt = null,
		IEnumerable<string>? tags = null,
		string productName = "Test Product")
	{
		return new TestOrderProjection
		{
			Id = id,
			CustomerId = customerId,
			Status = status,
			Amount = amount,
			Quantity = quantity,
			CreatedAt = createdAt ?? DateTimeOffset.UtcNow,
			Tags = tags?.ToList() ?? [],
			ProductName = productName
		};
	}
}
