// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;

using Excalibur.Dispatch.Delivery;

namespace Excalibur.Dispatch.Tests.Functional.Workflows.LoadTesting.Fixtures;

/// <summary>
/// Result from a dispatch test operation.
/// </summary>
public sealed record DispatchTestResult
{
	/// <summary>
	/// Gets a value indicating whether the dispatch succeeded.
	/// </summary>
	public bool Success { get; init; }

	/// <summary>
	/// Gets the latency in milliseconds.
	/// </summary>
	public double LatencyMs { get; init; }

	/// <summary>
	/// Gets the error message if failed.
	/// </summary>
	public string Error { get; init; } = string.Empty;
}

/// <summary>
/// Test client for load testing scenarios using real IDispatcher.
/// </summary>
/// <remarks>
/// Sprint 198 - NBomber Load Testing Integration.
/// Provides real dispatcher integration for functional tests.
/// </remarks>
public sealed class DispatchLoadTestClient : IAsyncDisposable
{
	private readonly ServiceProvider _serviceProvider;
	private readonly IDispatcher _dispatcher;
	private readonly IMessageContextFactory _contextFactory;
	private bool _disposed;

	/// <summary>
	/// Creates a new load test client with real dispatcher integration.
	/// </summary>
	public DispatchLoadTestClient()
	{
		var services = new ServiceCollection();

		// Register logging (required by Dispatch)
		_ = services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));

		// Register load test handlers BEFORE AddDispatch so they get scanned
		_ = services.AddTransient<IActionHandler<LoadTestCommand>, LoadTestCommandHandler>();
		_ = services.AddTransient<IActionHandler<CdcTestEvent>, CdcTestEventHandler>();

		// Register core dispatch services and scan THIS assembly for handlers
		_ = services.AddDispatchPipeline();
		_ = services.AddDispatchHandlers(typeof(DispatchLoadTestClient).Assembly);

		_serviceProvider = services.BuildServiceProvider();

		// Trigger LocalMessageBus registration by requesting the keyed IMessageBus
		_ = _serviceProvider.GetRequiredKeyedService<IMessageBus>("Local");

		_dispatcher = _serviceProvider.GetRequiredService<IDispatcher>();
		_contextFactory = _serviceProvider.GetRequiredService<IMessageContextFactory>();

		// Reset handler counters
		LoadTestCommandHandler.ResetCount();
		CdcTestEventHandler.ResetCounters();
	}

	/// <summary>
	/// Gets the total number of commands handled.
	/// </summary>
	public long HandledCount => LoadTestCommandHandler.HandledCount;

	/// <summary>
	/// Gets the total number of CDC events handled.
	/// </summary>
	public long CdcHandledCount => CdcTestEventHandler.HandledCount;

	/// <summary>
	/// Dispatches a load test command through the real dispatcher pipeline.
	/// </summary>
	public async Task<DispatchTestResult> DispatchAsync(CancellationToken cancellationToken)
	{
		var sw = Stopwatch.StartNew();

		try
		{
			var command = new LoadTestCommand();
			var context = _contextFactory.CreateContext();
			context.MessageId = Guid.NewGuid().ToString();

			var result = await _dispatcher.DispatchAsync(command, context, cancellationToken).ConfigureAwait(false);

			sw.Stop();

			return new DispatchTestResult
			{
				Success = result.IsSuccess,
				LatencyMs = sw.Elapsed.TotalMilliseconds,
				Error = result.IsSuccess ? string.Empty : result.ErrorMessage ?? "Dispatch failed",
			};
		}
		catch (Exception ex)
		{
			sw.Stop();

			return new DispatchTestResult
			{
				Success = false,
				LatencyMs = sw.Elapsed.TotalMilliseconds,
				Error = ex.Message,
			};
		}
	}

	/// <summary>
	/// Dispatches a CDC load test event through the real dispatcher pipeline.
	/// </summary>
	public async Task<DispatchTestResult> DispatchCdcAsync(long sequenceNumber, CancellationToken cancellationToken)
	{
		var sw = Stopwatch.StartNew();

		try
		{
			var cdcEvent = new CdcTestEvent { SequenceNumber = sequenceNumber };
			var context = _contextFactory.CreateContext();
			context.MessageId = Guid.NewGuid().ToString();

			var result = await _dispatcher.DispatchAsync(cdcEvent, context, cancellationToken).ConfigureAwait(false);

			sw.Stop();

			return new DispatchTestResult
			{
				Success = result.IsSuccess,
				LatencyMs = sw.Elapsed.TotalMilliseconds,
				Error = result.IsSuccess ? string.Empty : result.ErrorMessage ?? "CDC dispatch failed",
			};
		}
		catch (Exception ex)
		{
			sw.Stop();

			return new DispatchTestResult
			{
				Success = false,
				LatencyMs = sw.Elapsed.TotalMilliseconds,
				Error = ex.Message,
			};
		}
	}

	/// <inheritdoc />
	public async ValueTask DisposeAsync()
	{
		if (_disposed)
			return;
		_disposed = true;

		await _serviceProvider.DisposeAsync().ConfigureAwait(false);
	}
}

