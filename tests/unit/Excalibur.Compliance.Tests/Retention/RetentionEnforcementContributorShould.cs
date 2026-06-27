// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Compliance;
using Excalibur.Compliance.Retention;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Compliance.Tests.Retention;

/// <summary>
/// bd-vf7t02 — regression lock for the retention-enforcement contributor seam. Pre-fix,
/// <see cref="RetentionEnforcementService.EnforceRetentionAsync"/> was a no-op that returned
/// <c>RecordsCleaned = 0</c> while logging success. These tests prove enforcement now actually
/// dispatches to registered <see cref="IRetentionContributor"/>s and reports the records they cleaned.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class RetentionEnforcementContributorShould
{
	private static RetentionEnforcementService CreateSut(
		RetentionEnforcementOptions options,
		params IRetentionContributor[] contributors)
		=> new(
			Microsoft.Extensions.Options.Options.Create(options),
			NullLogger<RetentionEnforcementService>.Instance,
			contributors);

	[Fact]
	public async Task InvokeRegisteredContributorAndReportRecordsCleaned()
	{
		// Arrange — a contributor that reports it deleted 3 expired records.
		var contributor = new RecordingRetentionContributor(RetentionContributorResult.Succeeded(3));
		var sut = CreateSut(new RetentionEnforcementOptions(), contributor);

		// Act
		var result = await sut.EnforceRetentionAsync(CancellationToken.None);

		// Assert — the contributor was invoked (no longer a no-op) and its cleaned count is reported.
		// RED on the pre-fix code, which returned RecordsCleaned = 0 and never called any contributor.
		contributor.InvocationCount.ShouldBe(1);
		contributor.LastContext.ShouldNotBeNull();
		result.RecordsCleaned.ShouldBe(3);
	}

	[Fact]
	public async Task AggregateRecordsCleanedAcrossMultipleContributors()
	{
		var a = new RecordingRetentionContributor(RetentionContributorResult.Succeeded(2));
		var b = new RecordingRetentionContributor(RetentionContributorResult.Succeeded(5));
		var sut = CreateSut(new RetentionEnforcementOptions(), a, b);

		var result = await sut.EnforceRetentionAsync(CancellationToken.None);

		a.InvocationCount.ShouldBe(1);
		b.InvocationCount.ShouldBe(1);
		result.RecordsCleaned.ShouldBe(7);
	}

	[Fact]
	public async Task PropagateDryRunFlagToContributor()
	{
		var contributor = new RecordingRetentionContributor(RetentionContributorResult.Succeeded(0));
		var sut = CreateSut(new RetentionEnforcementOptions { DryRun = true }, contributor);

		var result = await sut.EnforceRetentionAsync(CancellationToken.None);

		result.IsDryRun.ShouldBeTrue();
		contributor.LastContext.ShouldNotBeNull();
		contributor.LastContext!.DryRun.ShouldBeTrue();
	}

	[Fact]
	public async Task FailOpenWhenAContributorThrows_StillRunningOthers()
	{
		// Arrange — first contributor throws; the second must still run and its count must still be reported.
		var throwing = new RecordingRetentionContributor(new InvalidOperationException("store unreachable"));
		var healthy = new RecordingRetentionContributor(RetentionContributorResult.Succeeded(4));
		var sut = CreateSut(new RetentionEnforcementOptions(), throwing, healthy);

		// Act — must not throw (fail-open per contributor).
		var result = await sut.EnforceRetentionAsync(CancellationToken.None);

		// Assert — the healthy contributor still ran; the thrown one contributed nothing.
		throwing.InvocationCount.ShouldBe(1);
		healthy.InvocationCount.ShouldBe(1);
		result.RecordsCleaned.ShouldBe(4);
	}

	[Fact]
	public async Task ReportZeroAndNotThrowWhenNoContributorsRegistered()
	{
		// Honest contract: no contributors -> nothing cleaned, no silent "success" with deletions.
		var sut = CreateSut(new RetentionEnforcementOptions());

		var result = await sut.EnforceRetentionAsync(CancellationToken.None);

		result.RecordsCleaned.ShouldBe(0);
	}

	[Fact]
	public async Task NotCountRecordsFromAContributorThatReportsFailure()
	{
		var failing = new RecordingRetentionContributor(RetentionContributorResult.Failed("partial outage"));
		var healthy = new RecordingRetentionContributor(RetentionContributorResult.Succeeded(6));
		var sut = CreateSut(new RetentionEnforcementOptions(), failing, healthy);

		var result = await sut.EnforceRetentionAsync(CancellationToken.None);

		failing.InvocationCount.ShouldBe(1);
		healthy.InvocationCount.ShouldBe(1);
		result.RecordsCleaned.ShouldBe(6);
	}

	/// <summary>
	/// Deterministic in-test <see cref="IRetentionContributor"/> double: records the context it was
	/// invoked with and returns a fixed result (or throws a fixed exception).
	/// </summary>
	private sealed class RecordingRetentionContributor : IRetentionContributor
	{
		private readonly RetentionContributorResult? _result;
		private readonly Exception? _throw;

		public RecordingRetentionContributor(RetentionContributorResult result) => _result = result;

		public RecordingRetentionContributor(Exception toThrow) => _throw = toThrow;

		public string Name => nameof(RecordingRetentionContributor);

		public int InvocationCount { get; private set; }

		public RetentionContributorContext? LastContext { get; private set; }

		public Task<RetentionContributorResult> EnforceAsync(
			RetentionContributorContext context,
			CancellationToken cancellationToken)
		{
			InvocationCount++;
			LastContext = context;

			if (_throw is not null)
			{
				throw _throw;
			}

			return Task.FromResult(_result!);
		}
	}
}
