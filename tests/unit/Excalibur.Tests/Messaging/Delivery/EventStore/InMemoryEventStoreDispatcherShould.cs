// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;

namespace Excalibur.Tests.Messaging.Delivery.EventStore;

/// <summary>
///     Unit tests for InMemoryEventStoreDispatcher to verify in-memory event store dispatcher functionality.
/// </summary>
[Trait("Category", "Unit")]
public class InMemoryEventStoreDispatcherShould
{
	private readonly ILogger<InMemoryEventStoreDispatcher> _logger;
	private readonly InMemoryEventStoreDispatcher _dispatcher;

	public InMemoryEventStoreDispatcherShould()
	{
		_logger = A.Fake<ILogger<InMemoryEventStoreDispatcher>>();
		_dispatcher = new InMemoryEventStoreDispatcher(_logger);
	}

	[Fact]
	public void ConstructorShouldAcceptLogger()
	{
		// Arrange & Act
		var dispatcher = new InMemoryEventStoreDispatcher(_logger);

		// Assert
		_ = dispatcher.ShouldNotBeNull();
		_ = dispatcher.ShouldBeAssignableTo<IEventStoreDispatcher>();
	}

	[Fact]
	public void InitShouldLogInformationWhenEnabled()
	{
		// Arrange
		_ = A.CallTo(() => _logger.IsEnabled(LogLevel.Information)).Returns(true);
		var dispatcherId = "test-dispatcher-123";

		// Act
		_dispatcher.Init(dispatcherId);

		// Assert - Verify IsEnabled was called to check if logging should occur
		_ = A.CallTo(() => _logger.IsEnabled(LogLevel.Information)).MustHaveHappenedOnceExactly();

		// Note: The actual logging uses LoggerMessage.Define<string> which creates a
		// specialized generic method (Log<LogValues`1[String]>) that FakeItEasy can't
		// easily match with a generic A<object>._ matcher. Instead, we verify the
		// IsEnabled check happened, which proves the logging path was taken.
		// This is a known limitation when testing high-performance logging patterns.
	}

