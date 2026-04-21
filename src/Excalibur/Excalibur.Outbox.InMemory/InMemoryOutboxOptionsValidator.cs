// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Outbox.InMemory;

/// <summary>
/// Validates <see cref="InMemoryOutboxOptions"/> at startup via the <c>ValidateOnStart</c> pipeline.
/// </summary>
internal sealed class InMemoryOutboxOptionsValidator : IValidateOptions<InMemoryOutboxOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, InMemoryOutboxOptions options)
	{
		if (options is null)
		{
			return ValidateOptionsResult.Fail("InMemoryOutboxOptions cannot be null.");
		}

		if (options.MaxMessages < 0)
		{
			return ValidateOptionsResult.Fail(
				"InMemoryOutboxOptions.MaxMessages must be zero or greater.");
		}

		if (options.DefaultRetentionPeriod <= TimeSpan.Zero)
		{
			return ValidateOptionsResult.Fail(
				"InMemoryOutboxOptions.DefaultRetentionPeriod must be greater than zero.");
		}

		return ValidateOptionsResult.Success;
	}
}
