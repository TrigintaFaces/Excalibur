// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.Abstractions;

/// <summary>
/// Validates <see cref="EventNotificationOptions"/> at startup via the <c>ValidateOnStart</c> pipeline.
/// Replaces the <c>[Range(typeof(TimeSpan), ...)]</c> attribute with an AOT-safe check.
/// </summary>
internal sealed class EventNotificationOptionsValidator : IValidateOptions<EventNotificationOptions>
{
	private static readonly TimeSpan MinThreshold = TimeSpan.FromMilliseconds(1);
	private static readonly TimeSpan MaxThreshold = TimeSpan.FromMinutes(10);

	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, EventNotificationOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		if (options.InlineProjectionWarningThreshold < MinThreshold ||
			options.InlineProjectionWarningThreshold > MaxThreshold)
		{
			return ValidateOptionsResult.Fail(
				$"{nameof(EventNotificationOptions)}.{nameof(EventNotificationOptions.InlineProjectionWarningThreshold)} " +
				$"must be between {MinThreshold} and {MaxThreshold} (was {options.InlineProjectionWarningThreshold}).");
		}

		return ValidateOptionsResult.Success;
	}
}
