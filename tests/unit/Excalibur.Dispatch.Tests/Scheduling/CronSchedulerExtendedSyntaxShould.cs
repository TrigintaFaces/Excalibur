// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Options.Scheduling;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

using Xunit;

namespace Excalibur.Dispatch.Tests.Scheduling;

/// <summary>
/// Author≠impl engage-test for S859 ADR-336 · <c>bd-66qc7s</c> — <see cref="CronScheduler"/> must
/// HONOR the advertised <see cref="CronScheduleOptions.EnableExtendedSyntax"/> toggle: when it is
/// <see langword="false"/>, an expression using the extended operators (<c>L</c>/<c>W</c>/<c>#</c>)
/// MUST be rejected, not silently accepted.
/// </summary>
/// <remarks>
/// Cronos parses <c>L</c>/<c>W</c>/<c>#</c> regardless of the toggle, so before <c>66qc7s</c> the option
/// was advertised-but-dead (configured-but-ignored — every extended expression parsed even with the
/// toggle off). <b>RED on the pre-fix commit</b> (<c>e36a3dc08^</c>): <c>Parse("0 0 L * *")</c> with the
/// toggle off returned a valid expression instead of throwing. The two negative facts guard against
/// over-rejection (extended-enabled, and a standard cron with the toggle off, must still parse).
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class CronSchedulerExtendedSyntaxShould
{
	private static CronScheduler Create(bool enableExtendedSyntax) =>
		new(
			Microsoft.Extensions.Options.Options.Create(new CronScheduleOptions
			{
				EnableExtendedSyntax = enableExtendedSyntax,
				IncludeSeconds = false,
			}),
			NullLogger<CronScheduler>.Instance);

	[Fact]
	public void RejectExtendedOperator_WhenExtendedSyntaxDisabled()
	{
		var scheduler = Create(enableExtendedSyntax: false);

		// "0 0 L * *" — 'L' (last-day-of-month) is an extended operator in the day-of-month field.
		var ex = Should.Throw<ArgumentException>(() => scheduler.Parse("0 0 L * *"));
		ex.ParamName.ShouldBe("expression");
	}

	[Fact]
	public void AcceptExtendedOperator_WhenExtendedSyntaxEnabled()
	{
		var scheduler = Create(enableExtendedSyntax: true);

		var expression = scheduler.Parse("0 0 L * *");

		expression.ShouldNotBeNull("with EnableExtendedSyntax=true the 'L' operator must be honored, not rejected");
	}

	[Fact]
	public void AcceptStandardCron_WhenExtendedSyntaxDisabled()
	{
		var scheduler = Create(enableExtendedSyntax: false);

		// A plain 5-field cron uses no extended operator — the toggle must NOT over-reject it.
		var expression = scheduler.Parse("0 0 * * *");

		expression.ShouldNotBeNull("a standard cron must still parse when EnableExtendedSyntax=false");
	}
}
