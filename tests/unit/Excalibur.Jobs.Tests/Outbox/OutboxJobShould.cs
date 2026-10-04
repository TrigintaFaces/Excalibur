// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Jobs.Core;
using Excalibur.Jobs.Outbox;

using FakeItEasy;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Quartz;

namespace Excalibur.Jobs.Tests.Outbox;

/// <summary>
/// Unit tests for <see cref="OutboxJob"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Jobs")]
public sealed class OutboxJobShould
{
	private readonly IOutboxDispatcher _fakeOutbox;
	private readonly JobHeartbeatTracker _heartbeatTracker;

	public OutboxJobShould()
	{
		_fakeOutbox = A.Fake<IOutboxDispatcher>();
		_heartbeatTracker = new JobHeartbeatTracker();
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task HonorConfiguredDispatcherWhetherOrNotAProcessorIsRegistered(bool registerProcessor)
	{
		var processor = A.Fake<IOutboxProcessor>();
		var services = new ServiceCollection();
		services.AddSingleton(_fakeOutbox);
		services.AddSingleton(_heartbeatTracker);
		services.AddLogging();
		services.AddTransient<OutboxJob>();
		if (registerProcessor)
		{
			services.AddSingleton(processor);
		}
		await using var provider = services.BuildServiceProvider();
		var job = provider.GetRequiredService<OutboxJob>();
		var context = CreateJobExecutionContext("custom", "DEFAULT");
		await job.Execute(context, CancellationToken.None);
		await job.Execute(context, CancellationToken.None);
		A.CallTo(() => _fakeOutbox.RunOutboxDispatchAsync(A<string>._, A<CancellationToken>._)).MustHaveHappenedTwiceExactly();
		A.CallTo(() => processor.DispatchPendingMessagesAsync(A<CancellationToken>._)).MustNotHaveHappened();
	}

	// --- Constructor null guards ---

	[Fact]
	public void ThrowWhenOutboxIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new OutboxJob(null!, _heartbeatTracker, NullLogger<OutboxJob>.Instance));
	}

	[Fact]
	public void ThrowWhenHeartbeatTrackerIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new OutboxJob(_fakeOutbox, null!, NullLogger<OutboxJob>.Instance));
	}

	[Fact]
	public void ThrowWhenLoggerIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new OutboxJob(_fakeOutbox, _heartbeatTracker, null!));
	}

	// --- Execute ---

	[Fact]
	public async Task ExecuteThrowWhenContextIsNull()
	{
		// Arrange
		var sut = new OutboxJob(_fakeOutbox, _heartbeatTracker, NullLogger<OutboxJob>.Instance);

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(() =>
			sut.Execute(null!));
	}

	[Fact]
	public async Task ExecuteCallRunOutboxDispatch()
	{
		// Arrange
		var sut = new OutboxJob(_fakeOutbox, _heartbeatTracker, NullLogger<OutboxJob>.Instance);
		A.CallTo(() => _fakeOutbox.RunOutboxDispatchAsync(A<string>._, A<CancellationToken>._))
			.Returns(3);

		var context = CreateJobExecutionContext("OutboxJob", "DEFAULT");

		// Act
		await sut.Execute(context);

		// Assert
		A.CallTo(() => _fakeOutbox.RunOutboxDispatchAsync(A<string>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ExecuteRecordHeartbeatOnSuccess()
	{
		// Arrange
		var sut = new OutboxJob(_fakeOutbox, _heartbeatTracker, NullLogger<OutboxJob>.Instance);
		A.CallTo(() => _fakeOutbox.RunOutboxDispatchAsync(A<string>._, A<CancellationToken>._))
			.Returns(1);

		var context = CreateJobExecutionContext("OutboxJob", "DEFAULT");

		// Act
		await sut.Execute(context);

		// Assert — heartbeat should be recorded
		_heartbeatTracker.GetLastHeartbeat("OutboxJob").ShouldNotBeNull();
	}

	[Fact]
	public async Task ExecuteReportFailureWithoutImmediateRefire()
	{
		// Arrange
		var sut = new OutboxJob(_fakeOutbox, _heartbeatTracker, NullLogger<OutboxJob>.Instance);
		A.CallTo(() => _fakeOutbox.RunOutboxDispatchAsync(A<string>._, A<CancellationToken>._))
			.ThrowsAsync(new InvalidOperationException("Outbox failure"));

		var context = CreateJobExecutionContext("OutboxJob", "DEFAULT");

		// Report failure without an immediate retry or a success heartbeat.
		var error = await Should.ThrowAsync<JobExecutionException>(() => sut.Execute(context));
		error.RefireImmediately.ShouldBeFalse();
		_heartbeatTracker.GetLastHeartbeat("OutboxJob").ShouldBeNull();
	}

	// --- bd-gh8ov8: OutboxJob MUST NOT dispose its injected SINGLETON IOutboxDispatcher (AC-12, EC-7) ---
	// Independent regression lock (author≠impl, TestsDeveloper). IOutboxDispatcher is registered
	// TryAddSingleton; Quartz resolves the job per fire but injects the SAME singleton every time, so the
	// job must NOT own/dispose its lifetime. The pre-fix code wrapped the dispatch in
	// `await using (_outbox.ConfigureAwait(false))`, which disposed the singleton on the first fire and
	// bricked it (ObjectDisposedException) for every subsequent fire and for every other consumer.
	// Load-bearing assertion: DisposeAsync is NEVER called. RED on the pre-fix `await using`; GREEN after.

	[Fact]
	public async Task NotDisposeInjectedSingletonDispatcherAcrossConsecutiveFires()
	{
		// Arrange — a single shared dispatcher instance, exactly as DI injects the singleton into each fire.
		A.CallTo(() => _fakeOutbox.RunOutboxDispatchAsync(A<string>._, A<CancellationToken>._))
			.Returns(2);
		var sut = new OutboxJob(_fakeOutbox, _heartbeatTracker, NullLogger<OutboxJob>.Instance);
		var context = CreateJobExecutionContext("OutboxJob", "DEFAULT");

		// Act — fire twice in succession (a real scheduler reuses the singleton across fires).
		await sut.Execute(context);
		await sut.Execute(context);

		// Assert — the job must never own/dispose the injected singleton's lifetime. EC-7: other
		// consumers (e.g. AuditMiddleware) must never observe it disposed. This is the load-bearing
		// structural lock — it is RED on the pre-fix `await using (_outbox)`.
		A.CallTo(() => _fakeOutbox.DisposeAsync()).MustNotHaveHappened();

		// …and both fires actually dispatched (proves the second fire ran against a live dispatcher).
		A.CallTo(() => _fakeOutbox.RunOutboxDispatchAsync(A<string>._, A<CancellationToken>._))
			.MustHaveHappenedTwiceExactly();
	}

	// --- JobConfigSectionName ---

	[Fact]
	public void HaveCorrectJobConfigSectionName()
	{
		// Assert
		OutboxJob.JobConfigSectionName.ShouldBe("Jobs:OutboxJob");
	}

	// --- ConfigureJob honors the Disabled flag (Excalibur.Dispatch-ku1i3e) ---
	// IQuartzBuilder cannot be faked, so these drive the real Quartz
	// configurator and inspect the resulting QuartzOptions for the registered job detail.
	// Inspecting options (rather than building a scheduler) keeps the test deterministic — it avoids
	// Quartz's process-global SchedulerRepository, which is shared across parallel test classes.

	[Fact]
	public async Task ConfigureJobDoesNotRegisterJobWhenDisabled()
	{
		// Arrange — Disabled:true must mean the job is never registered with the scheduler.
		var config = BuildJobConfig(disabled: true);

		// Act
		var registered = await IsJobRegistered(config);

		// Assert
		registered.ShouldBeFalse();
	}

	[Fact]
	public async Task ConfigureJobRegistersJobWhenEnabled()
	{
		// Arrange — control case: proves the assertion above tests the Disabled gate, not a wiring slip.
		var config = BuildJobConfig(disabled: false);

		// Act
		var registered = await IsJobRegistered(config);

		// Assert
		registered.ShouldBeTrue();
	}

	private static async Task<bool> IsJobRegistered(IConfiguration config)
	{
		var services = new ServiceCollection();
		_ = services.AddQuartz(q => OutboxJob.ConfigureJob(q, config));
		services.AddLogging();
		services.AddQuartz(q => q.ConfigureScheduler(o => o.InstanceName = Guid.NewGuid().ToString()));
		await using var provider = services.BuildServiceProvider();
		var scheduler = await provider.GetRequiredService<ISchedulerFactory>().GetScheduler();
		try
		{
			return await scheduler.GetJobDetail(new JobKey("OutboxJob", "TestGroup")) is not null;
		}
		finally
		{
			await scheduler.Shutdown();
		}
	}

	private static IConfiguration BuildJobConfig(bool disabled) =>
		new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
			{
				["Jobs:OutboxJob:JobName"] = "OutboxJob",
				["Jobs:OutboxJob:JobGroup"] = "TestGroup",
				["Jobs:OutboxJob:CronSchedule"] = "0/30 * * * * ?",
				["Jobs:OutboxJob:Disabled"] = disabled ? "true" : "false",
			})
			.Build();

	// --- Helpers ---

	private static IJobExecutionContext CreateJobExecutionContext(string jobName, string group)
	{
		var fakeContext = A.Fake<IJobExecutionContext>();

		var jobDetail = JobBuilder.Create<OutboxJob>()
			.WithIdentity(jobName, group)
			.Build();

		A.CallTo(() => fakeContext.JobDetail).Returns(jobDetail);
		A.CallTo(() => fakeContext.CancellationToken).Returns(CancellationToken.None);

		return fakeContext;
	}
}
