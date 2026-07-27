// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>Validates <see cref="SqsChannelOptions"/> at startup. Reflection-free (AOT-safe).</summary>
internal sealed class SqsChannelOptionsValidator : IValidateOptions<SqsChannelOptions>
{
	/// <inheritdoc/>
	public ValidateOptionsResult Validate(string? name, SqsChannelOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var failures = new List<string>();

		if (options.ConcurrentPollers < 1)
		{
			failures.Add($"{nameof(SqsChannelOptions.ConcurrentPollers)} must be greater than zero.");
		}

		if (options.MaxConcurrentPollers < options.ConcurrentPollers)
		{
			failures.Add(
				$"{nameof(SqsChannelOptions.MaxConcurrentPollers)} must be greater than or equal to " +
				$"{nameof(SqsChannelOptions.ConcurrentPollers)}.");
		}

		if (options.ReceiveChannelCapacity < 1)
		{
			failures.Add($"{nameof(SqsChannelOptions.ReceiveChannelCapacity)} must be greater than zero.");
		}

		if (options.VisibilityTimeout < 1)
		{
			failures.Add($"{nameof(SqsChannelOptions.VisibilityTimeout)} must be greater than zero.");
		}

		return failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
	}
}
