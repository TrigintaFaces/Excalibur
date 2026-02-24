using Excalibur.Dispatch.Compliance;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Compliance.Tests.Erasure;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class ErasureSchedulerBackgroundServiceShould
{
	[Fact]
	public void Throw_for_null_scope_factory()
	{
		Should.Throw<ArgumentNullException>(() =>
			new ErasureSchedulerBackgroundService(
				null!,
				Microsoft.Extensions.Options.Options.Create(new ErasureSchedulerOptions()),
				NullLogger<ErasureSchedulerBackgroundService>.Instance));
	}

	[Fact]
	public void Throw_for_null_options()
	{
		var scopeFactory = A.Fake<IServiceScopeFactory>();

		Should.Throw<ArgumentNullException>(() =>
			new ErasureSchedulerBackgroundService(
				scopeFactory,
				null!,
				NullLogger<ErasureSchedulerBackgroundService>.Instance));
	}

	[Fact]
	public void Throw_for_null_logger()
	{
		var scopeFactory = A.Fake<IServiceScopeFactory>();

		Should.Throw<ArgumentNullException>(() =>
			new ErasureSchedulerBackgroundService(
				scopeFactory,
				Microsoft.Extensions.Options.Options.Create(new ErasureSchedulerOptions()),
				null!));
	}

	[Fact]
	public async Task Exit_immediately_when_disabled()
	{
		var scopeFactory = A.Fake<IServiceScopeFactory>();
		var options = new ErasureSchedulerOptions { Enabled = false };
		var sut = new ErasureSchedulerBackgroundService(
			scopeFactory,
			Microsoft.Extensions.Options.Options.Create(options),
			NullLogger<ErasureSchedulerBackgroundService>.Instance);

		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
		await sut.StartAsync(cts.Token).ConfigureAwait(false);
		await sut.StopAsync(CancellationToken.None).ConfigureAwait(false);

		// No scope should be created when scheduler is disabled
		A.CallTo(() => scopeFactory.CreateScope()).MustNotHaveHappened();
	}

	[Fact]
	public async Task Process_scheduled_erasures_when_enabled()
	{
		var erasureStore = A.Fake<IErasureStore>();
		var erasureService = A.Fake<IErasureService>();
		var queryStore = A.Fake<IErasureQueryStore>();
		var queryObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

		A.CallTo(() => erasureStore.GetService(typeof(IErasureQueryStore)))
			.Returns(queryStore);

		A.CallTo(() => queryStore.GetScheduledRequestsAsync(A<int>._, A<CancellationToken>._))
			.ReturnsLazily(() =>
			{
				_ = queryObserved.TrySetResult();
				return Task.FromResult<IReadOnlyList<ErasureStatus>>([]);
			});

		var (scopeFactory, _) = SetupScopeFactory(erasureStore, erasureService);

		var options = new ErasureSchedulerOptions
		{
			Enabled = true,
			PollingInterval = TimeSpan.FromMilliseconds(50)
		};

		var sut = new ErasureSchedulerBackgroundService(
			scopeFactory,
			Microsoft.Extensions.Options.Options.Create(options),
			NullLogger<ErasureSchedulerBackgroundService>.Instance);

		using var cts = new CancellationTokenSource();
		await sut.StartAsync(cts.Token).ConfigureAwait(false);
		await queryObserved.Task.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken.None).ConfigureAwait(false);
		await sut.StopAsync(CancellationToken.None).ConfigureAwait(false);

		A.CallTo(() => queryStore.GetScheduledRequestsAsync(A<int>._, A<CancellationToken>._))
			.MustHaveHappened();
	}

	[Fact]
	public async Task Execute_successful_erasure_request()
	{
		var requestId = Guid.NewGuid();
		var erasureStore = A.Fake<IErasureStore>();
		var erasureService = A.Fake<IErasureService>();
		var queryStore = A.Fake<IErasureQueryStore>();
		var executionObserved = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

		A.CallTo(() => erasureStore.GetService(typeof(IErasureQueryStore)))
			.Returns(queryStore);

		var scheduledRequest = CreateErasureStatus(requestId);
		A.CallTo(() => queryStore.GetScheduledRequestsAsync(A<int>._, A<CancellationToken>._))
			.ReturnsNextFromSequence(
				Task.FromResult<IReadOnlyList<ErasureStatus>>([scheduledRequest]),
				Task.FromResult<IReadOnlyList<ErasureStatus>>([]));

		A.CallTo(() => erasureService.ExecuteAsync(requestId, A<CancellationToken>._))
			.Invokes(() => executionObserved.TrySetResult(true))
			.Returns(ErasureExecutionResult.Succeeded(3, 42));

		var (scopeFactory, _) = SetupScopeFactory(erasureStore, erasureService);

		var options = new ErasureSchedulerOptions
		{
			Enabled = true,
			PollingInterval = TimeSpan.FromMilliseconds(50),
			BatchSize = 10
		};

		var sut = new ErasureSchedulerBackgroundService(
			scopeFactory,
			Microsoft.Extensions.Options.Options.Create(options),
			NullLogger<ErasureSchedulerBackgroundService>.Instance);

		await sut.StartAsync(CancellationToken.None).ConfigureAwait(false);
		await executionObserved.Task.WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
		await sut.StopAsync(CancellationToken.None).ConfigureAwait(false);

		A.CallTo(() => erasureService.ExecuteAsync(requestId, A<CancellationToken>._))
			.MustHaveHappened();
	}

	[Fact]
	public async Task Handle_failed_erasure_execution()
	{
		var requestId = Guid.NewGuid();
		var erasureStore = A.Fake<IErasureStore>();
		var erasureService = A.Fake<IErasureService>();
		var queryStore = A.Fake<IErasureQueryStore>();
		var failedStatusUpdated = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

		A.CallTo(() => erasureStore.GetService(typeof(IErasureQueryStore)))
			.Returns(queryStore);

		A.CallTo(() => erasureStore.UpdateStatusAsync(
				requestId, ErasureRequestStatus.Failed, A<string>._, A<CancellationToken>._))
			.Invokes(() => failedStatusUpdated.TrySetResult(true))
			.Returns(true);

		var scheduledRequest = CreateErasureStatus(requestId);
		A.CallTo(() => queryStore.GetScheduledRequestsAsync(A<int>._, A<CancellationToken>._))
			.ReturnsNextFromSequence(
				Task.FromResult<IReadOnlyList<ErasureStatus>>([scheduledRequest]),
				Task.FromResult<IReadOnlyList<ErasureStatus>>([]));

		A.CallTo(() => erasureService.ExecuteAsync(requestId, A<CancellationToken>._))
			.Returns(ErasureExecutionResult.Failed("Key not found"));

		var (scopeFactory, _) = SetupScopeFactory(erasureStore, erasureService);

		var options = new ErasureSchedulerOptions
		{
			Enabled = true,
			PollingInterval = TimeSpan.FromMilliseconds(50)
		};

		var sut = new ErasureSchedulerBackgroundService(
			scopeFactory,
			Microsoft.Extensions.Options.Options.Create(options),
			NullLogger<ErasureSchedulerBackgroundService>.Instance);

		using var cts = new CancellationTokenSource();
		await sut.StartAsync(cts.Token).ConfigureAwait(false);
		await failedStatusUpdated.Task.WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
		await cts.CancelAsync().ConfigureAwait(false);
		await sut.StopAsync(CancellationToken.None).ConfigureAwait(false);

		A.CallTo(() => erasureStore.UpdateStatusAsync(
				requestId, ErasureRequestStatus.Failed, A<string>._, A<CancellationToken>._))
			.MustHaveHappened();
	}

	[Fact]
	public async Task Handle_erasure_execution_exception()
	{
		var requestId = Guid.NewGuid();
		var erasureStore = A.Fake<IErasureStore>();
		var erasureService = A.Fake<IErasureService>();
		var queryStore = A.Fake<IErasureQueryStore>();
		var failedStatusUpdated = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

		A.CallTo(() => erasureStore.GetService(typeof(IErasureQueryStore)))
			.Returns(queryStore);

		A.CallTo(() => erasureStore.UpdateStatusAsync(
				requestId, ErasureRequestStatus.Failed, A<string>._, A<CancellationToken>._))
			.Invokes(() => failedStatusUpdated.TrySetResult(true))
			.Returns(true);

		var scheduledRequest = CreateErasureStatus(requestId);
		A.CallTo(() => queryStore.GetScheduledRequestsAsync(A<int>._, A<CancellationToken>._))
			.ReturnsNextFromSequence(
				Task.FromResult<IReadOnlyList<ErasureStatus>>([scheduledRequest]),
				Task.FromResult<IReadOnlyList<ErasureStatus>>([]));

		A.CallTo(() => erasureService.ExecuteAsync(requestId, A<CancellationToken>._))
			.ThrowsAsync(new InvalidOperationException("KMS unavailable"));

		var (scopeFactory, _) = SetupScopeFactory(erasureStore, erasureService);

		var options = new ErasureSchedulerOptions
		{
			Enabled = true,
			PollingInterval = TimeSpan.FromMilliseconds(50)
		};

		var sut = new ErasureSchedulerBackgroundService(
			scopeFactory,
			Microsoft.Extensions.Options.Options.Create(options),
			NullLogger<ErasureSchedulerBackgroundService>.Instance);

		using var cts = new CancellationTokenSource();
		await sut.StartAsync(cts.Token).ConfigureAwait(false);
		await failedStatusUpdated.Task.WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
		await cts.CancelAsync().ConfigureAwait(false);
		await sut.StopAsync(CancellationToken.None).ConfigureAwait(false);

		// Should mark as failed after exception
		A.CallTo(() => erasureStore.UpdateStatusAsync(
				requestId, ErasureRequestStatus.Failed, A<string>._, A<CancellationToken>._))
			.MustHaveHappened();
	}

	[Fact]
	public async Task Continue_after_processing_error()
	{
		var erasureStore = A.Fake<IErasureStore>();
		var erasureService = A.Fake<IErasureService>();
		var queryStore = A.Fake<IErasureQueryStore>();
		var secondQueryObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

		A.CallTo(() => erasureStore.GetService(typeof(IErasureQueryStore)))
			.Returns(queryStore);

		var callCount = 0;
		A.CallTo(() => queryStore.GetScheduledRequestsAsync(A<int>._, A<CancellationToken>._))
			.ReturnsLazily(() =>
			{
				var count = Interlocked.Increment(ref callCount);
				if (count == 1)
				{
					throw new InvalidOperationException("Database offline");
				}

				if (count >= 2)
				{
					_ = secondQueryObserved.TrySetResult();
				}

				return Task.FromResult<IReadOnlyList<ErasureStatus>>([]);
			});

		var (scopeFactory, _) = SetupScopeFactory(erasureStore, erasureService);

		var options = new ErasureSchedulerOptions
		{
			Enabled = true,
			PollingInterval = TimeSpan.FromMilliseconds(50)
		};

		var sut = new ErasureSchedulerBackgroundService(
			scopeFactory,
			Microsoft.Extensions.Options.Options.Create(options),
			NullLogger<ErasureSchedulerBackgroundService>.Instance);

		using var cts = new CancellationTokenSource();
		await sut.StartAsync(cts.Token).ConfigureAwait(false);
		await secondQueryObserved.Task.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken.None).ConfigureAwait(false);
		await sut.StopAsync(CancellationToken.None).ConfigureAwait(false);

		// Should have attempted more than once (continues after error)
		A.CallTo(() => queryStore.GetScheduledRequestsAsync(A<int>._, A<CancellationToken>._))
			.MustHaveHappenedTwiceOrMore();
	}

	[Fact]
	public async Task Handle_query_store_not_supported()
	{
		var erasureStore = A.Fake<IErasureStore>();
		var erasureService = A.Fake<IErasureService>();
		var queryStoreLookupObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

		A.CallTo(() => erasureStore.GetService(typeof(IErasureQueryStore)))
			.Invokes(() => { _ = queryStoreLookupObserved.TrySetResult(); })
			.Returns(null);

		var (scopeFactory, _) = SetupScopeFactory(erasureStore, erasureService);

		var options = new ErasureSchedulerOptions
		{
			Enabled = true,
			PollingInterval = TimeSpan.FromMilliseconds(50)
		};

		var sut = new ErasureSchedulerBackgroundService(
			scopeFactory,
			Microsoft.Extensions.Options.Options.Create(options),
			NullLogger<ErasureSchedulerBackgroundService>.Instance);

		// The service catches exceptions in the processing loop, so it should still run
		using var cts = new CancellationTokenSource();
		await sut.StartAsync(cts.Token).ConfigureAwait(false);
		await queryStoreLookupObserved.Task.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken.None).ConfigureAwait(false);
		await sut.StopAsync(CancellationToken.None).ConfigureAwait(false);

		// Should have tried to get the query store
		A.CallTo(() => erasureStore.GetService(typeof(IErasureQueryStore)))
			.MustHaveHappened();
	}

	private static ErasureStatus CreateErasureStatus(Guid requestId) =>
		new()
		{
			RequestId = requestId,
			DataSubjectIdHash = "abc123hash",
			IdType = DataSubjectIdType.Email,
			Scope = ErasureScope.User,
			LegalBasis = ErasureLegalBasis.DataSubjectRequest,
			Status = ErasureRequestStatus.Scheduled,
			RequestedBy = "test@example.com",
			RequestedAt = DateTimeOffset.UtcNow.AddDays(-3),
			ScheduledExecutionAt = DateTimeOffset.UtcNow.AddMinutes(-10),
			UpdatedAt = DateTimeOffset.UtcNow
		};

	private static (IServiceScopeFactory, IServiceScope) SetupScopeFactory(
		IErasureStore erasureStore,
		IErasureService erasureService)
	{
		var serviceProvider = A.Fake<IServiceProvider>();
		A.CallTo(() => serviceProvider.GetService(typeof(IErasureStore)))
			.Returns(erasureStore);
		A.CallTo(() => serviceProvider.GetService(typeof(IErasureService)))
			.Returns(erasureService);

		// CreateAsyncScope() is an extension method that calls CreateScope() internally
		var scope = A.Fake<IServiceScope>();
		A.CallTo(() => scope.ServiceProvider).Returns(serviceProvider);

		var scopeFactory = A.Fake<IServiceScopeFactory>();
		A.CallTo(() => scopeFactory.CreateScope()).Returns(scope);

		return (scopeFactory, scope);
	}
}
