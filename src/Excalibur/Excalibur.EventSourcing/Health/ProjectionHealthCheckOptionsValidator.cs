// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.Health;

/// <summary>
/// Validates <see cref="ProjectionHealthCheckOptions"/> at startup via the <c>ValidateOnStart</c> pipeline.
/// Written as an <see cref="IValidateOptions{TOptions}"/> rather than data annotations so the checks stay
/// AOT-safe, matching the convention used by the other option validators in this package.
/// </summary>
/// <remarks>
/// The ordering check is the load-bearing one. A health check whose degraded threshold is not strictly below
/// its unhealthy threshold can never report <c>Degraded</c> — every lag that would be degraded is already
/// unhealthy — so the intermediate state silently disappears and an operator loses the early warning the
/// threshold pair exists to provide. That misconfiguration produces no error at runtime, which is precisely
/// why it is worth failing at startup.
/// </remarks>
internal sealed class ProjectionHealthCheckOptionsValidator : IValidateOptions<ProjectionHealthCheckOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, ProjectionHealthCheckOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var failures = new List<string>();

		if (options.UnhealthyLagThreshold <= 0)
		{
			failures.Add(
				$"{nameof(ProjectionHealthCheckOptions)}.{nameof(ProjectionHealthCheckOptions.UnhealthyLagThreshold)} " +
				$"must be greater than zero (was {options.UnhealthyLagThreshold}).");
		}

		if (options.DegradedLagThreshold <= 0)
		{
			failures.Add(
				$"{nameof(ProjectionHealthCheckOptions)}.{nameof(ProjectionHealthCheckOptions.DegradedLagThreshold)} " +
				$"must be greater than zero (was {options.DegradedLagThreshold}).");
		}

		if (options.DegradedLagThreshold >= options.UnhealthyLagThreshold)
		{
			failures.Add(
				$"{nameof(ProjectionHealthCheckOptions)}.{nameof(ProjectionHealthCheckOptions.DegradedLagThreshold)} " +
				$"({options.DegradedLagThreshold}) must be strictly less than " +
				$"{nameof(ProjectionHealthCheckOptions.UnhealthyLagThreshold)} ({options.UnhealthyLagThreshold}); " +
				"otherwise the Degraded state is unreachable and the early warning is lost.");
		}

		if (options.InlineErrorWindow <= TimeSpan.Zero)
		{
			failures.Add(
				$"{nameof(ProjectionHealthCheckOptions)}.{nameof(ProjectionHealthCheckOptions.InlineErrorWindow)} " +
				$"must be a positive duration (was {options.InlineErrorWindow}).");
		}

		return failures.Count > 0
			? ValidateOptionsResult.Fail(failures)
			: ValidateOptionsResult.Success;
	}
}
