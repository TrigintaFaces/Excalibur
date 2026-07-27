// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>Validates <see cref="ChannelLongPollingOptions"/> at startup. Reflection-free (AOT-safe).</summary>
internal sealed class ChannelLongPollingOptionsValidator : IValidateOptions<ChannelLongPollingOptions>
{
	/// <inheritdoc/>
	public ValidateOptionsResult Validate(string? name, ChannelLongPollingOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var failures = new List<string>();

		if (options.MinPollers < 1)
		{
			failures.Add($"{nameof(ChannelLongPollingOptions.MinPollers)} must be greater than zero.");
		}

		if (options.MaxPollers < options.MinPollers)
		{
			failures.Add(
				$"{nameof(ChannelLongPollingOptions.MaxPollers)} must be greater than or equal to " +
				$"{nameof(ChannelLongPollingOptions.MinPollers)}.");
		}

		if (options.ChannelCapacity < 1)
		{
			failures.Add($"{nameof(ChannelLongPollingOptions.ChannelCapacity)} must be greater than zero.");
		}

		if (options.VisibilityTimeout < 1)
		{
			failures.Add($"{nameof(ChannelLongPollingOptions.VisibilityTimeout)} must be greater than zero.");
		}

		return failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
	}
}
