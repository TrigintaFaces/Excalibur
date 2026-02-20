// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json;

using Amazon;
using Amazon.Runtime;
using Amazon.Scheduler;
using Amazon.Scheduler.Model;

using Excalibur.Jobs.Abstractions;
using Excalibur.Jobs.CloudProviders.Aws;

using FakeItEasy;

using Microsoft.Extensions.Logging;

namespace Excalibur.Jobs.Tests.CloudProviders;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class AwsSchedulerJobProviderShould
{
	[Fact]
	public void ThrowWhenSchedulerClientIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new AwsSchedulerJobProvider(null!, CreateOptions(), A.Fake<ILogger<AwsSchedulerJobProvider>>()));
	}

	[Fact]
	public void ThrowWhenOptionsIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new AwsSchedulerJobProvider(new TestAmazonSchedulerClient(), null!, A.Fake<ILogger<AwsSchedulerJobProvider>>()));
	}

	[Fact]
	public void ThrowWhenLoggerIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new AwsSchedulerJobProvider(new TestAmazonSchedulerClient(), CreateOptions(), null!));
	}

	[Fact]
	public async Task BuildCreateScheduleRequestFromInputs()
	{
		var client = new TestAmazonSchedulerClient();
		var provider = new AwsSchedulerJobProvider(client, CreateOptions(), A.Fake<ILogger<AwsSchedulerJobProvider>>());

		await provider.ScheduleJobAsync<TestBackgroundJob>("sync-orders", "0/5 * * * ? *", CancellationToken.None);

		client.LastCreateRequest.ShouldNotBeNull();
		client.LastCreateRequest.Name.ShouldBe("sync-orders");
		client.LastCreateRequest.ScheduleExpression.ShouldBe("cron(0/5 * * * ? *)");
		client.LastCreateRequest.ScheduleExpressionTimezone.ShouldBe("America/New_York");
		client.LastCreateRequest.Target.Arn.ShouldBe("arn:aws:lambda:us-east-1:123456789012:function:jobs");
		client.LastCreateRequest.Target.RoleArn.ShouldBe("arn:aws:iam::123456789012:role/excalibur-jobs");

		using var json = JsonDocument.Parse(client.LastCreateRequest.Target.Input);
		json.RootElement.GetProperty("JobName").GetString().ShouldBe("sync-orders");
		json.RootElement.GetProperty("JobType").GetString().ShouldContain(nameof(TestBackgroundJob));
	}

	[Fact]
	public async Task RethrowWhenCreateScheduleFails()
	{
		var client = new TestAmazonSchedulerClient
		{
			CreateException = new InvalidOperationException("create failed")
		};
		var provider = new AwsSchedulerJobProvider(client, CreateOptions(), A.Fake<ILogger<AwsSchedulerJobProvider>>());

		var ex = await Should.ThrowAsync<InvalidOperationException>(() =>
			provider.ScheduleJobAsync<TestBackgroundJob>("sync-orders", "0 * * * ? *", CancellationToken.None));

		ex.Message.ShouldBe("create failed");
	}

	[Fact]
	public async Task DeleteScheduleWhenDeleteJobIsCalled()
	{
		var client = new TestAmazonSchedulerClient();
		var provider = new AwsSchedulerJobProvider(client, CreateOptions(), A.Fake<ILogger<AwsSchedulerJobProvider>>());

		await provider.DeleteJobAsync("sync-orders", CancellationToken.None);

		client.LastDeleteRequest.ShouldNotBeNull();
		client.LastDeleteRequest.Name.ShouldBe("sync-orders");
	}

	[Fact]
	public async Task SwallowResourceNotFoundDuringDelete()
	{
		var client = new TestAmazonSchedulerClient
		{
			DeleteException = new ResourceNotFoundException("not found")
		};
		var provider = new AwsSchedulerJobProvider(client, CreateOptions(), A.Fake<ILogger<AwsSchedulerJobProvider>>());

		await provider.DeleteJobAsync("missing", CancellationToken.None);
	}

	[Fact]
	public async Task RethrowWhenDeleteFailsForOtherErrors()
	{
		var client = new TestAmazonSchedulerClient
		{
			DeleteException = new InvalidOperationException("delete failed")
		};
		var provider = new AwsSchedulerJobProvider(client, CreateOptions(), A.Fake<ILogger<AwsSchedulerJobProvider>>());

		var ex = await Should.ThrowAsync<InvalidOperationException>(() =>
			provider.DeleteJobAsync("sync-orders", CancellationToken.None));

		ex.Message.ShouldBe("delete failed");
	}

	[Fact]
	public async Task ThrowObjectDisposedAfterDispose()
	{
		var provider = new AwsSchedulerJobProvider(
			new TestAmazonSchedulerClient(),
			CreateOptions(),
			A.Fake<ILogger<AwsSchedulerJobProvider>>());
		provider.Dispose();

		await Should.ThrowAsync<ObjectDisposedException>(() =>
			provider.ScheduleJobAsync<TestBackgroundJob>("sync-orders", "0 * * * ? *", CancellationToken.None));

		await Should.ThrowAsync<ObjectDisposedException>(() =>
			provider.DeleteJobAsync("sync-orders", CancellationToken.None));
	}

	private static AwsSchedulerOptions CreateOptions() =>
		new()
		{
			TargetArn = "arn:aws:lambda:us-east-1:123456789012:function:jobs",
			ExecutionRoleArn = "arn:aws:iam::123456789012:role/excalibur-jobs",
			TimeZone = "America/New_York"
		};

	private sealed class TestBackgroundJob : IBackgroundJob
	{
		public Task ExecuteAsync(CancellationToken cancellationToken) => Task.CompletedTask;
	}

	private sealed class TestAmazonSchedulerClient : AmazonSchedulerClient
	{
		public CreateScheduleRequest? LastCreateRequest { get; private set; }
		public DeleteScheduleRequest? LastDeleteRequest { get; private set; }
		public Exception? CreateException { get; init; }
		public Exception? DeleteException { get; init; }

		public TestAmazonSchedulerClient()
			: base(
				new AnonymousAWSCredentials(),
				new AmazonSchedulerConfig
				{
					RegionEndpoint = RegionEndpoint.USEast1,
					ServiceURL = "http://localhost",
				})
		{
		}

		public override Task<CreateScheduleResponse> CreateScheduleAsync(CreateScheduleRequest request, CancellationToken cancellationToken = default)
		{
			LastCreateRequest = request;
			if (CreateException is not null)
			{
				throw CreateException;
			}

			return Task.FromResult(new CreateScheduleResponse());
		}

		public override Task<DeleteScheduleResponse> DeleteScheduleAsync(DeleteScheduleRequest request, CancellationToken cancellationToken = default)
		{
			LastDeleteRequest = request;
			if (DeleteException is not null)
			{
				throw DeleteException;
			}

			return Task.FromResult(new DeleteScheduleResponse());
		}
	}
}
