// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Inbox.InMemory;

/// <summary>
/// Validates <see cref="InMemoryInboxOptions"/> at startup via the <c>ValidateOnStart</c> pipeline.
/// </summary>
internal sealed class InMemoryInboxOptionsValidator : IValidateOptions<InMemoryInboxOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, InMemoryInboxOptions options)
	{
		if (options is null)
		{
			return ValidateOptionsResult.Fail("In-memory inbox options cannot be null.");
		}

		if (options.MaxEntries < 0)
		{
			return ValidateOptionsResult.Fail(
				"InMemoryInboxOptions.MaxEntries must be zero or greater.");
		}

		if (options.CleanupInterval <= TimeSpan.Zero)
		{
			return ValidateOptionsResult.Fail(
				"InMemoryInboxOptions.CleanupInterval must be greater than zero.");
		}

		if (options.RetentionPeriod <= TimeSpan.Zero)
		{
			return ValidateOptionsResult.Fail(
				"InMemoryInboxOptions.RetentionPeriod must be greater than zero.");
		}

		return ValidateOptionsResult.Success;
	}
}
