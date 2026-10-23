// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.Projections;

/// <summary>
/// Validates <see cref="EventNotificationOptions"/> at startup via the <c>ValidateOnStart</c> pipeline.
/// Replaces the <c>[Range(typeof(TimeSpan), ...)]</c> attribute with an AOT-safe check.
/// </summary>
internal sealed class EventNotificationOptionsValidator : IValidateOptions<EventNotificationOptions>
{
	private static readonly TimeSpan MinThreshold = TimeSpan.FromMilliseconds(1);
	private static readonly TimeSpan MaxThreshold = TimeSpan.FromMinutes(10);
	// A sub-second bound would abandon the post-commit phase on ordinary store latency and leave the
	// read model unapplied for no reason; the floor keeps the backstop from becoming a budget.
	private static readonly TimeSpan MinPostCommitTimeout = TimeSpan.FromSeconds(1);
	private static readonly TimeSpan MaxPostCommitTimeout = TimeSpan.FromMinutes(10);

	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, EventNotificationOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		if (options.ProjectionApplyWarningThreshold < MinThreshold ||
			options.ProjectionApplyWarningThreshold > MaxThreshold)
		{
			return ValidateOptionsResult.Fail(
				$"{nameof(EventNotificationOptions)}.{nameof(EventNotificationOptions.ProjectionApplyWarningThreshold)} " +
				$"must be between {MinThreshold} and {MaxThreshold} (was {options.ProjectionApplyWarningThreshold}).");
		}

		if (options.PostCommitTimeout < MinPostCommitTimeout ||
			options.PostCommitTimeout > MaxPostCommitTimeout)
		{
			return ValidateOptionsResult.Fail(
				$"{nameof(EventNotificationOptions)}.{nameof(EventNotificationOptions.PostCommitTimeout)} " +
				$"must be between {MinPostCommitTimeout} and {MaxPostCommitTimeout} (was {options.PostCommitTimeout}).");
		}

		return ValidateOptionsResult.Success;
	}
}
