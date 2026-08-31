// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.MySql;

namespace Excalibur.Data.Tests.MySql;

/// <summary>
/// Pins the ceiling on the MySQL backoff schedule, and that the schedule is grown from the base delay the
/// policy advertises.
/// </summary>
/// <remarks>
/// The schedule previously ignored <c>BaseRetryDelay</c> altogether and returned two raised to the attempt
/// number, in seconds, with no ceiling: the property the policy publishes as the base of its backoff
/// described a schedule it was not using, and the attempt budget was the only thing bounding the wait.
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class MySqlRetryDelayCeilingShould
{
	private static readonly TimeSpan Ceiling = TimeSpan.FromSeconds(30);

	[Fact]
	public void NeverExceedTheCeilingAtTheLargestAttemptBudgetTheOptionsAccept()
	{
		var policy = CreatePolicy(maxRetry: 10);

		for (var attempt = 1; attempt <= 10; attempt++)
		{
			policy.CalculateDelay(attempt)
				.ShouldBeLessThanOrEqualTo(Ceiling, $"Delay for attempt {attempt} exceeded the ceiling.");
		}
	}

	[Fact]
	public void SaturateAtTheCeilingRatherThanKeepDoubling()
	{
		var policy = CreatePolicy(maxRetry: 10);

		// Non-vacuity: unbounded, attempt 10 grown from one second is 512 seconds. A cap that never binds
		// would leave this arm passing for the wrong reason.
		policy.CalculateDelay(10).ShouldBe(Ceiling);
		policy.CalculateDelay(6).ShouldBe(Ceiling);
	}

	[Fact]
	public void GrowTheScheduleFromTheAdvertisedBaseDelay()
	{
		var policy = CreatePolicy(maxRetry: 5);

		policy.CalculateDelay(1).ShouldBe(policy.BaseRetryDelay);
		policy.CalculateDelay(2).ShouldBe(policy.BaseRetryDelay * 2);
		policy.CalculateDelay(3).ShouldBe(policy.BaseRetryDelay * 4);
	}

	private static MySqlRetryPolicy CreatePolicy(int maxRetry) =>
		new(new MySqlProviderOptions { MaxRetryCount = maxRetry }, EnabledTestLogger.Create());
}
