// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Outbox.MultiTransport;

/// <summary>
/// Validates <see cref="MultiTransportOutboxOptions"/> at startup via the <c>ValidateOnStart</c> pipeline.
/// </summary>
internal sealed class MultiTransportOutboxOptionsValidator : IValidateOptions<MultiTransportOutboxOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, MultiTransportOutboxOptions options)
	{
		if (options is null)
		{
			return ValidateOptionsResult.Fail("MultiTransportOutboxOptions cannot be null.");
		}

		if (string.IsNullOrWhiteSpace(options.DefaultTransport))
		{
			return ValidateOptionsResult.Fail(
				"MultiTransportOutboxOptions.DefaultTransport is required.");
		}

		return ValidateOptionsResult.Success;
	}
}
