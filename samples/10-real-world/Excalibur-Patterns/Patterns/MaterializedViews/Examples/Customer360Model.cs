// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
//
// Licensed under multiple licenses:
// - Excalibur License 1.0 (see LICENSE-EXCALIBUR.txt)
// - GNU Affero General Public License v3.0 or later (AGPL-3.0) (see LICENSE-AGPL-3.0.txt)
// - Server Side Public License v1.0 (SSPL-1.0) (see LICENSE-SSPL-1.0.txt)
// - Apache License 2.0 (see LICENSE-APACHE-2.0.txt)
//
// You may not use this file except in compliance with the License terms above. You may obtain copies of the licenses in the project root or online.
//
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.

namespace examples.Excalibur.Patterns.MaterializedViews.Examples;

/// <summary>
///     The 360-degree customer view model.
/// </summary>
public class Customer360Model
{
	/// <summary>
	///     Gets or sets the customer ID.
	/// </summary>
	public string CustomerId { get; set; } = string.Empty;

	/// <summary>
	///     Gets or sets the customer name.
	/// </summary>
	public string CustomerName { get; set; } = string.Empty;

	/// <summary>
	///     Gets or sets the email address.
	/// </summary>
	public string Email { get; set; } = string.Empty;

	/// <summary>
	///     Gets or sets the phone number.
	/// </summary>
	public string Phone { get; set; } = string.Empty;

	/// <summary>
	///     Gets or sets the registration date.
	/// </summary>
	public DateTime RegistrationDate { get; set; }

	/// <summary>
	///     Gets or sets the total number of orders.
	/// </summary>
	public int TotalOrders { get; set; }

	/// <summary>
	///     Gets or sets the lifetime value.
	/// </summary>
	public decimal LifetimeValue { get; set; }

	/// <summary>
	///     Gets or sets the average order value.
	/// </summary>
	public decimal AverageOrderValue { get; set; }

	/// <summary>
	///     Gets or sets the last order date.
	/// </summary>
	public DateTime? LastOrderDate { get; set; }

	/// <summary>
	///     Gets or sets the total interactions.
	/// </summary>
	public int TotalInteractions { get; set; }

	/// <summary>
	///     Gets or sets the last interaction date.
	/// </summary>
	public DateTime? LastInteractionDate { get; set; }

	/// <summary>
	///     Gets or sets the preferred communication channel.
	/// </summary>
	public string PreferredChannel { get; set; } = string.Empty;

	/// <summary>
	///     Gets or sets the number of open support tickets.
	/// </summary>
	public int OpenTickets { get; set; }

	/// <summary>
	///     Gets or sets the total support tickets.
	/// </summary>
	public int TotalTickets { get; set; }

	/// <summary>
	///     Gets or sets the average ticket resolution time in hours.
	/// </summary>
	public double AverageResolutionTime { get; set; }

	/// <summary>
	///     Gets or sets customer preferences.
	/// </summary>
	public Dictionary<string, string> Preferences { get; set; } = new();

	/// <summary>
	///     Gets or sets the engagement score (0-100).
	/// </summary>
	public int EngagementScore { get; set; }

	/// <summary>
	///     Gets or sets the risk score (0-100).
	/// </summary>
	public int RiskScore { get; set; }

	/// <summary>
	///     Gets or sets the loyalty tier.
	/// </summary>
	public string LoyaltyTier { get; set; } = "Bronze";

	/// <summary>
	///     Gets or sets when this view was last updated.
	/// </summary>
	public DateTime LastUpdated { get; set; }

	/// <summary>
	///     Creates a deep clone of this model.
	/// </summary>
	/// <returns> A cloned instance. </returns>
	public Customer360Model Clone() =>
		new()
		{
			CustomerId = CustomerId,
			CustomerName = CustomerName,
			Email = Email,
			Phone = Phone,
			RegistrationDate = RegistrationDate,
			TotalOrders = TotalOrders,
			LifetimeValue = LifetimeValue,
			AverageOrderValue = AverageOrderValue,
			LastOrderDate = LastOrderDate,
			TotalInteractions = TotalInteractions,
			LastInteractionDate = LastInteractionDate,
			PreferredChannel = PreferredChannel,
			OpenTickets = OpenTickets,
			TotalTickets = TotalTickets,
			AverageResolutionTime = AverageResolutionTime,
			Preferences = new Dictionary<string, string>(Preferences),
			EngagementScore = EngagementScore,
			RiskScore = RiskScore,
			LoyaltyTier = LoyaltyTier,
			LastUpdated = LastUpdated
		};
}
