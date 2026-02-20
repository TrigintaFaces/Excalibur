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

using Excalibur.Dispatch.Patterns.CQRS.CQRS.Commands;

namespace examples.Excalibur.Patterns.CQRS.Examples.Banking.Commands;

/// <summary>
///     Command to withdraw money from an account.
/// </summary>
public class WithdrawMoneyCommand : CommandBase
{
	/// <summary>
	///     Initializes a new instance of the <see cref="WithdrawMoneyCommand" /> class.
	/// </summary>
	public WithdrawMoneyCommand(string accountId, decimal amount, string? reference)
	{
		if (string.IsNullOrWhiteSpace(accountId))
		{
			throw new ArgumentException("Account ID is required", nameof(accountId));
		}

		if (amount <= 0)
		{
			throw new ArgumentException("Withdrawal amount must be positive", nameof(amount));
		}

		AccountId = accountId;
		Amount = amount;
		Reference = reference ?? $"WDR-{Guid.NewGuid():N}";
	}

	/// <summary>
	///     Gets the account ID.
	/// </summary>
	public string AccountId { get; }

	/// <summary>
	///     Gets the withdrawal amount.
	/// </summary>
	public decimal Amount { get; }

	/// <summary>
	///     Gets the withdrawal Excalibur.Dispatch.Transport.Kafka.
	/// </summary>
	public string Reference { get; }
}
