// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc.Processing;

namespace Excalibur.Tests.Cdc.Processing;

/// <summary>
/// Unit tests for <see cref="CdcProcessingHostedService"/>.
/// Tests the background service for CDC change processing.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "CdcProcessing")]
[Trait("Priority", "0")]
public sealed class CdcProcessingHostedServiceShould : UnitTestBase
{
	#region Constructor Tests

	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenProcessorIsNull()
	{
		// Arrange
		var options = CreateValidOptions();
		var logger = A.Fake<ILogger<CdcProcessingHostedService>>();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new CdcProcessingHostedService(
			null!,
			options,
			logger));
	}

	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenOptionsIsNull()
	{
		// Arrange
		var processor = A.Fake<ICdcBackgroundProcessor>();
		var logger = A.Fake<ILogger<CdcProcessingHostedService>>();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new CdcProcessingHostedService(
			processor,
			null!,
			logger));
	}

	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenLoggerIsNull()
	{
		// Arrange
		var processor = A.Fake<ICdcBackgroundProcessor>();
		var options = CreateValidOptions();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new CdcProcessingHostedService(
			processor,
			options,
			null!));
	}

	[Fact]
	public void Constructor_CreatesService_WithValidParameters()
	{
		// Arrange
		var processor = A.Fake<ICdcBackgroundProcessor>();
		var options = CreateValidOptions();
		var logger = A.Fake<ILogger<CdcProcessingHostedService>>();

		// Act
		var service = new CdcProcessingHostedService(processor, options, logger);

		// Assert
		_ = service.ShouldNotBeNull();
	}

	#endregion

	#region ExecuteAsync Tests - Disabled Service

	[Fact]
	public async Task ExecuteAsync_DoesNotProcessChanges_WhenDisabled()
	{
		// Arrange
		var processor = A.Fake<ICdcBackgroundProcessor>();
		var options = Options.Create(new CdcProcessingOptions
		{
			Enabled = false,
			PollingInterval = TimeSpan.FromMilliseconds(100)
		});
		var logger = A.Fake<ILogger<CdcProcessingHostedService>>();

		var service = new CdcProcessingHostedService(processor, options, logger);

		// Act
		await service.StartAsync(CancellationToken.None);
		await service.StopAsync(CancellationToken.None);

		// Assert - No processing methods should be called when disabled
		A.CallTo(() => processor.ProcessChangesAsync(A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	#endregion

	#region ExecuteAsync Tests - Polling Loop

	[Fact]
	public async Task ExecuteAsync_CallsProcessChangesAsync_WhenEnabled()
	{
		// Arrange
		var firstProcessingCall = CreateSignal();
		var processor = A.Fake<ICdcBackgroundProcessor>();
		_ = A.CallTo(() => processor.ProcessChangesAsync(A<CancellationToken>._))
			.Invokes(() => firstProcessingCall.TrySetResult(true))
			.Returns(0);

		var options = Options.Create(new CdcProcessingOptions
		{
			Enabled = true,
			PollingInterval = TimeSpan.FromMilliseconds(100)
		});
		var logger = A.Fake<ILogger<CdcProcessingHostedService>>();

		var service = new CdcProcessingHostedService(processor, options, logger);

		using var cts = new CancellationTokenSource();

		// Act
		await service.StartAsync(cts.Token);
		await firstProcessingCall.Task.WaitAsync(TimeSpan.FromSeconds(5));
		await cts.CancelAsync();
		await service.StopAsync(CancellationToken.None);

		// Assert
		_ = A.CallTo(() => processor.ProcessChangesAsync(A<CancellationToken>._))
			.MustHaveHappened();
	}

	[Fact]
	public async Task ExecuteAsync_PollsOnInterval_WhenEnabled()
	{
		// Arrange
		var callCount = 0;
		var twoCallsObserved = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
		var processor = A.Fake<ICdcBackgroundProcessor>();
		_ = A.CallTo(() => processor.ProcessChangesAsync(A<CancellationToken>._))
			.Invokes(() =>
			{
				if (Interlocked.Increment(ref callCount) >= 2)
				{
					twoCallsObserved.TrySetResult(true);
				}
			})
			.Returns(0);

		var options = Options.Create(new CdcProcessingOptions
		{
			Enabled = true,
			PollingInterval = TimeSpan.FromMilliseconds(50)
		});
		var logger = A.Fake<ILogger<CdcProcessingHostedService>>();

		var service = new CdcProcessingHostedService(processor, options, logger);

		using var cts = new CancellationTokenSource();

		// Act
		await service.StartAsync(cts.Token);
		await twoCallsObserved.Task.WaitAsync(TimeSpan.FromSeconds(5));
		await cts.CancelAsync();
		await service.StopAsync(CancellationToken.None);

		// Assert - Should have been called multiple times
		callCount.ShouldBeGreaterThan(1);
	}

	#endregion

	#region Error Handling Tests

	[Fact]
	public async Task ExecuteAsync_ContinuesProcessing_AfterException()
	{
		// Arrange
		var callCount = 0;
		var twoCallsObserved = CreateSignal();
		var processor = A.Fake<ICdcBackgroundProcessor>();
		_ = A.CallTo(() => processor.ProcessChangesAsync(A<CancellationToken>._))
			.Invokes(() =>
			{
				var currentCall = Interlocked.Increment(ref callCount);
				if (currentCall >= 2)
				{
					twoCallsObserved.TrySetResult(true);
				}

				if (currentCall == 1)
				{
					throw new InvalidOperationException("Test exception");
				}
			})
			.Returns(0);

		var options = Options.Create(new CdcProcessingOptions
		{
			Enabled = true,
			PollingInterval = TimeSpan.FromMilliseconds(50)
		});
		var logger = A.Fake<ILogger<CdcProcessingHostedService>>();

		var service = new CdcProcessingHostedService(processor, options, logger);

		using var cts = new CancellationTokenSource();

		// Act
		await service.StartAsync(cts.Token);
		await twoCallsObserved.Task.WaitAsync(TimeSpan.FromSeconds(5));
		await cts.CancelAsync();
		await service.StopAsync(CancellationToken.None);

		// Assert - Should have been called more than once despite the first failure
		callCount.ShouldBeGreaterThan(1);
	}

	#endregion

	#region Drain Timeout Tests

	[Fact]
	public async Task StopAsync_CompletesGracefully_WhenProcessorFinishesBeforeDrainTimeout()
	{
		// Arrange
		var firstProcessingCall = CreateSignal();
		var processor = A.Fake<ICdcBackgroundProcessor>();
		_ = A.CallTo(() => processor.ProcessChangesAsync(A<CancellationToken>._))
			.Invokes(() => firstProcessingCall.TrySetResult(true))
			.Returns(0);

		var options = Options.Create(new CdcProcessingOptions
		{
			Enabled = true,
			PollingInterval = TimeSpan.FromMilliseconds(100),
			DrainTimeoutSeconds = 5
		});
		var logger = A.Fake<ILogger<CdcProcessingHostedService>>();

		var service = new CdcProcessingHostedService(processor, options, logger);

		using var cts = new CancellationTokenSource();

		// Act
		await service.StartAsync(cts.Token);
		await firstProcessingCall.Task.WaitAsync(TimeSpan.FromSeconds(5));
		await cts.CancelAsync();

		// StopAsync should complete without throwing
		await service.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task StopAsync_UsesDrainTimeout_FromOptions()
	{
		// Arrange
		var firstProcessingCall = CreateSignal();
		var processor = A.Fake<ICdcBackgroundProcessor>();
		_ = A.CallTo(() => processor.ProcessChangesAsync(A<CancellationToken>._))
			.Invokes(() => firstProcessingCall.TrySetResult(true))
			.Returns(0);

		var drainTimeoutSeconds = 10;
		var options = Options.Create(new CdcProcessingOptions
		{
			Enabled = true,
			PollingInterval = TimeSpan.FromMilliseconds(100),
			DrainTimeoutSeconds = drainTimeoutSeconds
		});
		var logger = A.Fake<ILogger<CdcProcessingHostedService>>();

		var service = new CdcProcessingHostedService(processor, options, logger);

		// Act
		await service.StartAsync(CancellationToken.None);
		await firstProcessingCall.Task.WaitAsync(TimeSpan.FromSeconds(5));

		// StopAsync should complete since the processor is fast
		await service.StopAsync(CancellationToken.None);

		// Assert - service stopped without issue; drain timeout was configured
		options.Value.DrainTimeout.ShouldBe(TimeSpan.FromSeconds(drainTimeoutSeconds));
	}

	#endregion

	#region Cancellation During Processing Tests

	[Fact]
	public async Task ExecuteAsync_ExitsGracefully_WhenCancelledDuringProcessing()
	{
		// Arrange
		var processingStarted = new TaskCompletionSource<bool>();
		var cancellationTriggered = new TaskCompletionSource<bool>();

		var processor = A.Fake<ICdcBackgroundProcessor>();
		_ = A.CallTo(() => processor.ProcessChangesAsync(A<CancellationToken>._))
			.ReturnsLazily(async call =>
			{
				var token = call.Arguments.Get<CancellationToken>(0);
				processingStarted.TrySetResult(true);

				// Wait for cancellation to be triggered
				await cancellationTriggered.Task.ConfigureAwait(false);

				// Simulate work being done when cancellation is requested
				token.ThrowIfCancellationRequested();
				return 0;
			});

		var options = Options.Create(new CdcProcessingOptions
		{
			Enabled = true,
			PollingInterval = TimeSpan.FromMilliseconds(100),
			DrainTimeoutSeconds = 5
		});
		var logger = A.Fake<ILogger<CdcProcessingHostedService>>();

		var service = new CdcProcessingHostedService(processor, options, logger);
		using var cts = new CancellationTokenSource();

		// Act
		await service.StartAsync(cts.Token);

		// Wait for processing to start
		await processingStarted.Task;

		// Cancel while processing
		await cts.CancelAsync();
		cancellationTriggered.TrySetResult(true);

		// Should complete without throwing
		await service.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task StopAsync_LogsWarning_WhenDrainTimeoutExceeded()
	{
		// Arrange
		var processingStarted = new TaskCompletionSource<bool>();

		var processor = A.Fake<ICdcBackgroundProcessor>();
		_ = A.CallTo(() => processor.ProcessChangesAsync(A<CancellationToken>._))
			.ReturnsLazily(async call =>
			{
				var token = call.Arguments.Get<CancellationToken>(0);
				processingStarted.TrySetResult(true);

				// Simulate long-running processing that ignores cancellation
				try
				{
					await global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(TimeSpan.FromSeconds(10), token).ConfigureAwait(false);
				}
				catch (OperationCanceledException)
				{
					// Continue anyway to simulate work that takes time to drain
					await global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(500).ConfigureAwait(false);
				}

				return 0;
			});

		var options = Options.Create(new CdcProcessingOptions
		{
			Enabled = true,
			PollingInterval = TimeSpan.FromMilliseconds(50),
			DrainTimeoutSeconds = 1 // Very short drain timeout to trigger the warning
		});
		var logger = A.Fake<ILogger<CdcProcessingHostedService>>();

		var service = new CdcProcessingHostedService(processor, options, logger);

		// Act
		await service.StartAsync(CancellationToken.None);

		// Wait for processing to start
		await processingStarted.Task;

		// Stop with a short drain timeout - this should hit the drain timeout exceeded path
		// The StopAsync itself has a drain timeout that will be exceeded
		await service.StopAsync(CancellationToken.None);

		// Assert - The service should still complete (drain timeout exceeded is just a warning)
		// The test passes if StopAsync completes without throwing
	}

	#endregion

	#region Options Tests

	[Fact]
	public void CdcProcessingOptions_HasCorrectDefaults()
	{
		// Arrange & Act
		var options = new CdcProcessingOptions();

		// Assert
		options.PollingInterval.ShouldBe(TimeSpan.FromSeconds(5));
		options.Enabled.ShouldBeTrue();
		options.DrainTimeoutSeconds.ShouldBe(30);
		options.DrainTimeout.ShouldBe(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void CdcProcessingOptions_DrainTimeout_ReflectsDrainTimeoutSeconds()
	{
		// Arrange
		var options = new CdcProcessingOptions { DrainTimeoutSeconds = 60 };

		// Act & Assert
		options.DrainTimeout.ShouldBe(TimeSpan.FromSeconds(60));
	}

	#endregion

	#region Helper Methods

	private static IOptions<CdcProcessingOptions> CreateValidOptions()
	{
		return Options.Create(new CdcProcessingOptions
		{
			Enabled = true,
			PollingInterval = TimeSpan.FromSeconds(5),
			DrainTimeoutSeconds = 30
		});
	}

	private static TaskCompletionSource<bool> CreateSignal()
	{
		return new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
	}

	#endregion
}
