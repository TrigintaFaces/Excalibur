// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Outbox.Marten;

/// <summary>
/// Validates <see cref="MartenOutboxStoreOptions"/> at startup.
/// </summary>
public sealed class MartenOutboxStoreOptionsValidator : IValidateOptions<MartenOutboxStoreOptions>
{
	/// <summary>
	/// Validates the provided Marten outbox store options.
	/// </summary>
	/// <param name="name"> The name of the options instance being validated. </param>
	/// <param name="options"> The Marten outbox store options to validate. </param>
	/// <returns> A validation result indicating success or failure. </returns>
	public ValidateOptionsResult Validate(string? name, MartenOutboxStoreOptions options)
	{
		if (options is null)
		{
			return ValidateOptionsResult.Fail("Marten outbox store options cannot be null.");
		}

		if (options.DefaultRetentionPeriod <= TimeSpan.Zero)
		{
			return ValidateOptionsResult.Fail("Default retention period must be positive.");
		}

		if (options.CleanupBatchSize <= 0)
		{
			return ValidateOptionsResult.Fail("Cleanup batch size must be greater than zero.");
		}

		return ValidateOptionsResult.Success;
	}
}
