using Excalibur.Dispatch.Compliance;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Compliance.Tests.Retention;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class RetentionEnforcementBackgroundServiceShould
{
	[Fact]
	public void Throw_for_null_scope_factory()
	{
		Should.Throw<ArgumentNullException>(() =>
			new RetentionEnforcementBackgroundService(
				null!,
				Microsoft.Extensions.Options.Options.Create(new RetentionEnforcementOptions()),
				NullLogger<RetentionEnforcementBackgroundService>.Instance));
	}

	[Fact]
	public void Throw_for_null_options()
	{
		var scopeFactory = A.Fake<IServiceScopeFactory>();

		Should.Throw<ArgumentNullException>(() =>
			new RetentionEnforcementBackgroundService(
				scopeFactory,
				null!,
				NullLogger<RetentionEnforcementBackgroundService>.Instance));
	}

	[Fact]
	public void Throw_for_null_logger()
	{
		var scopeFactory = A.Fake<IServiceScopeFactory>();

		Should.Throw<ArgumentNullException>(() =>
			new RetentionEnforcementBackgroundService(
				scopeFactory,
				Microsoft.Extensions.Options.Options.Create(new RetentionEnforcementOptions()),
				null!));
	}

	[Fact]
	public async Task Exit_immediately_when_disabled()
	{
		var scopeFactory = A.Fake<IServiceScopeFactory>();
		var options = new RetentionEnforcementOptions { Enabled = false };
		var sut = new RetentionEnforcementBackgroundService(
			scopeFactory,
			Microsoft.Extensions.Options.Options.Create(options),
			NullLogger<RetentionEnforcementBackgroundService>.Instance);

		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
		await sut.StartAsync(cts.Token).ConfigureAwait(false);
		await sut.StopAsync(CancellationToken.None).ConfigureAwait(false);

		A.CallTo(() => scopeFactory.CreateScope()).MustNotHaveHappened();
	}

	[Fact]
	public async Task Run_enforcement_cycle()
	{
		var enforcementService = A.Fake<IRetentionEnforcementService>();
		A.CallTo(() => enforcementService.EnforceRetentionAsync(A<CancellationToken>._))
			.Returns(new RetentionEnforcementResult
			{
				PoliciesEvaluated = 5,
				RecordsCleaned = 0,
				IsDryRun = false,
				CompletedAt = DateTimeOffset.UtcNow
			});

		var (scopeFactory, _) = SetupScopeFactory(enforcementService);

		var options = new RetentionEnforcementOptions
		{
			Enabled = true,
			ScanInterval = TimeSpan.FromMilliseconds(50)
		};

		var sut = new RetentionEnforcementBackgroundService(
			scopeFactory,
			Microsoft.Extensions.Options.Options.Create(options),
			NullLogger<RetentionEnforcementBackgroundService>.Instance);

		using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
		await sut.StartAsync(cts.Token).ConfigureAwait(false);
		await Task.Delay(TimeSpan.FromMilliseconds(300), CancellationToken.None).ConfigureAwait(false);
		await sut.StopAsync(CancellationToken.None).ConfigureAwait(false);

		A.CallTo(() => enforcementService.EnforceRetentionAsync(A<CancellationToken>._))
			.MustHaveHappened();
	}

	[Fact]
	public async Task Continue_after_enforcement_error()
	{
		var enforcementService = A.Fake<IRetentionEnforcementService>();
		var callCount = 0;
		var secondCallObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		A.CallTo(() => enforcementService.EnforceRetentionAsync(A<CancellationToken>._))
			.ReturnsLazily(() =>
			{
				var observedCount = Interlocked.Increment(ref callCount);
				if (observedCount == 1)
				{
					throw new InvalidOperationException("First cycle fails");
				}
				if (observedCount >= 2)
				{
					_ = secondCallObserved.TrySetResult();
				}

				return Task.FromResult(new RetentionEnforcementResult
				{
					PoliciesEvaluated = 1,
					RecordsCleaned = 0,
					IsDryRun = false,
					CompletedAt = DateTimeOffset.UtcNow
				});
			});

		var (scopeFactory, _) = SetupScopeFactory(enforcementService);

		var options = new RetentionEnforcementOptions
		{
			Enabled = true,
			ScanInterval = TimeSpan.FromMilliseconds(50)
		};

		var sut = new RetentionEnforcementBackgroundService(
			scopeFactory,
			Microsoft.Extensions.Options.Options.Create(options),
			NullLogger<RetentionEnforcementBackgroundService>.Instance);

		using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));
		await sut.StartAsync(cts.Token).ConfigureAwait(false);
		await secondCallObserved.Task.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken.None).ConfigureAwait(false);
		await sut.StopAsync(CancellationToken.None).ConfigureAwait(false);

		// Should have been called more than once (survived the error)
		callCount.ShouldBeGreaterThanOrEqualTo(2);
	}

	private static (IServiceScopeFactory, IServiceScope) SetupScopeFactory(IRetentionEnforcementService enforcementService)
	{
		var serviceProvider = A.Fake<IServiceProvider>();
		A.CallTo(() => serviceProvider.GetService(typeof(IRetentionEnforcementService)))
			.Returns(enforcementService);

		var scope = A.Fake<IServiceScope>();
		A.CallTo(() => scope.ServiceProvider).Returns(serviceProvider);

		var scopeFactory = A.Fake<IServiceScopeFactory>();
		A.CallTo(() => scopeFactory.CreateScope()).Returns(scope);

		return (scopeFactory, scope);
	}
}