/// <summary>
/// Simple test command for load testing.
/// </summary>
public sealed record LoadTestCommand : IDispatchAction
{
	/// <summary>
	/// Gets the unique identifier for this command.
	/// </summary>
	public Guid Id { get; init; } = Guid.NewGuid();

	/// <summary>
	/// Gets the timestamp when the command was created.
	/// </summary>
	public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Handler for load test commands.
/// </summary>
public sealed class LoadTestCommandHandler : IActionHandler<LoadTestCommand>
{
	private static long _handledCount;

	/// <summary>
	/// Gets the number of handled commands.
	/// </summary>
	public static long HandledCount => Interlocked.Read(ref _handledCount);

	/// <summary>
	/// Resets the handled count.
	/// </summary>
	public static void ResetCount() => Interlocked.Exchange(ref _handledCount, 0);

	/// <inheritdoc />
	public Task HandleAsync(LoadTestCommand action, CancellationToken cancellationToken)
	{
		_ = Interlocked.Increment(ref _handledCount);
		return Task.CompletedTask;
	}
}

/// <summary>
/// CDC test event for load testing.
/// </summary>
public sealed record CdcTestEvent : IDispatchAction
{
	/// <summary>
	/// Gets the unique identifier for this event.
	/// </summary>
	public Guid Id { get; init; } = Guid.NewGuid();

	/// <summary>
	/// Gets the sequence number for ordering.
	/// </summary>
	public long SequenceNumber { get; init; }
}

/// <summary>
/// Handler for CDC test events.
/// </summary>
public sealed class CdcTestEventHandler : IActionHandler<CdcTestEvent>
{
	private static long _handledCount;
	private static long _lastSequence;
	private static long _violations;

	/// <summary>
	/// Gets the number of handled CDC events.
	/// </summary>
	public static long HandledCount => Interlocked.Read(ref _handledCount);

	/// <summary>
	/// Gets the number of ordering violations.
	/// </summary>
	public static long Violations => Interlocked.Read(ref _violations);

	/// <summary>
	/// Resets the counters.
	/// </summary>
	public static void ResetCounters()
	{
		_ = Interlocked.Exchange(ref _handledCount, 0);
		_ = Interlocked.Exchange(ref _lastSequence, 0);
		_ = Interlocked.Exchange(ref _violations, 0);
	}

	/// <inheritdoc />
	public Task HandleAsync(CdcTestEvent action, CancellationToken cancellationToken)
	{
		_ = Interlocked.Increment(ref _handledCount);

		// Track ordering violations (for concurrent scenarios, some violations are expected)
		var lastSeq = Interlocked.Read(ref _lastSequence);
		if (action.SequenceNumber < lastSeq)
		{
			_ = Interlocked.Increment(ref _violations);
		}

		_ = Interlocked.Exchange(ref _lastSequence, action.SequenceNumber);

		return Task.CompletedTask;
	}
}
