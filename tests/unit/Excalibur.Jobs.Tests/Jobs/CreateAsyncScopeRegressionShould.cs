// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Jobs;
using Excalibur.Jobs.Jobs;
using Excalibur.Jobs.Quartz;

using FakeItEasy;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using Quartz;

namespace Excalibur.Jobs.Tests.Jobs;

/// <summary>
/// Regression test for Beads issue Excalibur.Dispatch-am5nrt:
/// Job classes used CreateScope() instead of CreateAsyncScope(), which throws
/// InvalidOperationException when a resolved service implements IAsyncDisposable.
/// IOutboxDispatcher inherits IAsyncDisposable, making this a real production scenario.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Component", "Platform")]
public sealed class CreateAsyncScopeRegressionShould
{
	[Fact]
	public async Task OutboxProcessorJob_NotThrow_WhenOutboxDispatcherIsAsyncDisposable()
	{
		// Arrange -- IOutboxDispatcher inherits IAsyncDisposable, so any real
		// implementation will trigger the bug when CreateScope + using is used.
		var services = new ServiceCollection();
		services.AddScoped<IOutboxDispatcher, FakeAsyncDisposableOutboxDispatcher>();
		services.AddLogging();
		var sp = services.BuildServiceProvider();
		var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();

		var sut = new OutboxProcessorJob(scopeFactory, NullLogger<OutboxProcessorJob>.Instance);

		// Act & Assert -- before the fix, scope disposal threw InvalidOperationException
		// because synchronous Dispose() cannot handle IAsyncDisposable services.
		await Should.NotThrowAsync(() => sut.ExecuteAsync(CancellationToken.None));

		// Verify DisposeAsync was called (proving the async path is used)
		FakeAsyncDisposableOutboxDispatcher.DisposeAsyncCalled.ShouldBeTrue(
			"CreateAsyncScope + await using should invoke DisposeAsync on IAsyncDisposable services");
	}

	[Fact]
	public async Task QuartzJobAdapter_NotThrow_WhenResolvedJobIsAsyncDisposable()
	{
		// Arrange -- register an IBackgroundJob that also implements IAsyncDisposable
		var services = new ServiceCollection();
		services.AddScoped<AsyncDisposableStubJob>();
		services.AddLogging();
		var sp = services.BuildServiceProvider();
		var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();

		var sut = new QuartzJobAdapter(scopeFactory, NullLogger<QuartzJobAdapter>.Instance);

		// QuartzJobAdapter resolves jobs by type from JobDataMap["JobType"]
		var jobContext = CreateFakeJobContext(typeof(AsyncDisposableStubJob));

		// Act & Assert -- before the fix, scope disposal threw InvalidOperationException
		await Should.NotThrowAsync(() => sut.Execute(jobContext));

		// Verify async disposal was invoked
		AsyncDisposableStubJob.DisposeAsyncCalled.ShouldBeTrue(
			"CreateAsyncScope + await using should invoke DisposeAsync on IAsyncDisposable job instances");
	}

	private static IJobExecutionContext CreateFakeJobContext(Type jobType)
	{
		var context = A.Fake<IJobExecutionContext>();
		var jobDetail = A.Fake<IJobDetail>();
		var jobDataMap = new JobDataMap
		{
			["JobType"] = jobType,
		};

		A.CallTo(() => context.JobDetail).Returns(jobDetail);
		A.CallTo(() => jobDetail.Key).Returns(new JobKey("test-job"));
		A.CallTo(() => jobDetail.JobDataMap).Returns(jobDataMap);
		A.CallTo(() => context.CancellationToken).Returns(CancellationToken.None);

		return context;
	}

	/// <summary>
	/// IOutboxDispatcher already extends IAsyncDisposable. This fake proves
	/// the production scenario: any scoped IOutboxDispatcher requires async disposal.
	/// </summary>
	private sealed class FakeAsyncDisposableOutboxDispatcher : IOutboxDispatcher
	{
		public static bool DisposeAsyncCalled { get; private set; }

		public Task<int> RunOutboxDispatchAsync(string dispatcherId, CancellationToken cancellationToken)
			=> Task.FromResult(0);

		public Task SaveEventsAsync(
			IReadOnlyCollection<IIntegrationEvent> integrationEvents,
			IMessageMetadata metadata,
			CancellationToken cancellationToken)
			=> Task.CompletedTask;

		public Task<int> SaveMessagesAsync(
			ICollection<IOutboxMessage> outboxMessages,
			CancellationToken cancellationToken)
			=> Task.FromResult(0);

		public Task<IEnumerable<IDispatchMessage>> GetPendingMessagesAsync(CancellationToken cancellationToken)
			=> Task.FromResult(Enumerable.Empty<IDispatchMessage>());

		public ValueTask DisposeAsync()
		{
			DisposeAsyncCalled = true;
			return ValueTask.CompletedTask;
		}
	}

	private sealed class AsyncDisposableStubJob : IBackgroundJob, IAsyncDisposable
	{
		public static bool DisposeAsyncCalled { get; private set; }

		public Task ExecuteAsync(CancellationToken cancellationToken) => Task.CompletedTask;

		public ValueTask DisposeAsync()
		{
			DisposeAsyncCalled = true;
			return ValueTask.CompletedTask;
		}
	}
}
