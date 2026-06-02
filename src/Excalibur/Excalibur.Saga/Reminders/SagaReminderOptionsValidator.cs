// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Saga.Reminders;

/// <summary>
/// Validates <see cref="SagaReminderOptions"/> at startup via the <c>ValidateOnStart</c> pipeline.
/// </summary>
/// <remarks>
/// Performs cross-property constraint checks that cannot be expressed via
/// <see cref="System.ComponentModel.DataAnnotations"/> attributes alone.
/// Sprint 833 bd-1he43e.
/// </remarks>
internal sealed class SagaReminderOptionsValidator : IValidateOptions<SagaReminderOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, SagaReminderOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var failures = new List<string>();

		// DefaultDelay must be positive
		if (options.DefaultDelay <= TimeSpan.Zero)
		{
			failures.Add(
				$"{nameof(SagaReminderOptions.DefaultDelay)} must be positive (was {options.DefaultDelay}).");
		}

		// MaxRemindersPerSaga validated by [Range(1, 1000)] DataAnnotation,
		// but double-check for defense in depth
		if (options.MaxRemindersPerSaga < 1)
		{
			failures.Add(
				$"{nameof(SagaReminderOptions.MaxRemindersPerSaga)} must be >= 1 (was {options.MaxRemindersPerSaga}).");
		}

		// MinimumDelay must be positive
		if (options.MinimumDelay <= TimeSpan.Zero)
		{
			failures.Add(
				$"{nameof(SagaReminderOptions.MinimumDelay)} must be positive (was {options.MinimumDelay}).");
		}

		// MaximumDelay must be positive
		if (options.MaximumDelay <= TimeSpan.Zero)
		{
			failures.Add(
				$"{nameof(SagaReminderOptions.MaximumDelay)} must be positive (was {options.MaximumDelay}).");
		}

		// Cross-property: MinimumDelay must be less than MaximumDelay
		if (options.MinimumDelay >= options.MaximumDelay)
		{
			failures.Add(
				$"{nameof(SagaReminderOptions.MinimumDelay)} ({options.MinimumDelay}) must be less than " +
				$"{nameof(SagaReminderOptions.MaximumDelay)} ({options.MaximumDelay}).");
		}

		// Cross-property: DefaultDelay must be within [MinimumDelay, MaximumDelay]
		if (options.DefaultDelay < options.MinimumDelay || options.DefaultDelay > options.MaximumDelay)
		{
			failures.Add(
				$"{nameof(SagaReminderOptions.DefaultDelay)} ({options.DefaultDelay}) must be between " +
				$"{nameof(SagaReminderOptions.MinimumDelay)} ({options.MinimumDelay}) and " +
				$"{nameof(SagaReminderOptions.MaximumDelay)} ({options.MaximumDelay}).");
		}

		return failures.Count > 0
			? ValidateOptionsResult.Fail(failures)
			: ValidateOptionsResult.Success;
	}
}
