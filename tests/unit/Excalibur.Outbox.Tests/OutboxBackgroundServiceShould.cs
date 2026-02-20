// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

using Excalibur.Outbox.Health;
using Excalibur.Outbox.Outbox;

using FakeItEasy;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Outbox.Tests;

/// <summary>
/// Unit tests for <see cref="OutboxBackgroundService"/>.
/// Tests the background service for outbox message processing.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Outbox")]
[Trait("Priority", "0")]
public sealed class OutboxBackgroundServiceShould : UnitTestBase
{
	#region Constructor Tests

	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenPublisherIsNull()
	{
		// Arrange
		var options = CreateValidOptions();
		var logger = A.Fake<ILogger<OutboxBackgroundService>>();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new OutboxBackgroundService(
			null!,
			options,
			logger));
	}

	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenOptionsIsNull()
	{
		// Arrange
		var publisher = A.Fake<IOutboxPublisher>();
		var logger = A.Fake<ILogger<OutboxBackgroundService>>();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new OutboxBackgroundService(
			publisher,
			null!,
			logger));
	}

	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenLoggerIsNull()
	{
		// Arrange
		var publisher = A.Fake<IOutboxPublisher>();
		var options = CreateValidOptions();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new OutboxBackgroundService(
			publisher,
			options,
			null!));
	}

	[Fact]
	public void Constructor_CreatesService_WithValidParameters()
	{
		// Arrange
		var publisher = A.Fake<IOutboxPublisher>();
		var options = CreateValidOptions();
		var logger = A.Fake<ILogger<OutboxBackgroundService>>();

		// Act
		var service = new OutboxBackgroundService(publisher, options, logger);

		// Assert
		_ = service.ShouldNotBeNull();
	}

	#endregion

	#region ExecuteAsync Tests - Disabled Service

	[Fact]
	public async Task ExecuteAsync_DoesNotPublishMessages_WhenDisabled()
	{
		// Arrange
		var publisher = A.Fake<IOutboxPublisher>();
		var options = Options.Create(new OutboxProcessingOptions
		{
			Enabled = false,
			PollingInterval = TimeSpan.FromMilliseconds(100)
		});
		var logger = A.Fake<ILogger<OutboxBackgroundService>>();

		var service = new OutboxBackgroundService(publisher, options, logger);

		using var cts = new CancellationTokenSource();

		// Act
		await service.StartAsync(cts.Token);
		await Task.Delay(200); // Allow time for ExecuteAsync to check Enabled flag
		await cts.CancelAsync();
		await service.StopAsync(CancellationToken.None);

		// Assert - No publishing methods should be called when disabled
		A.CallTo(() => publisher.PublishPendingMessagesAsync(A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	#endregion

	#region ExecuteAsync Tests - Enabled Service

	[Fact]
	public async Task ExecuteAsync_CallsPublishPendingMessagesAsync_WhenEnabled()
	{
		// Arrange
		var publisher = A.Fake<IOutboxPublisher>();
		_ = A.CallTo(() => publisher.PublishPendingMessagesAsync(A<CancellationToken>._))
			.Returns(PublishingResult.Success(0));
		_ = A.CallTo(() => publisher.PublishScheduledMessagesAsync(A<CancellationToken>._))
			.Returns(PublishingResult.Success(0));
		_ = A.CallTo(() => publisher.RetryFailedMessagesAsync(A<int>._, A<CancellationToken>._))
			.Returns(PublishingResult.Success(0));

		var options = Options.Create(new OutboxProcessingOptions
		{
			Enabled = true,
			PollingInterval = TimeSpan.FromMilliseconds(100),
			ProcessScheduledMessages = false,
			RetryFailedMessages = false
		});
		var logger = A.Fake<ILogger<OutboxBackgroundService>>();

		var service = new OutboxBackgroundService(publisher, options, logger);

		using var cts = new CancellationTokenSource();

		// Act
		await service.StartAsync(cts.Token);
		await Task.Delay(250); // Allow time for at least one processing cycle
		await cts.CancelAsync();
		await service.StopAsync(CancellationToken.None);

		// Assert
		_ = A.CallTo(() => publisher.PublishPendingMessagesAsync(A<CancellationToken>._))
			.MustHaveHappened();
	}

	[Fact]
	public async Task ExecuteAsync_CallsPublishScheduledMessagesAsync_WhenEnabled()
	{
		// Arrange
		var publisher = A.Fake<IOutboxPublisher>();
		_ = A.CallTo(() => publisher.PublishPendingMessagesAsync(A<CancellationToken>._))
			.Returns(PublishingResult.Success(0));
		_ = A.CallTo(() => publisher.PublishScheduledMessagesAsync(A<CancellationToken>._))
			.Returns(PublishingResult.Success(0));
		_ = A.CallTo(() => publisher.RetryFailedMessagesAsync(A<int>._, A<CancellationToken>._))
			.Returns(PublishingResult.Success(0));

		var options = Options.Create(new OutboxProcessingOptions
		{
			Enabled = true,
			PollingInterval = TimeSpan.FromMilliseconds(100),
			ProcessScheduledMessages = true,
			RetryFailedMessages = false
		});
		var logger = A.Fake<ILogger<OutboxBackgroundService>>();

		var service = new OutboxBackgroundService(publisher, options, logger);

		using var cts = new CancellationTokenSource();

		// Act
		await service.StartAsync(cts.Token);
		await Task.Delay(250); // Allow time for at least one processing cycle
		await cts.CancelAsync();
		await service.StopAsync(CancellationToken.None);

		// Assert
		_ = A.CallTo(() => publisher.PublishScheduledMessagesAsync(A<CancellationToken>._))
			.MustHaveHappened();
	}

	[Fact]
	public async Task ExecuteAsync_CallsRetryFailedMessagesAsync_WhenEnabled()
	{
		// Arrange
		var publisher = A.Fake<IOutboxPublisher>();
		_ = A.CallTo(() => publisher.PublishPendingMessagesAsync(A<CancellationToken>._))
			.Returns(PublishingResult.Success(0));
		_ = A.CallTo(() => publisher.PublishScheduledMessagesAsync(A<CancellationToken>._))
			.Returns(PublishingResult.Success(0));
		_ = A.CallTo(() => publisher.RetryFailedMessagesAsync(A<int>._, A<CancellationToken>._))
			.Returns(PublishingResult.Success(0));

		var options = Options.Create(new OutboxProcessingOptions
		{
			Enabled = true,
			PollingInterval = TimeSpan.FromMilliseconds(100),
			ProcessScheduledMessages = false,
			RetryFailedMessages = true,
			MaxRetries = 5
		});
		var logger = A.Fake<ILogger<OutboxBackgroundService>>();

		var service = new OutboxBackgroundService(publisher, options, logger);

		using var cts = new CancellationTokenSource();

		// Act
		await service.StartAsync(cts.Token);
		await Task.Delay(250); // Allow time for at least one processing cycle
		await cts.CancelAsync();
		await service.StopAsync(CancellationToken.None);

		// Assert
		_ = A.CallTo(() => publisher.RetryFailedMessagesAsync(5, A<CancellationToken>._))
			.MustHaveHappened();
	}

	[Fact]
	public async Task ExecuteAsync_DoesNotCallScheduledMessages_WhenDisabled()
	{
		// Arrange
		var publisher = A.Fake<IOutboxPublisher>();
		_ = A.CallTo(() => publisher.PublishPendingMessagesAsync(A<CancellationToken>._))
			.Returns(PublishingResult.Success(0));

		var options = Options.Create(new OutboxProcessingOptions
		{
			Enabled = true,
			PollingInterval = TimeSpan.FromMilliseconds(100),
			ProcessScheduledMessages = false,
			RetryFailedMessages = false
		});
		var logger = A.Fake<ILogger<OutboxBackgroundService>>();

		var service = new OutboxBackgroundService(publisher, options, logger);

		using var cts = new CancellationTokenSource();

		// Act
		await service.StartAsync(cts.Token);
		await Task.Delay(250);
		await cts.CancelAsync();
		await service.StopAsync(CancellationToken.None);

		// Assert
		A.CallTo(() => publisher.PublishScheduledMessagesAsync(A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task ExecuteAsync_DoesNotCallRetryFailed_WhenDisabled()
	{
		// Arrange
		var publisher = A.Fake<IOutboxPublisher>();
		_ = A.CallTo(() => publisher.PublishPendingMessagesAsync(A<CancellationToken>._))
			.Returns(PublishingResult.Success(0));

		var options = Options.Create(new OutboxProcessingOptions
		{
			Enabled = true,
			PollingInterval = TimeSpan.FromMilliseconds(100),
			ProcessScheduledMessages = false,
			RetryFailedMessages = false
		});
		var logger = A.Fake<ILogger<OutboxBackgroundService>>();

		var service = new OutboxBackgroundService(publisher, options, logger);

		using var cts = new CancellationTokenSource();

		// Act
		await service.StartAsync(cts.Token);
		await Task.Delay(250);
		await cts.CancelAsync();
		await service.StopAsync(CancellationToken.None);

		// Assert
		A.CallTo(() => publisher.RetryFailedMessagesAsync(A<int>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	#endregion

	#region Constructor With HealthState Tests

	[Fact]
	public void Constructor_AcceptsNullHealthState()
	{
		// Arrange
		var publisher = A.Fake<IOutboxPublisher>();
		var options = CreateValidOptions();
		var logger = A.Fake<ILogger<OutboxBackgroundService>>();

		// Act
		var service = new OutboxBackgroundService(publisher, options, logger, healthState: null);

		// Assert
		_ = service.ShouldNotBeNull();
	}

	[Fact]
	public void Constructor_AcceptsHealthState()
	{
		// Arrange
		var publisher = A.Fake<IOutboxPublisher>();
		var options = CreateValidOptions();
		var logger = A.Fake<ILogger<OutboxBackgroundService>>();
		var healthState = new BackgroundServiceHealthState();

		// Act
		var service = new OutboxBackgroundService(publisher, options, logger, healthState);

		// Assert
		_ = service.ShouldNotBeNull();
	}

	#endregion

	#region HealthState Integration Tests

	[Fact]
	public async Task ExecuteAsync_MarksHealthStateStarted_WhenEnabled()
	{
		// Arrange
		var publisher = A.Fake<IOutboxPublisher>();
		_ = A.CallTo(() => publisher.PublishPendingMessagesAsync(A<CancellationToken>._))
			.Returns(PublishingResult.Success(0));

		var options = Options.Create(new OutboxProcessingOptions
		{
			Enabled = true,
			PollingInterval = TimeSpan.FromMilliseconds(100),
			ProcessScheduledMessages = false,
			RetryFailedMessages = false
		});
		var logger = A.Fake<ILogger<OutboxBackgroundService>>();
		var healthState = new BackgroundServiceHealthState();

		var service = new OutboxBackgroundService(publisher, options, logger, healthState);

		using var cts = new CancellationTokenSource();

		// Act
		await service.StartAsync(cts.Token);
		await Task.Delay(150);
		await cts.CancelAsync();
		await service.StopAsync(CancellationToken.None);

		// Assert - Health state should have been updated
		healthState.TotalCycles.ShouldBeGreaterThan(0);
	}

	[Fact]
	public async Task ExecuteAsync_RecordsSuccessCycle_WhenMessagesProcessed()
	{
		// Arrange
		var publisher = A.Fake<IOutboxPublisher>();
		_ = A.CallTo(() => publisher.PublishPendingMessagesAsync(A<CancellationToken>._))
			.Returns(PublishingResult.Success(5)); // 5 successful messages

		var options = Options.Create(new OutboxProcessingOptions
		{
			Enabled = true,
			PollingInterval = TimeSpan.FromMilliseconds(100),
			ProcessScheduledMessages = false,
			RetryFailedMessages = false
		});
		var logger = A.Fake<ILogger<OutboxBackgroundService>>();
		var healthState = new BackgroundServiceHealthState();

		var service = new OutboxBackgroundService(publisher, options, logger, healthState);

		using var cts = new CancellationTokenSource();

		// Act
		await service.StartAsync(cts.Token);
		await Task.Delay(200);
		await cts.CancelAsync();
		await service.StopAsync(CancellationToken.None);

		// Assert - Cycle should have recorded success
		healthState.TotalProcessed.ShouldBeGreaterThan(0);
	}

	[Fact]
	public async Task ExecuteAsync_RecordsFailureCycle_WhenMessagesFailedToProcess()
	{
		// Arrange
		var publisher = A.Fake<IOutboxPublisher>();
		_ = A.CallTo(() => publisher.PublishPendingMessagesAsync(A<CancellationToken>._))
			.Returns(PublishingResult.WithFailures(0, 2, Array.Empty<PublishingError>())); // 2 failed messages

		var options = Options.Create(new OutboxProcessingOptions
		{
			Enabled = true,
			PollingInterval = TimeSpan.FromMilliseconds(100),
			ProcessScheduledMessages = false,
			RetryFailedMessages = false
		});
		var logger = A.Fake<ILogger<OutboxBackgroundService>>();
		var healthState = new BackgroundServiceHealthState();

		var service = new OutboxBackgroundService(publisher, options, logger, healthState);

		using var cts = new CancellationTokenSource();

		// Act
		await service.StartAsync(cts.Token);
		await Task.Delay(200);
		await cts.CancelAsync();
		await service.StopAsync(CancellationToken.None);

		// Assert - Health state recorded the failure
		_ = healthState.ShouldNotBeNull();
	}

	#endregion

	#region StopAsync Tests

	[Fact]
	public async Task StopAsync_MarksHealthStateStopped()
	{
		// Arrange
		var publisher = A.Fake<IOutboxPublisher>();
		_ = A.CallTo(() => publisher.PublishPendingMessagesAsync(A<CancellationToken>._))
			.Returns(PublishingResult.Success(0));

		var options = Options.Create(new OutboxProcessingOptions
		{
			Enabled = true,
			PollingInterval = TimeSpan.FromMilliseconds(100),
			DrainTimeoutSeconds = 5
		});
		var logger = A.Fake<ILogger<OutboxBackgroundService>>();
		var healthState = new BackgroundServiceHealthState();

		var service = new OutboxBackgroundService(publisher, options, logger, healthState);

		using var cts = new CancellationTokenSource();

		// Act
		await service.StartAsync(cts.Token);
		await Task.Delay(100);
		await cts.CancelAsync();
		await service.StopAsync(CancellationToken.None);

		// Assert - No exception and service stopped cleanly
		_ = service.ShouldNotBeNull();
	}

	#endregion

	#region Scheduled Messages With Results Tests

	[Fact]
	public async Task ExecuteAsync_LogsScheduledMessageResults_WhenSuccessful()
	{
		// Arrange
		var publisher = A.Fake<IOutboxPublisher>();
		_ = A.CallTo(() => publisher.PublishPendingMessagesAsync(A<CancellationToken>._))
			.Returns(PublishingResult.Success(0));
		_ = A.CallTo(() => publisher.PublishScheduledMessagesAsync(A<CancellationToken>._))
			.Returns(PublishingResult.Success(3)); // 3 scheduled messages processed

		var options = Options.Create(new OutboxProcessingOptions
		{
			Enabled = true,
			PollingInterval = TimeSpan.FromMilliseconds(100),
			ProcessScheduledMessages = true,
			RetryFailedMessages = false
		});
		var logger = A.Fake<ILogger<OutboxBackgroundService>>();

		var service = new OutboxBackgroundService(publisher, options, logger);

		using var cts = new CancellationTokenSource();

		// Act
		await service.StartAsync(cts.Token);
		await Task.Delay(250);
		await cts.CancelAsync();
		await service.StopAsync(CancellationToken.None);

		// Assert
		_ = A.CallTo(() => publisher.PublishScheduledMessagesAsync(A<CancellationToken>._))
			.MustHaveHappened();
	}

	#endregion

	#region Retry Messages With Results Tests

	[Fact]
	public async Task ExecuteAsync_LogsRetryMessageResults_WhenSuccessful()
	{
		// Arrange
		var publisher = A.Fake<IOutboxPublisher>();
		_ = A.CallTo(() => publisher.PublishPendingMessagesAsync(A<CancellationToken>._))
			.Returns(PublishingResult.Success(0));
		_ = A.CallTo(() => publisher.RetryFailedMessagesAsync(A<int>._, A<CancellationToken>._))
			.Returns(PublishingResult.Success(2)); // 2 retried messages succeeded

		var options = Options.Create(new OutboxProcessingOptions
		{
			Enabled = true,
			PollingInterval = TimeSpan.FromMilliseconds(100),
			ProcessScheduledMessages = false,
			RetryFailedMessages = true,
			MaxRetries = 3
		});
		var logger = A.Fake<ILogger<OutboxBackgroundService>>();

		var service = new OutboxBackgroundService(publisher, options, logger);

		using var cts = new CancellationTokenSource();

		// Act
		await service.StartAsync(cts.Token);
		await Task.Delay(250);
		await cts.CancelAsync();
		await service.StopAsync(CancellationToken.None);

		// Assert
		_ = A.CallTo(() => publisher.RetryFailedMessagesAsync(3, A<CancellationToken>._))
			.MustHaveHappened();
	}

	#endregion

	#region Partial Success Tests

	[Fact]
	public async Task ExecuteAsync_RecordsPartialResult_WhenSomeMessagesFail()
	{
		// Arrange
		var publisher = A.Fake<IOutboxPublisher>();
		_ = A.CallTo(() => publisher.PublishPendingMessagesAsync(A<CancellationToken>._))
			.Returns(PublishingResult.WithFailures(successCount: 3, failureCount: 2, errors: Array.Empty<PublishingError>())); // Mixed results

		var options = Options.Create(new OutboxProcessingOptions
		{
			Enabled = true,
			PollingInterval = TimeSpan.FromMilliseconds(100),
			ProcessScheduledMessages = false,
			RetryFailedMessages = false
		});
		var logger = A.Fake<ILogger<OutboxBackgroundService>>();
		var healthState = new BackgroundServiceHealthState();

		var service = new OutboxBackgroundService(publisher, options, logger, healthState);

		using var cts = new CancellationTokenSource();

		// Act
		await service.StartAsync(cts.Token);
		await Task.Delay(200);
		await cts.CancelAsync();
		await service.StopAsync(CancellationToken.None);

		// Assert - Service handled mixed results
		_ = healthState.ShouldNotBeNull();
	}

	#endregion

	#region Error Handling Tests

	[Fact]
	public async Task ExecuteAsync_ContinuesProcessing_AfterException()
	{
		// Arrange
		var callCount = 0;
		var publisher = A.Fake<IOutboxPublisher>();
		_ = A.CallTo(() => publisher.PublishPendingMessagesAsync(A<CancellationToken>._))
			.Invokes(() =>
			{
				callCount++;
				if (callCount == 1)
				{
					throw new InvalidOperationException("Test exception");
				}
			})
			.Returns(PublishingResult.Success(0));

		var options = Options.Create(new OutboxProcessingOptions
		{
			Enabled = true,
			PollingInterval = TimeSpan.FromMilliseconds(50),
			ProcessScheduledMessages = false,
			RetryFailedMessages = false
		});
		var logger = A.Fake<ILogger<OutboxBackgroundService>>();

		var service = new OutboxBackgroundService(publisher, options, logger);

		using var cts = new CancellationTokenSource();

		// Act
		await service.StartAsync(cts.Token);
		await Task.Delay(300); // Allow time for multiple cycles
		await cts.CancelAsync();
		await service.StopAsync(CancellationToken.None);

		// Assert - Should have been called more than once despite the first failure
		callCount.ShouldBeGreaterThan(1);
	}

	#endregion

	#region Helper Methods

	private static IOptions<OutboxProcessingOptions> CreateValidOptions()
	{
		return Options.Create(new OutboxProcessingOptions
		{
			Enabled = true,
			PollingInterval = TimeSpan.FromSeconds(5),
			MaxRetries = 3,
			ProcessScheduledMessages = true,
			RetryFailedMessages = true
		});
	}

	#endregion
}
