// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>Validates <see cref="SqsBatchOptions"/> at startup. Reflection-free (AOT-safe).</summary>
internal sealed class SqsBatchOptionsValidator : IValidateOptions<SqsBatchOptions>
{
	/// <inheritdoc/>
	public ValidateOptionsResult Validate(string? name, SqsBatchOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var failures = new List<string>();

		if (options.MaxConcurrentReceiveBatches < 1)
		{
			failures.Add($"{nameof(SqsBatchOptions.MaxConcurrentReceiveBatches)} must be greater than zero.");
		}

		if (options.MaxConcurrentSendBatches < 1)
		{
			failures.Add($"{nameof(SqsBatchOptions.MaxConcurrentSendBatches)} must be greater than zero.");
		}

		if (options.VisibilityTimeout < 1)
		{
			failures.Add($"{nameof(SqsBatchOptions.VisibilityTimeout)} must be greater than zero.");
		}

		return failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
	}
}
