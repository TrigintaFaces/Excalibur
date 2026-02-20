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

namespace examples.Excalibur.Patterns.EventSourcing.Examples.Projections;

/// <summary>
///     Read model for bank account balance projection.
/// </summary>
public class BankAccountBalanceReadModel
{
	/// <summary>
	///     Gets or sets the account ID.
	/// </summary>
	public string AccountId { get; set; } = string.Empty;

	/// <summary>
	///     Gets or sets the account holder name.
	/// </summary>
	public string AccountHolder { get; set; } = string.Empty;

	/// <summary>
	///     Gets or sets the current balance.
	/// </summary>
	public decimal Balance { get; set; }

	/// <summary>
	///     Gets or sets the total deposits.
	/// </summary>
	public decimal TotalDeposits { get; set; }

	/// <summary>
	///     Gets or sets the total withdrawals.
	/// </summary>
	public decimal TotalWithdrawals { get; set; }

	/// <summary>
	///     Gets or sets the transaction count.
	/// </summary>
	public int TransactionCount { get; set; }

	/// <summary>
	///     Gets or sets the account status.
	/// </summary>
	public string Status { get; set; } = "Unknown";

	/// <summary>
	///     Gets or sets the last updated timestamp.
	/// </summary>
	public DateTime LastUpdated { get; set; }
}
