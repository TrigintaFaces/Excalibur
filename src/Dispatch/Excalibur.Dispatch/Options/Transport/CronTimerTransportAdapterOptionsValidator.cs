// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Options.Transport;

/// <summary>
/// Validates <see cref="CronTimerTransportAdapterOptions"/> at startup via the <c>ValidateOnStart</c> pipeline.
/// </summary>
internal sealed class CronTimerTransportAdapterOptionsValidator : IValidateOptions<CronTimerTransportAdapterOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, CronTimerTransportAdapterOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var failures = new List<string>();

		if (options.TimeZone is null)
		{
			failures.Add(
				$"{nameof(CronTimerTransportAdapterOptions)}.{nameof(CronTimerTransportAdapterOptions.TimeZone)} must not be null. " +
				$"Configure it via services.Configure<{nameof(CronTimerTransportAdapterOptions)}>(o => o.TimeZone = TimeZoneInfo.Utc).");
		}

		return failures.Count > 0
			? ValidateOptionsResult.Fail(failures)
			: ValidateOptionsResult.Success;
	}
}
