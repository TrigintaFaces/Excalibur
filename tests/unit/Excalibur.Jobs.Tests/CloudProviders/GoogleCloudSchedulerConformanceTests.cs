// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Jobs;
using Excalibur.Jobs.GoogleCloud;
using Excalibur.Testing.Conformance;

using Google.Api.Gax.ResourceNames;
using Google.Cloud.Scheduler.V1;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Jobs.Tests.CloudProviders;

/// <summary>
/// Conformance tests wiring <see cref="GoogleCloudSchedulerJobProvider"/> to
/// <see cref="SchedulerConformanceTestKit"/>. Proves the Google provider passes the caller's cron
/// expression through to the Cloud Scheduler <c>CreateJob</c> call (the <see cref="Job.Schedule"/> field)
/// rather than substituting a default.
/// </summary>
[SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores",
	Justification = "Conformance scenario method naming convention (matches sibling conformance kits).")]
[Trait("Category", "Unit")]
[Trait("Component", "Jobs")]
[Trait("Pattern", "SCHEDULER")]
public sealed class GoogleCloudSchedulerConformanceTests : SchedulerConformanceTestKit
{
	private CapturingCloudSchedulerClient? _schedulerClient;

	/// <inheritdoc/>
	protected override IJobSchedulerProvider CreateProvider()
	{
		// ADR-142 §D7: capture via a first-party subclass override, NOT a concrete-SDK fake of the
		// scheduler client (banned by NoConcreteSdkFakesGovernanceShould). Mirrors the AWS sibling.
		_schedulerClient = new CapturingCloudSchedulerClient();

		return new GoogleCloudSchedulerJobProvider(
			_schedulerClient,
			Microsoft.Extensions.Options.Options.Create(new GoogleCloudSchedulerOptions
			{
				ProjectId = "conformance-project",
				LocationId = "us-central1",
				TargetUrl = "https://jobs.example.com/execute",
				TimeZone = "UTC",
			}),
			NullLogger<GoogleCloudSchedulerJobProvider>.Instance);
	}

	/// <inheritdoc/>
	protected override Task<string?> GetCapturedCronExpressionAsync() =>
		Task.FromResult(_schedulerClient?.CapturedSchedule);

	[Fact]
	[RequiresUnreferencedCode("Conformance scenario drives ScheduleJobAsync, which may serialize the job payload via reflection.")]
	[RequiresDynamicCode("Conformance scenario drives ScheduleJobAsync, which may serialize the job payload via reflection.")]
	public Task ScheduleJobAsync_PassesCronExpressionToDownstreamCall_Test() =>
		ScheduleJobAsync_PassesCronExpressionToDownstreamCall();

	/// <summary>
	/// First-party capturing subclass of the Google SDK client (ADR-142 §D7 seam-adapter pattern):
	/// overrides the virtual <c>CreateJobAsync</c> to record the caller's <see cref="Job.Schedule"/>
	/// without faking the concrete SDK type.
	/// </summary>
	private sealed class CapturingCloudSchedulerClient : CloudSchedulerClient
	{
		public string? CapturedSchedule { get; private set; }

		public override Task<Job> CreateJobAsync(
			LocationName parent, Job job, CancellationToken cancellationToken = default)
		{
			CapturedSchedule = job.Schedule;
			return Task.FromResult(new Job());
		}
	}

	[Fact]
	public Task ConformanceSuite_ShouldWireEveryArm_Test() => ConformanceSuite_ShouldWireEveryArm();
}