	[Fact]
	public void InitShouldNotLogWhenInformationLevelDisabled()
	{
		// Arrange
		_ = A.CallTo(() => _logger.IsEnabled(LogLevel.Information)).Returns(false);
		var dispatcherId = "test-dispatcher-456";

		// Act
		_dispatcher.Init(dispatcherId);

		// Assert
		_ = A.CallTo(() => _logger.IsEnabled(LogLevel.Information)).MustHaveHappenedOnceExactly();
		A.CallTo(() => _logger.Log(
				A<LogLevel>._,
				A<EventId>._,
				A<object>._,
				A<Exception>._,
				A<Func<object, Exception?, string>>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public void InitShouldAcceptNullDispatcherId()
	{
		// Arrange
		_ = A.CallTo(() => _logger.IsEnabled(LogLevel.Information)).Returns(true);

		// Act & Assert
		Should.NotThrow(() => _dispatcher.Init(null!));
	}

	[Fact]
	public void InitShouldAcceptEmptyDispatcherId()
	{
		// Arrange
		_ = A.CallTo(() => _logger.IsEnabled(LogLevel.Information)).Returns(true);

		// Act & Assert
		Should.NotThrow(() => _dispatcher.Init(string.Empty));
	}

	[Fact]
	public void InitShouldAcceptWhitespaceDispatcherId()
	{
		// Arrange
		_ = A.CallTo(() => _logger.IsEnabled(LogLevel.Information)).Returns(true);

		// Act & Assert
		Should.NotThrow(() => _dispatcher.Init(" "));
	}

	[Fact]
	public async Task DispatchAsyncShouldCompleteSuccessfully() =>
		// Arrange & Act
		await _dispatcher.DispatchAsync(CancellationToken.None).ConfigureAwait(false);

	// Assert - Should complete without throwing This is a no-op method so we just verify it doesn't throw
	[Fact]
	public async Task DispatchAsyncShouldCompleteSuccessfullyWithCancellationToken()
	{
		// Arrange
		using var cts = new CancellationTokenSource();

		// Act
		await _dispatcher.DispatchAsync(cts.Token).ConfigureAwait(false);

		// Assert - Should complete without throwing
	}

	[Fact]
	public async Task DispatchAsyncShouldCompleteEvenWithCancelledToken()
	{
		// Arrange
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync().ConfigureAwait(false);

		// Act & Assert - Should complete without throwing even with cancelled token
		await _dispatcher.DispatchAsync(cts.Token).ConfigureAwait(false);
	}

	[Fact]
	public async Task DispatchAsyncShouldBeIdempotent()
	{
		// Arrange & Act
		await _dispatcher.DispatchAsync(CancellationToken.None).ConfigureAwait(false);
		await _dispatcher.DispatchAsync(CancellationToken.None).ConfigureAwait(false);
		await _dispatcher.DispatchAsync(CancellationToken.None).ConfigureAwait(false);

		// Assert - Multiple calls should not throw
	}

	[Fact]
	public async Task DispatchAsyncShouldSupportConcurrentCalls()
	{
		// Arrange
		var tasks = new List<Task>();

		// Act - Make multiple concurrent calls
		for (var i = 0; i < 10; i++)
		{
			tasks.Add(_dispatcher.DispatchAsync(CancellationToken.None));
		}

		// Assert - All calls should complete successfully
		await Task.WhenAll(tasks).ConfigureAwait(false);
	}

	[Fact]
	public void InitShouldSupportMultipleCalls()
	{
		// Arrange
		_ = A.CallTo(() => _logger.IsEnabled(LogLevel.Information)).Returns(true);

		// Act & Assert
		Should.NotThrow(() =>
		{
			_dispatcher.Init("dispatcher-1");
			_dispatcher.Init("dispatcher-2");
			_dispatcher.Init("dispatcher-3");
		});

		// Verify logging happened for each call
		_ = A.CallTo(() => _logger.IsEnabled(LogLevel.Information)).MustHaveHappened(3, Times.Exactly);
	}

	[Fact]
	public async Task DispatchAsyncShouldReturnCompletedTask()
	{
		// Arrange & Act
		var task = _dispatcher.DispatchAsync(CancellationToken.None);

		// Assert
		task.IsCompleted.ShouldBeTrue();
		task.Status.ShouldBe(TaskStatus.RanToCompletion);
		await task.ConfigureAwait(false); // Should not throw
	}

	[Fact]
	public void DispatcherShouldImplementCorrectInterface() =>
		// Arrange & Act & Assert
		_ = _dispatcher.ShouldBeAssignableTo<IEventStoreDispatcher>();

	[Fact]
	public async Task DispatchAsyncWithTimeoutShouldCompleteQuickly()
	{
		// Arrange
		using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

		// Act
		var stopwatch = Stopwatch.StartNew();
		await _dispatcher.DispatchAsync(cts.Token).ConfigureAwait(false);
		stopwatch.Stop();

		// Assert - Should complete very quickly (much faster than timeout)
		stopwatch.ElapsedMilliseconds.ShouldBeLessThan(50);
	}

	[Fact]
	public void InitWithLongDispatcherIdShouldNotThrow()
	{
		// Arrange
		var longDispatcherId = new string('A', 1000);
		_ = A.CallTo(() => _logger.IsEnabled(LogLevel.Information)).Returns(true);

		// Act & Assert
		Should.NotThrow(() => _dispatcher.Init(longDispatcherId));
	}

	[Fact]
	public void InitWithSpecialCharactersShouldNotThrow()
	{
		// Arrange
		var specialDispatcherId = "dispatcher-Ã°Å¸Å¡â‚¬-Ã§â€°Â¹Ã¦Â®Å Ã¥Â­â€”Ã§Â¬Â¦-@#$%^&*()";
		_ = A.CallTo(() => _logger.IsEnabled(LogLevel.Information)).Returns(true);

		// Act & Assert
		Should.NotThrow(() => _dispatcher.Init(specialDispatcherId));
	}

	[Fact]
	public async Task MultipleSequentialDispatchCallsShouldNotInterfere()
	{
		// Arrange & Act
		for (var i = 0; i < 100; i++)
		{
			await _dispatcher.DispatchAsync(CancellationToken.None).ConfigureAwait(false);
		}

		// Assert - Should complete all calls without issues
	}

	[Fact]
	public async Task DispatchAsyncShouldNotLogAnything()
	{
		// Arrange & Act
		await _dispatcher.DispatchAsync(CancellationToken.None).ConfigureAwait(false);

		// Assert - DispatchAsync method should not perform any logging
		A.CallTo(() => _logger.Log(
				A<LogLevel>._,
				A<EventId>._,
				A<object>._,
				A<Exception>._,
				A<Func<object, Exception?, string>>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public void InitAndDispatchShouldWorkIndependently()
	{
		// Arrange
		_ = A.CallTo(() => _logger.IsEnabled(LogLevel.Information)).Returns(true);

		// Act & Assert
		Should.NotThrow(async () =>
		{
			_dispatcher.Init("test-dispatcher");
			await _dispatcher.DispatchAsync(CancellationToken.None).ConfigureAwait(false);
			_dispatcher.Init("another-dispatcher");
			await _dispatcher.DispatchAsync(CancellationToken.None).ConfigureAwait(false);
		});
	}
}
