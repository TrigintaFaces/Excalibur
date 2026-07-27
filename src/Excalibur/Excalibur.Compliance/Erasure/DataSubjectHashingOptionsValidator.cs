// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Compliance.Erasure;

using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Validates <see cref="DataSubjectHashingOptions"/> at startup via the <c>ValidateOnStart</c> pipeline so a
/// missing or weak pepper is a fail-closed startup error rather than a silent unkeyed hash.
/// </summary>
internal sealed class DataSubjectHashingOptionsValidator : IValidateOptions<DataSubjectHashingOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, DataSubjectHashingOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		if (string.IsNullOrEmpty(options.Pepper))
		{
			return ValidateOptionsResult.Fail(
				$"{nameof(DataSubjectHashingOptions.Pepper)} is required — configure a high-entropy secret " +
				"from your secret manager / KMS. The data-subject hasher will not fall back to an unkeyed hash.");
		}

		return options.Pepper.Length < DataSubjectHashingOptions.MinimumPepperLength
			? ValidateOptionsResult.Fail(
				$"{nameof(DataSubjectHashingOptions.Pepper)} must be at least " +
				$"{DataSubjectHashingOptions.MinimumPepperLength} characters (was {options.Pepper.Length}).")
			: ValidateOptionsResult.Success;
	}
}
