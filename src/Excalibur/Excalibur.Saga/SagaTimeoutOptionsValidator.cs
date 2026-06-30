// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Saga;

/// <summary>
/// Validates <see cref="SagaTimeoutOptions"/> at startup via the <c>ValidateOnStart</c> pipeline.
/// </summary>
/// <remarks>
/// Performs cross-property constraint checks that cannot be expressed via
/// <see cref="System.ComponentModel.DataAnnotations"/> attributes alone.
/// </remarks>
internal sealed class SagaTimeoutOptionsValidator : IValidateOptions<SagaTimeoutOptions>
{
	private static readonly TimeSpan MinPollInterval = TimeSpan.FromMilliseconds(100);

	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, SagaTimeoutOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var failures = new List<string>();

		// PollInterval must be at least 100ms (documented minimum)
		if (options.PollInterval < MinPollInterval)
		{
			failures.Add(
				$"{nameof(SagaTimeoutOptions.PollInterval)} must be >= 100ms (was {options.PollInterval}).");
		}

		// BatchSize validated by [Range(1, int.MaxValue)] DataAnnotation,
		// but double-check for defense in depth
		if (options.BatchSize < 1)
		{
			failures.Add(
				$"{nameof(SagaTimeoutOptions.BatchSize)} must be >= 1 (was {options.BatchSize}).");
		}

		// ShutdownTimeout must be positive
		if (options.ShutdownTimeout <= TimeSpan.Zero)
		{
			failures.Add(
				$"{nameof(SagaTimeoutOptions.ShutdownTimeout)} must be positive (was {options.ShutdownTimeout}).");
		}

		return failures.Count > 0
			? ValidateOptionsResult.Fail(failures)
			: ValidateOptionsResult.Success;
	}
}
