// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Amazon;
using Amazon.Runtime;
using Amazon.Scheduler;
using Amazon.Scheduler.Model;

using Excalibur.Jobs;
using Excalibur.Jobs.Aws;
using Excalibur.Testing.Conformance;

using FakeItEasy;

using Microsoft.Extensions.Logging;

namespace Excalibur.Jobs.Tests.CloudProviders;

/// <summary>
/// Conformance tests wiring <see cref="AwsSchedulerJobProvider"/> to
/// <see cref="SchedulerConformanceTestKit"/>. Proves the AWS provider passes the caller's cron expression
/// through to the EventBridge Scheduler <c>CreateSchedule</c> call rather than substituting a default.
/// </summary>
[SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores",
	Justification = "Conformance scenario method naming convention (matches sibling conformance kits).")]
[Trait("Category", "Unit")]
[Trait("Component", "Jobs")]
[Trait("Pattern", "SCHEDULER")]
public sealed class AwsSchedulerConformanceTests : SchedulerConformanceTestKit, IDisposable
{
	private CapturingAmazonSchedulerClient? _client;

	/// <inheritdoc/>
	protected override IJobSchedulerProvider CreateProvider()
	{
		_client = new CapturingAmazonSchedulerClient();
		return new AwsSchedulerJobProvider(
			_client,
			Microsoft.Extensions.Options.Options.Create(new AwsSchedulerOptions
			{
				TargetArn = "arn:aws:lambda:us-east-1:123456789012:function:jobs",
				ExecutionRoleArn = "arn:aws:iam::123456789012:role/excalibur-jobs",
				TimeZone = "UTC",
			}),
			A.Fake<ILogger<AwsSchedulerJobProvider>>());
	}

	/// <inheritdoc/>
	protected override Task<string?> GetCapturedCronExpressionAsync()
	{
		// The provider wraps the cron expression as $"cron({cronExpression})"; strip the wrapper so the
		// kit compares the raw cron string it supplied (the Liskov "no substitution" invariant).
		var captured = _client?.LastCreateRequest?.ScheduleExpression;
		if (captured is not null && captured.StartsWith("cron(", StringComparison.Ordinal)
			&& captured.EndsWith(')'))
		{
			captured = captured["cron(".Length..^1];
		}

		return Task.FromResult(captured);
	}

	/// <inheritdoc/>
	public void Dispose() => _client?.Dispose();

	[Fact]
	[RequiresUnreferencedCode("Conformance scenario drives ScheduleJobAsync, which may serialize the job payload via reflection.")]
	[RequiresDynamicCode("Conformance scenario drives ScheduleJobAsync, which may serialize the job payload via reflection.")]
	public Task ScheduleJobAsync_PassesCronExpressionToDownstreamCall_Test() =>
		ScheduleJobAsync_PassesCronExpressionToDownstreamCall();

	private sealed class CapturingAmazonSchedulerClient : AmazonSchedulerClient
	{
		public CreateScheduleRequest? LastCreateRequest { get; private set; }

		public CapturingAmazonSchedulerClient()
			: base(
				new AnonymousAWSCredentials(),
				new AmazonSchedulerConfig
				{
					RegionEndpoint = RegionEndpoint.USEast1,
					ServiceURL = "http://localhost",
				})
		{
		}

		public override Task<CreateScheduleResponse> CreateScheduleAsync(
			CreateScheduleRequest request,
			CancellationToken cancellationToken = default)
		{
			LastCreateRequest = request;
			return Task.FromResult(new CreateScheduleResponse());
		}
	}

	[Fact]
	public Task ConformanceSuite_ShouldWireEveryArm_Test() => ConformanceSuite_ShouldWireEveryArm();
}
