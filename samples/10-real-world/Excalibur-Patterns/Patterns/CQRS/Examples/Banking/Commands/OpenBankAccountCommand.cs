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
///     Command to open a new bank account.
/// </summary>
public class OpenBankAccountCommand : CommandBase<string>
{
	/// <summary>
	///     Initializes a new instance of the <see cref="OpenBankAccountCommand" /> class.
	/// </summary>
	public OpenBankAccountCommand(string accountHolder, decimal initialDeposit, string accountType = "Checking")
	{
		if (string.IsNullOrWhiteSpace(accountHolder))
		{
			throw new ArgumentException("Account holder name is required", nameof(accountHolder));
		}

		if (initialDeposit < 0)
		{
			throw new ArgumentException("Initial deposit cannot be negative", nameof(initialDeposit));
		}

		AccountHolder = accountHolder;
		InitialDeposit = initialDeposit;
		AccountType = accountType;
	}

	/// <summary>
	///     Gets the account holder name.
	/// </summary>
	public string AccountHolder { get; }

	/// <summary>
	///     Gets the initial deposit amount.
	/// </summary>
	public decimal InitialDeposit { get; }

	/// <summary>
	///     Gets the account type.
	/// </summary>
	public string AccountType { get; }
}
