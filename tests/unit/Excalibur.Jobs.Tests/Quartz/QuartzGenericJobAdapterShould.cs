// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json;

using Excalibur.Jobs.Abstractions;
using Excalibur.Jobs.Quartz;

using FakeItEasy;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Quartz;

namespace Excalibur.Jobs.Tests.Quartz;

/// <summary>
/// Unit tests for <see cref="QuartzGenericJobAdapter{TJob, TContext}"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Jobs")]
public sealed class QuartzGenericJobAdapterShould
{
	private readonly GenericAdapterTestContextJob _job = new();
	private readonly ILogger<QuartzGenericJobAdapter<GenericAdapterTestContextJob, GenericAdapterTestJobContext>> _logger =
		NullLogger<QuartzGenericJobAdapter<GenericAdapterTestContextJob, GenericAdapterTestJobContext>>.Instance;

	[Fact]
	public void ThrowArgumentNullException_WhenJobIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new QuartzGenericJobAdapter<GenericAdapterTestContextJob, GenericAdapterTestJobContext>(null!, _logger));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenLoggerIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new QuartzGenericJobAdapter<GenericAdapterTestContextJob, GenericAdapterTestJobContext>(_job, null!));
	}

	[Fact]
	public async Task ThrowArgumentNullException_WhenContextIsNull()
	{
		// Arrange
		var adapter = new QuartzGenericJobAdapter<GenericAdapterTestContextJob, GenericAdapterTestJobContext>(_job, _logger);

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(
			() => adapter.Execute(null!));
	}

	[Fact]
	public async Task ExecuteJob_WhenContextIsDirectObject()
	{
		// Arrange
		var expectedContext = new GenericAdapterTestJobContext { Name = "test-value" };
		var adapter = new QuartzGenericJobAdapter<GenericAdapterTestContextJob, GenericAdapterTestJobContext>(_job, _logger);

		var jobDetail = A.Fake<IJobDetail>();
		var jobDataMap = new JobDataMap { ["Context"] = expectedContext };
		A.CallTo(() => jobDetail.JobDataMap).Returns(jobDataMap);
		A.CallTo(() => jobDetail.Key).Returns(new JobKey("test-job"));

		var executionContext = A.Fake<IJobExecutionContext>();
		A.CallTo(() => executionContext.JobDetail).Returns(jobDetail);
		A.CallTo(() => executionContext.CancellationToken).Returns(CancellationToken.None);

		// Act
		await adapter.Execute(executionContext);

		// Assert
		_job.ExecutedContext.ShouldNotBeNull();
		_job.ExecutedContext.Name.ShouldBe("test-value");
	}

	[Fact]
	public async Task ExecuteJob_WhenContextIsSerializedJson()
	{
		// Arrange
		var expectedContext = new GenericAdapterTestJobContext { Name = "json-value" };
		var json = JsonSerializer.Serialize(expectedContext);
		var adapter = new QuartzGenericJobAdapter<GenericAdapterTestContextJob, GenericAdapterTestJobContext>(_job, _logger);

		var jobDetail = A.Fake<IJobDetail>();
		var jobDataMap = new JobDataMap { ["Context"] = json };
		A.CallTo(() => jobDetail.JobDataMap).Returns(jobDataMap);
		A.CallTo(() => jobDetail.Key).Returns(new JobKey("test-job"));

		var executionContext = A.Fake<IJobExecutionContext>();
		A.CallTo(() => executionContext.JobDetail).Returns(jobDetail);
		A.CallTo(() => executionContext.CancellationToken).Returns(CancellationToken.None);

		// Act
		await adapter.Execute(executionContext);

		// Assert
		_job.ExecutedContext.ShouldNotBeNull();
		_job.ExecutedContext.Name.ShouldBe("json-value");
	}

	[Fact]
	public async Task ThrowInvalidOperationException_WhenContextIsNull()
	{
		// Arrange
		var adapter = new QuartzGenericJobAdapter<GenericAdapterTestContextJob, GenericAdapterTestJobContext>(_job, _logger);

		var jobDetail = A.Fake<IJobDetail>();
		var jobDataMap = new JobDataMap { ["Context"] = null! };
		A.CallTo(() => jobDetail.JobDataMap).Returns(jobDataMap);
		A.CallTo(() => jobDetail.Key).Returns(new JobKey("test-job"));

		var executionContext = A.Fake<IJobExecutionContext>();
		A.CallTo(() => executionContext.JobDetail).Returns(jobDetail);

		// Act & Assert
		await Should.ThrowAsync<InvalidOperationException>(
			() => adapter.Execute(executionContext));
	}

	[Fact]
	public async Task ThrowInvalidOperationException_WhenJsonContextIsInvalid()
	{
		// Arrange
		var adapter = new QuartzGenericJobAdapter<GenericAdapterTestContextJob, GenericAdapterTestJobContext>(_job, _logger);

		var jobDetail = A.Fake<IJobDetail>();
		var jobDataMap = new JobDataMap { ["Context"] = "{invalid-json" };
		A.CallTo(() => jobDetail.JobDataMap).Returns(jobDataMap);
		A.CallTo(() => jobDetail.Key).Returns(new JobKey("test-job"));

		var executionContext = A.Fake<IJobExecutionContext>();
		A.CallTo(() => executionContext.JobDetail).Returns(jobDetail);

		// Act & Assert
		await Should.ThrowAsync<InvalidOperationException>(
			() => adapter.Execute(executionContext));
	}

	[Fact]
	public async Task RethrowException_WhenJobExecutionFails()
	{
		// Arrange
		var failingJob = new GenericAdapterFailingContextJob();
		var adapter = new QuartzGenericJobAdapter<GenericAdapterFailingContextJob, GenericAdapterTestJobContext>(failingJob,
			NullLogger<QuartzGenericJobAdapter<GenericAdapterFailingContextJob, GenericAdapterTestJobContext>>.Instance);

		var context = new GenericAdapterTestJobContext { Name = "fail" };
		var jobDetail = A.Fake<IJobDetail>();
		var jobDataMap = new JobDataMap { ["Context"] = context };
		A.CallTo(() => jobDetail.JobDataMap).Returns(jobDataMap);
		A.CallTo(() => jobDetail.Key).Returns(new JobKey("test-job"));

		var executionContext = A.Fake<IJobExecutionContext>();
		A.CallTo(() => executionContext.JobDetail).Returns(jobDetail);
		A.CallTo(() => executionContext.CancellationToken).Returns(CancellationToken.None);

		// Act & Assert
		await Should.ThrowAsync<InvalidOperationException>(
			() => adapter.Execute(executionContext));
	}
}

internal sealed class GenericAdapterTestJobContext
{
	public string? Name { get; set; }
}

internal sealed class GenericAdapterTestContextJob : IBackgroundJob<GenericAdapterTestJobContext>
{
	public GenericAdapterTestJobContext? ExecutedContext { get; private set; }

	public Task ExecuteAsync(GenericAdapterTestJobContext context, CancellationToken cancellationToken)
	{
		ExecutedContext = context;
		return Task.CompletedTask;
	}
}

internal sealed class GenericAdapterFailingContextJob : IBackgroundJob<GenericAdapterTestJobContext>
{
	public Task ExecuteAsync(GenericAdapterTestJobContext context, CancellationToken cancellationToken) =>
		throw new InvalidOperationException("Job execution failed");
}
