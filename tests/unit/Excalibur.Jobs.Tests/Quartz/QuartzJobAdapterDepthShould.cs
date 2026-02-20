// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Jobs.Abstractions;
using Excalibur.Jobs.Quartz;

using FakeItEasy;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Quartz;

namespace Excalibur.Jobs.Tests.Quartz;

/// <summary>
/// Depth tests for <see cref="QuartzJobAdapter"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Jobs")]
public sealed class QuartzJobAdapterDepthShould
{
	[Fact]
	public void ThrowArgumentNullException_WhenScopeFactoryIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new QuartzJobAdapter(null!, NullLogger<QuartzJobAdapter>.Instance));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenLoggerIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new QuartzJobAdapter(A.Fake<IServiceScopeFactory>(), null!));
	}

	[Fact]
	public async Task ThrowArgumentNullException_WhenContextIsNull()
	{
		// Arrange
		var adapter = new QuartzJobAdapter(A.Fake<IServiceScopeFactory>(), NullLogger<QuartzJobAdapter>.Instance);

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(
			() => adapter.Execute(null!));
	}

	[Fact]
	public async Task ExecuteJob_WhenJobTypeIsType()
	{
		// Arrange
		var job = new AdapterTestBackgroundJob();
		var services = new ServiceCollection();
		services.AddSingleton<AdapterTestBackgroundJob>(job);
		var sp = services.BuildServiceProvider();
		var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();

		var adapter = new QuartzJobAdapter(scopeFactory, NullLogger<QuartzJobAdapter>.Instance);

		var jobDetail = A.Fake<IJobDetail>();
		var jobDataMap = new JobDataMap { ["JobType"] = typeof(AdapterTestBackgroundJob) };
		A.CallTo(() => jobDetail.JobDataMap).Returns(jobDataMap);
		A.CallTo(() => jobDetail.Key).Returns(new JobKey("test-job"));

		var executionContext = A.Fake<IJobExecutionContext>();
		A.CallTo(() => executionContext.JobDetail).Returns(jobDetail);
		A.CallTo(() => executionContext.CancellationToken).Returns(CancellationToken.None);

		// Act
		await adapter.Execute(executionContext);

		// Assert
		job.Executed.ShouldBeTrue();
	}

	[Fact]
	public async Task ExecuteJob_WhenJobTypeIsString()
	{
		// Arrange
		var job = new AdapterTestBackgroundJob();
		var services = new ServiceCollection();
		services.AddSingleton<AdapterTestBackgroundJob>(job);
		var sp = services.BuildServiceProvider();
		var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();

		var adapter = new QuartzJobAdapter(scopeFactory, NullLogger<QuartzJobAdapter>.Instance);

		var jobDetail = A.Fake<IJobDetail>();
		var typeName = typeof(AdapterTestBackgroundJob).AssemblyQualifiedName!;
		var jobDataMap = new JobDataMap { ["JobType"] = typeName };
		A.CallTo(() => jobDetail.JobDataMap).Returns(jobDataMap);
		A.CallTo(() => jobDetail.Key).Returns(new JobKey("test-job"));

		var executionContext = A.Fake<IJobExecutionContext>();
		A.CallTo(() => executionContext.JobDetail).Returns(jobDetail);
		A.CallTo(() => executionContext.CancellationToken).Returns(CancellationToken.None);

		// Act
		await adapter.Execute(executionContext);

		// Assert
		job.Executed.ShouldBeTrue();
	}

	[Fact]
	public async Task ThrowInvalidOperationException_WhenJobTypeIsNullData()
	{
		// Arrange
		var adapter = new QuartzJobAdapter(A.Fake<IServiceScopeFactory>(), NullLogger<QuartzJobAdapter>.Instance);

		var jobDetail = A.Fake<IJobDetail>();
		var jobDataMap = new JobDataMap { ["JobType"] = 12345 }; // neither Type nor string
		A.CallTo(() => jobDetail.JobDataMap).Returns(jobDataMap);
		A.CallTo(() => jobDetail.Key).Returns(new JobKey("test-job"));

		var executionContext = A.Fake<IJobExecutionContext>();
		A.CallTo(() => executionContext.JobDetail).Returns(jobDetail);

		// Act & Assert
		await Should.ThrowAsync<InvalidOperationException>(
			() => adapter.Execute(executionContext));
	}

	[Fact]
	public async Task ThrowInvalidOperationException_WhenJobTypeCannotBeResolved()
	{
		// Arrange
		var services = new ServiceCollection();
		var sp = services.BuildServiceProvider();
		var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();

		var adapter = new QuartzJobAdapter(scopeFactory, NullLogger<QuartzJobAdapter>.Instance);

		var jobDetail = A.Fake<IJobDetail>();
		var jobDataMap = new JobDataMap { ["JobType"] = typeof(AdapterTestBackgroundJob) };
		A.CallTo(() => jobDetail.JobDataMap).Returns(jobDataMap);
		A.CallTo(() => jobDetail.Key).Returns(new JobKey("test-job"));

		var executionContext = A.Fake<IJobExecutionContext>();
		A.CallTo(() => executionContext.JobDetail).Returns(jobDetail);

		// Act & Assert
		await Should.ThrowAsync<InvalidOperationException>(
			() => adapter.Execute(executionContext));
	}

	[Fact]
	public async Task ThrowInvalidOperationException_WhenJobDoesNotImplementIBackgroundJob()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddSingleton(new AdapterNonBackgroundJob());
		var sp = services.BuildServiceProvider();
		var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();

		var adapter = new QuartzJobAdapter(scopeFactory, NullLogger<QuartzJobAdapter>.Instance);

		var jobDetail = A.Fake<IJobDetail>();
		var jobDataMap = new JobDataMap { ["JobType"] = typeof(AdapterNonBackgroundJob) };
		A.CallTo(() => jobDetail.JobDataMap).Returns(jobDataMap);
		A.CallTo(() => jobDetail.Key).Returns(new JobKey("test-job"));

		var executionContext = A.Fake<IJobExecutionContext>();
		A.CallTo(() => executionContext.JobDetail).Returns(jobDetail);
		A.CallTo(() => executionContext.CancellationToken).Returns(CancellationToken.None);

		// Act & Assert
		await Should.ThrowAsync<InvalidOperationException>(
			() => adapter.Execute(executionContext));
	}

	[Fact]
	public async Task RethrowException_WhenJobExecutionFails()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddSingleton<AdapterFailingBackgroundJob>();
		var sp = services.BuildServiceProvider();
		var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();

		var adapter = new QuartzJobAdapter(scopeFactory, NullLogger<QuartzJobAdapter>.Instance);

		var jobDetail = A.Fake<IJobDetail>();
		var jobDataMap = new JobDataMap { ["JobType"] = typeof(AdapterFailingBackgroundJob) };
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

internal sealed class AdapterTestBackgroundJob : IBackgroundJob
{
	public bool Executed { get; private set; }

	public Task ExecuteAsync(CancellationToken cancellationToken)
	{
		Executed = true;
		return Task.CompletedTask;
	}
}

internal sealed class AdapterFailingBackgroundJob : IBackgroundJob
{
	public Task ExecuteAsync(CancellationToken cancellationToken) =>
		throw new InvalidOperationException("Job failed");
}

internal sealed class AdapterNonBackgroundJob;
