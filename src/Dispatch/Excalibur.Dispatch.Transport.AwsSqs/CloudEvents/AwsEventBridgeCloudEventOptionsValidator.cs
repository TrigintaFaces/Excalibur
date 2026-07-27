// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// Validates <see cref="AwsEventBridgeCloudEventOptions"/> at startup so a misconfigured EventBridge
/// CloudEvents pipeline fails fast rather than at first put-events.
/// </summary>
internal sealed class AwsEventBridgeCloudEventOptionsValidator : IValidateOptions<AwsEventBridgeCloudEventOptions>
{
	/// <inheritdoc/>
	public ValidateOptionsResult Validate(string? name, AwsEventBridgeCloudEventOptions options)
	{
		if (options is null)
		{
			return ValidateOptionsResult.Fail($"{nameof(AwsEventBridgeCloudEventOptions)} must not be null.");
		}

		var failures = new List<string>();

		if (string.IsNullOrWhiteSpace(options.EventBusName))
		{
			failures.Add($"{nameof(options.EventBusName)} must not be null or whitespace.");
		}

		if (string.IsNullOrWhiteSpace(options.SourcePrefix))
		{
			failures.Add($"{nameof(options.SourcePrefix)} must not be null or whitespace.");
		}

		// EventBridge PutEvents accepts at most 10 entries per call.
		if (options.MaxBatchSize is < 1 or > 10)
		{
			failures.Add($"{nameof(options.MaxBatchSize)} must be between 1 and 10 (the EventBridge batch limit).");
		}

		if (options.EnableReplay && string.IsNullOrWhiteSpace(options.ReplayArchiveName))
		{
			failures.Add(
				$"{nameof(options.ReplayArchiveName)} is required when {nameof(options.EnableReplay)} is enabled.");
		}

		return failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
	}
}
