// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// Validates <see cref="AwsSqsCloudEventOptions"/> at startup so a misconfigured SQS CloudEvents pipeline
/// fails fast rather than at first send.
/// </summary>
internal sealed class AwsSqsCloudEventOptionsValidator : IValidateOptions<AwsSqsCloudEventOptions>
{
	/// <inheritdoc/>
	public ValidateOptionsResult Validate(string? name, AwsSqsCloudEventOptions options)
	{
		if (options is null)
		{
			return ValidateOptionsResult.Fail($"{nameof(AwsSqsCloudEventOptions)} must not be null.");
		}

		var failures = new List<string>();

		// SQS SendMessageBatch accepts at most 10 entries per call.
		if (options.MaxBatchSize is < 1 or > 10)
		{
			failures.Add($"{nameof(options.MaxBatchSize)} must be between 1 and 10 (the SQS batch limit).");
		}

		// SQS DelaySeconds is bounded to 0..900 (15 minutes).
		if (options.DelaySeconds is < 0 or > 900)
		{
			failures.Add($"{nameof(options.DelaySeconds)} must be between 0 and 900 seconds.");
		}

		return failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
	}
}
