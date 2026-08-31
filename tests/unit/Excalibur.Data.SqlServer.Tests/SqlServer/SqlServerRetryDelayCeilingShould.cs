// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.SqlServer;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Data.Tests.SqlServer;

/// <summary>
/// Pins the ceiling on the SQL Server backoff schedule, and that the schedule is grown from the base delay
/// the caller supplied.
/// </summary>
/// <remarks>
/// The schedule previously ignored the supplied base delay altogether and returned two raised to the
/// attempt number, in seconds, with no ceiling - so a caller who configured a base delay was given a
/// schedule unrelated to it, and the attempt budget was the only thing bounding the wait.
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Data.SqlServer")]
public sealed class SqlServerRetryDelayCeilingShould
{
	private static readonly TimeSpan Ceiling = TimeSpan.FromSeconds(30);

	[Fact]
	public void NeverExceedTheCeilingAtTheLargestAttemptBudgetTheOptionsAccept()
	{
		var policy = new SqlServerRetryPolicy(
			new SqlServerProviderOptions { RetryCount = 10 }, NullLogger.Instance);

		for (var attempt = 1; attempt <= 10; attempt++)
		{
			policy.CalculateDelay(attempt)
				.ShouldBeLessThanOrEqualTo(Ceiling, $"Delay for attempt {attempt} exceeded the ceiling.");
		}
	}

	[Fact]
	public void SaturateAtTheCeilingRatherThanKeepDoubling()
	{
		var policy = new SqlServerRetryPolicy(
			new SqlServerProviderOptions { RetryCount = 10 }, NullLogger.Instance);

		// Non-vacuity: unbounded, attempt 10 grown from one second is 512 seconds.
		policy.CalculateDelay(10).ShouldBe(Ceiling);
		policy.CalculateDelay(6).ShouldBe(Ceiling);
	}

	[Fact]
	public void GrowTheScheduleFromTheSuppliedBaseDelay()
	{
		// A base delay unrelated to the old hard-coded one-second schedule, so an implementation that
		// ignored the argument cannot satisfy this by coincidence.
		var policy = new SqlServerRetryPolicy(
			maxRetryAttempts: 5, baseRetryDelay: TimeSpan.FromMilliseconds(250), NullLogger.Instance);

		policy.CalculateDelay(1).ShouldBe(TimeSpan.FromMilliseconds(250));
		policy.CalculateDelay(2).ShouldBe(TimeSpan.FromMilliseconds(500));
		policy.CalculateDelay(3).ShouldBe(TimeSpan.FromMilliseconds(1_000));
	}
}
