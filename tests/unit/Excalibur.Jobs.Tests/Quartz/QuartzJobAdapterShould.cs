// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Jobs.Abstractions;
using Excalibur.Jobs.Quartz;

using FakeItEasy;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using Quartz;
using Quartz.Impl;

namespace Excalibur.Jobs.Tests.Quartz;

public sealed class QuartzJobAdapterShould
{
	private readonly IServiceScopeFactory _fakeScopeFactory;
	private readonly IServiceScope _fakeScope;
	private readonly IServiceProvider _fakeServiceProvider;
	private readonly QuartzJobAdapter _sut;

	public QuartzJobAdapterShould()
	{
		_fakeScopeFactory = A.Fake<IServiceScopeFactory>();
		_fakeScope = A.Fake<IServiceScope>();
		_fakeServiceProvider = A.Fake<IServiceProvider>();

		A.CallTo(() => _fakeScopeFactory.CreateScope()).Returns(_fakeScope);
		A.CallTo(() => _fakeScope.ServiceProvider).Returns(_fakeServiceProvider);

		_sut = new QuartzJobAdapter(
			_fakeScopeFactory,
			NullLogger<QuartzJobAdapter>.Instance);
	}

	[Fact]
	public void ThrowOnNullScopeFactory()
	{
		Should.Throw<ArgumentNullException>(() =>
			new QuartzJobAdapter(null!, NullLogger<QuartzJobAdapter>.Instance));
	}

	[Fact]
	public void ThrowOnNullLogger()
	{
		Should.Throw<ArgumentNullException>(() =>
			new QuartzJobAdapter(_fakeScopeFactory, null!));
	}

	[Fact]
	public async Task ThrowOnNullContext()
	{
		await Should.ThrowAsync<ArgumentNullException>(() =>
			_sut.Execute(null!));
	}

	[Fact]
	public async Task ResolveAndExecuteJobByType()
	{
		var fakeJob = A.Fake<IBackgroundJob>();
		var jobType = typeof(FakeTestJob);

		A.CallTo(() => _fakeServiceProvider.GetService(jobType))
			.Returns(fakeJob);

		var context = CreateJobExecutionContext(jobType);

		await _sut.Execute(context);

		A.CallTo(() => fakeJob.ExecuteAsync(A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ResolveJobByTypeName()
	{
		var fakeJob = A.Fake<IBackgroundJob>();
		var jobType = typeof(FakeTestJob);
		var typeName = jobType.AssemblyQualifiedName;

		A.CallTo(() => _fakeServiceProvider.GetService(jobType))
			.Returns(fakeJob);

		var context = CreateJobExecutionContext(typeName);

		await _sut.Execute(context);

		A.CallTo(() => fakeJob.ExecuteAsync(A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ThrowWhenJobTypeNotFound()
	{
		// Provide an invalid type name
		var context = CreateJobExecutionContext("NonExistent.Type, NonExistent");

		await Should.ThrowAsync<InvalidOperationException>(() =>
			_sut.Execute(context));
	}

	[Fact]
	public async Task ThrowWhenJobCannotBeResolved()
	{
		var jobType = typeof(FakeTestJob);
		A.CallTo(() => _fakeServiceProvider.GetService(jobType))
			.Returns(null);

		var context = CreateJobExecutionContext(jobType);

		await Should.ThrowAsync<InvalidOperationException>(() =>
			_sut.Execute(context));
	}

	[Fact]
	public async Task ThrowWhenJobDoesNotImplementInterface()
	{
		// Return an object that doesn't implement IBackgroundJob
		A.CallTo(() => _fakeServiceProvider.GetService(typeof(FakeTestJob)))
			.Returns("not a job");

		var context = CreateJobExecutionContext(typeof(FakeTestJob));

		await Should.ThrowAsync<InvalidOperationException>(() =>
			_sut.Execute(context));
	}

	[Fact]
	public async Task RethrowExceptionFromJob()
	{
		var fakeJob = A.Fake<IBackgroundJob>();
		var exception = new InvalidOperationException("Job failed");
		A.CallTo(() => fakeJob.ExecuteAsync(A<CancellationToken>._))
			.ThrowsAsync(exception);

		A.CallTo(() => _fakeServiceProvider.GetService(typeof(FakeTestJob)))
			.Returns(fakeJob);

		var context = CreateJobExecutionContext(typeof(FakeTestJob));

		var thrown = await Should.ThrowAsync<InvalidOperationException>(() =>
			_sut.Execute(context));

		thrown.ShouldBeSameAs(exception);
	}

	private static IJobExecutionContext CreateJobExecutionContext(object jobTypeData)
	{
		var fakeContext = A.Fake<IJobExecutionContext>();
		var jobDataMap = new JobDataMap { ["JobType"] = jobTypeData };

		var jobDetail = JobBuilder.Create<QuartzJobAdapter>()
			.WithIdentity("test-job")
			.UsingJobData(jobDataMap)
			.Build();

		A.CallTo(() => fakeContext.JobDetail).Returns(jobDetail);

		return fakeContext;
	}

}

/// <summary>
/// Test stub for job type resolution. Not executed directly.
/// </summary>
public sealed class FakeTestJob : IBackgroundJob
{
	public Task ExecuteAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
