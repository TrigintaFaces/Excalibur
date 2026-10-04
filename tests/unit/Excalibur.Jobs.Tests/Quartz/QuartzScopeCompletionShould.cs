// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Jobs.Core;
using Excalibur.Jobs.Quartz;
using FakeItEasy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Quartz;

namespace Excalibur.Jobs.Tests.Quartz;

[Trait("Category", "Unit")]
[Trait("Component", "Jobs")]
[Trait("Pattern", "Regression")]
public sealed class QuartzScopeCompletionShould
{
	[Theory]
	[InlineData(false, false)]
	[InlineData(true, false)]
	[InlineData(false, true)]
	public async Task PublishHeartbeatOnlyAfterSuccessfulScopeCleanup(bool failDisposal, bool cancelDuringDisposal)
	{
		var probe = new DisposalProbe { Fail = failDisposal };
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddSingleton<JobHeartbeatTracker>();
		services.AddScoped(_ => probe);
		new JobConfigurator(services).AddJob<ScopedJob>("0 0 * * * ?", "scope-probe");
		await using var provider = services.BuildServiceProvider();
		var adapter = new QuartzJobAdapter(provider.GetRequiredService<IServiceScopeFactory>(), NullLogger<QuartzJobAdapter>.Instance);
		var context = A.Fake<IJobExecutionContext>();
		var detail = JobBuilder.Create<QuartzJobAdapter>().WithIdentity("scope-probe")
			.UsingJobData("JobType", typeof(ScopedJob).AssemblyQualifiedName!).Build();
		A.CallTo(() => context.JobDetail).Returns(detail);
		using var cancellation = new CancellationTokenSource();
		var execution = adapter.Execute(context, cancellation.Token).AsTask();
		await probe.Disposing.Task.WaitAsync(TimeSpan.FromSeconds(10));
		var heartbeat = provider.GetRequiredService<JobHeartbeatTracker>();
		heartbeat.GetLastHeartbeat("scope-probe").ShouldBeNull();
		execution.IsCompleted.ShouldBeFalse();
		if (cancelDuringDisposal) { await cancellation.CancelAsync(); }
		probe.Release.TrySetResult();
		if (failDisposal) { await Should.ThrowAsync<InvalidOperationException>(() => execution); }
		else if (cancelDuringDisposal) { await Should.ThrowAsync<OperationCanceledException>(() => execution); }
		else { await execution; }
		(heartbeat.GetLastHeartbeat("scope-probe") is not null).ShouldBe(!failDisposal && !cancelDuringDisposal);
	}

	internal sealed class ScopedJob(DisposalProbe probe) : IBackgroundJob
	{
		public Task ExecuteAsync(CancellationToken cancellationToken)
		{
			GC.KeepAlive(probe);
			return Task.CompletedTask;
		}
	}
	internal sealed class DisposalProbe : IAsyncDisposable
	{
		public bool Fail { get; init; }
		public TaskCompletionSource Disposing { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
		public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
		public async ValueTask DisposeAsync()
		{
			Disposing.TrySetResult();
			await Release.Task;
			if (Fail) { throw new InvalidOperationException("scope cleanup failed"); }
		}
	}
}
