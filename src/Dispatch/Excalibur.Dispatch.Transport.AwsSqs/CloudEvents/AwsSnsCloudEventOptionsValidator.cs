// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// Validates <see cref="AwsSnsCloudEventOptions"/> at startup so a misconfigured SNS CloudEvents pipeline
/// fails fast rather than at first publish.
/// </summary>
internal sealed class AwsSnsCloudEventOptionsValidator : IValidateOptions<AwsSnsCloudEventOptions>
{
	// SNS caps the message Subject at 100 characters.
	private const int MaxSubjectLength = 100;

	/// <inheritdoc/>
	public ValidateOptionsResult Validate(string? name, AwsSnsCloudEventOptions options)
	{
		if (options is null)
		{
			return ValidateOptionsResult.Fail($"{nameof(AwsSnsCloudEventOptions)} must not be null.");
		}

		var failures = new List<string>();

		if (options.DefaultSubject is { Length: > MaxSubjectLength })
		{
			failures.Add(
				$"{nameof(options.DefaultSubject)} must be {MaxSubjectLength} characters or fewer (the SNS Subject limit).");
		}

		if (options.UseFifoFeatures
			&& !options.EnableContentBasedDeduplication
			&& string.IsNullOrWhiteSpace(options.DefaultMessageGroupId))
		{
			failures.Add(
				$"{nameof(options.DefaultMessageGroupId)} is required for a FIFO topic when " +
				$"{nameof(options.EnableContentBasedDeduplication)} is disabled.");
		}

		return failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
	}
}
