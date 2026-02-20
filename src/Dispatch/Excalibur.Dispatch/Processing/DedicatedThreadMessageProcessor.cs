// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Channels;

using Excalibur.Dispatch.Diagnostics;
using Excalibur.Dispatch.Metrics;
using Excalibur.Dispatch.Pooling;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Processing;

/// <summary>
/// High-performance message processor that uses dedicated threads instead of async/await to avoid Task allocation overhead and thread pool
/// contention in hot paths.
/// </summary>
public sealed partial class DedicatedThreadMessageProcessor<TMessage> : IDisposable
	where TMessage : unmanaged
{
	private readonly int _threadCount;
	private readonly Thread[] _processingThreads;
	private readonly Channel<ProcessingRequest> _incomingMessages;
	private readonly IMessageHandler<TMessage> _handler;
	private readonly MessageBufferPool _bufferPool;
	private readonly ILogger<DedicatedThreadMessageProcessor<TMessage>> _logger;

	/// <summary>
	/// Metrics.
	/// </summary>
	private readonly RateCounter _messagesProcessed;

	private readonly RateCounter _processingErrors;
	private readonly ValueHistogram _processingLatency;

	private readonly CancellationTokenSource _shutdownCts;

	/// <summary>
	/// Cache-aligned counters for lock-free updates.
	/// </summary>
	private readonly CacheAlignedCounter[] _threadLocalProcessed;

	private readonly CacheAlignedCounter[] _threadLocalErrors;
	private volatile bool _isRunning;

	/// <summary>
	/// Initializes a new instance of the <see cref="DedicatedThreadMessageProcessor{TMessage}" /> class. Initializes a new instance of the
	/// dedicated thread message processor with the specified configuration. This processor creates dedicated background threads for message
	/// processing to avoid Task allocation overhead.
	/// </summary>
	/// <param name="threadCount"> The number of dedicated processing threads to create. </param>
	/// <param name="handler"> The message handler responsible for processing individual messages. </param>
	/// <param name="bufferPool"> The buffer pool for efficient memory management during processing. </param>
	/// <param name="logger"> The logger for capturing processing events and diagnostics. </param>
	/// <param name="metrics"> Optional metrics registry for performance monitoring. </param>
	/// <exception cref="ArgumentException"> Thrown when threadCount is less than or equal to zero. </exception>
	/// <exception cref="ArgumentNullException"> Thrown when handler, bufferPool, or logger is null. </exception>
	public DedicatedThreadMessageProcessor(
		int threadCount,
		IMessageHandler<TMessage> handler,
		MessageBufferPool bufferPool,
		ILogger<DedicatedThreadMessageProcessor<TMessage>> logger,
		MetricRegistry? metrics = null)
	{
		if (threadCount <= 0)
		{
			throw new ArgumentException(Resources.DedicatedThreadMessageProcessor_ThreadCountMustBePositive, nameof(threadCount));
		}

		_threadCount = threadCount;
		_handler = handler ?? throw new ArgumentNullException(nameof(handler));
		_bufferPool = bufferPool ?? throw new ArgumentNullException(nameof(bufferPool));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));

		// Use unbounded channel with multiple readers for best performance
		_incomingMessages = Channel.CreateUnbounded<ProcessingRequest>(new UnboundedChannelOptions
		{
			SingleWriter = false,
			SingleReader = false,
			AllowSynchronousContinuations = false,
		});

		// Initialize metrics
		var registry = metrics ?? MetricRegistry.Global;
		_messagesProcessed = registry.Counter("dedicated_processor_messages_processed");
		_processingErrors = registry.Counter("dedicated_processor_errors");
		_processingLatency = registry.Histogram(
			"dedicated_processor_latency_us",
			"Dedicated thread processor latency in microseconds",
			"us",
			HistogramConfiguration.Exponential(1, 2, 20)); // 1us to 1s

		// Initialize cache-aligned counters
		_threadLocalProcessed = new CacheAlignedCounter[threadCount];
		_threadLocalErrors = new CacheAlignedCounter[threadCount];
		for (var i = 0; i < threadCount; i++)
		{
			_threadLocalProcessed[i] = default(CacheAlignedCounter);
			_threadLocalErrors[i] = default(CacheAlignedCounter);
		}

		_shutdownCts = new CancellationTokenSource();
		_processingThreads = new Thread[threadCount];
	}

	/// <summary>
	/// Starts the dedicated processing threads and begins accepting messages for processing. This method creates and starts all configured
	/// processing threads in the background.
	/// </summary>
	/// <exception cref="InvalidOperationException"> Thrown when the processor is already running. </exception>
	public void Start()
	{
		if (_isRunning)
		{
			throw new InvalidOperationException(Resources.DedicatedThreadMessageProcessor_AlreadyRunning);
		}

		_isRunning = true;

		// Create and start dedicated processing threads
		for (var i = 0; i < _threadCount; i++)
		{
			var threadIndex = i;
			_processingThreads[i] = new Thread(() => ProcessingThreadMain(threadIndex))
			{
				Name = $"DedicatedProcessor-{typeof(TMessage).Name}-{i}",
				IsBackground = false,
				Priority = ThreadPriority.AboveNormal,
			};
			_processingThreads[i].Start();
		}

		LogProcessorStarted(_threadCount, typeof(TMessage).Name);
	}

	/// <summary>
	/// Stops the dedicated processing threads and completes any remaining message processing. This method signals shutdown and waits up to
	/// 5 seconds for each thread to complete gracefully.
	/// </summary>
	public void Stop()
	{
		if (!_isRunning)
		{
			return;
		}

		_isRunning = false;
		_shutdownCts.Cancel();
		_ = _incomingMessages.Writer.TryComplete();

		// Wait for all threads to complete
		foreach (var thread in _processingThreads)
		{
			_ = (thread?.Join(TimeSpan.FromSeconds(5)));
		}

		LogProcessorStopped(typeof(TMessage).Name);
	}

	/// <summary>
	/// Submit a message for processing. This method is lock-free and allocation-free.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool TrySubmit(in TMessage message, ulong correlationId = 0)
	{
		if (!_isRunning)
		{
			return false;
		}

		var request = new ProcessingRequest(
			message: message,
			correlationId: correlationId,
			submitTimestamp: Stopwatch.GetTimestamp());

		return _incomingMessages.Writer.TryWrite(request);
	}

	/// <summary>
	/// Submit a batch of messages for processing.
	/// </summary>
	public int SubmitBatch(ReadOnlySpan<TMessage> messages)
	{
		if (!_isRunning)
		{
			return 0;
		}

		var submitted = 0;
		var timestamp = Stopwatch.GetTimestamp();

		foreach (ref readonly var message in messages)
		{
			var request = new ProcessingRequest(
				message: message,
				correlationId: 0,
				submitTimestamp: timestamp);

			if (_incomingMessages.Writer.TryWrite(request))
			{
				submitted++;
			}
			else
			{
				break;
			}
		}

		return submitted;
	}

	/// <summary>
	/// Gets comprehensive statistics about the processor's performance and operation status. This includes message processing counts, error
	/// rates, and performance metrics from all threads.
	/// </summary>
	/// <returns> Statistics object containing aggregated metrics from all processing threads. </returns>
	public ProcessorStatistics GetStatistics()
	{
		long totalProcessed = 0;
		long totalErrors = 0;

		for (var i = 0; i < _threadCount; i++)
		{
			totalProcessed += _threadLocalProcessed[i].Value;
			totalErrors += _threadLocalErrors[i].Value;
		}

		return new ProcessorStatistics(
			totalMessagesProcessed: totalProcessed,
			totalErrors: totalErrors,
			averageLatencyUs: 0, // _processingLatency.GetSnapshot().Mean,
			p99LatencyUs: 0, // _processingLatency.GetSnapshot().Percentile99,
			activeThreads: CountActiveThreads());
	}

	/// <summary>
	/// Disposes the processor by stopping all processing threads and releasing managed resources. This method ensures a clean shutdown of
	/// all background processing operations.
	/// </summary>
	public void Dispose()
	{
		Stop();
		_shutdownCts.Dispose();
	}

	private static void SetThreadAffinity(int threadIndex)
	{
		try
		{
			// Spread threads across available cores
			var coreCount = Environment.ProcessorCount;
			var targetCore = threadIndex % coreCount;

			if (OperatingSystem.IsWindows())
			{
				// Windows: Use SetThreadAffinityMask
				var mask = (IntPtr)(1L << targetCore);
				_ = NativeMethods.SetThreadAffinityMask(NativeMethods.GetCurrentThread(), mask);
			}
			else if (OperatingSystem.IsLinux())
			{
				// Linux: Would use sched_setaffinity via P/Invoke For now, just set thread priority
				Thread.CurrentThread.Priority = ThreadPriority.Highest;
			}
		}
		catch
		{
			// Ignore affinity errors - not critical
		}
	}

	private void ProcessingThreadMain(int threadIndex)
	{
		// Pin thread to specific CPU core for better cache locality
		if (OperatingSystem.IsWindows() || OperatingSystem.IsLinux())
		{
			SetThreadAffinity(threadIndex);
		}

		var reader = _incomingMessages.Reader;
		var cancellationToken = _shutdownCts.Token;
		ref var localProcessed = ref _threadLocalProcessed[threadIndex];
		ref var localErrors = ref _threadLocalErrors[threadIndex];

		// Pre-allocate response buffer to avoid allocations in hot path
		var responseBuffer = _bufferPool.Rent(4096); // 4KB default buffer size

		try
		{
			while (!cancellationToken.IsCancellationRequested)
			{
				// Synchronous read - blocks until message available or channel closed
				if (!reader.TryRead(out var request))
				{
					// For dedicated threads, use blocking wait with timeout to avoid async overhead This is safe because we're already on a
					// dedicated background thread
					try
					{
						using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
						timeoutCts.CancelAfter(TimeSpan.FromMilliseconds(100)); // 100ms timeout for responsiveness

						// Block synchronously on dedicated thread - this is the correct pattern for dedicated threads
						if (!reader.WaitToReadAsync(timeoutCts.Token).AsTask().Wait(100, cancellationToken))
						{
							// Timeout occurred, check cancellation and continue loop
							if (cancellationToken.IsCancellationRequested)
							{
								break;
							}
						}
					}
					catch (OperationCanceledException)
					{
						// Expected during shutdown
						break;
					}

					continue;
				}

				var startTimestamp = Stopwatch.GetTimestamp();

				try
				{
					// Process message synchronously
					var result = _handler.ProcessMessage(
						in request.Message,
						request.CorrelationId,
						responseBuffer.Buffer.AsSpan());

					if (result.Success)
					{
						_ = localProcessed.Increment();
						_ = _messagesProcessed.IncrementBy(1L);
					}
					else
					{
						_ = localErrors.Increment();
						_ = _processingErrors.IncrementBy(1L);
					}

					// Record latency in microseconds
					var elapsedTicks = Stopwatch.GetTimestamp() - startTimestamp;
					var latencyUs = elapsedTicks * 1_000_000.0 / Stopwatch.Frequency;
					_processingLatency.Record((long)latencyUs);
				}
				catch (Exception ex)
				{
					_ = localErrors.Increment();

					// _processingErrors.Increment();
					LogProcessingError(ex, threadIndex);
				}
			}
		}
		catch (OperationCanceledException)
		{
			// Expected during shutdown
		}
		catch (Exception ex)
		{
			LogThreadFatalError(ex, threadIndex);
		}
		finally
		{
			responseBuffer.Dispose();
		}
	}

	private int CountActiveThreads()
	{
		var count = 0;
		for (var i = 0; i < _processingThreads.Length; i++)
		{
			if (_processingThreads[i]?.IsAlive == true)
			{
				count++;
			}
		}

		return count;
	}

	// Source-generated logging methods
	[LoggerMessage(CoreEventId.DedicatedProcessorStarted, LogLevel.Information,
		"Dedicated thread processor started with {ThreadCount} threads for message type {MessageType}")]
	private partial void LogProcessorStarted(int threadCount, string messageType);

	[LoggerMessage(CoreEventId.DedicatedProcessorStopped, LogLevel.Information,
		"Dedicated thread processor stopped for message type {MessageType}")]
	private partial void LogProcessorStopped(string messageType);

	[LoggerMessage(CoreEventId.DedicatedProcessorError, LogLevel.Error,
		"Error processing message on thread {ThreadIndex}")]
	private partial void LogProcessingError(Exception ex, int threadIndex);

	[LoggerMessage(CoreEventId.DedicatedProcessorFatalError, LogLevel.Critical,
		"Fatal error in processing thread {ThreadIndex}")]
	private partial void LogThreadFatalError(Exception ex, int threadIndex);

	/// <summary>
	/// Internal request structure for zero-allocation message passing.
	/// </summary>
	[StructLayout(LayoutKind.Auto)]
	private readonly struct ProcessingRequest(in TMessage message, ulong correlationId, long submitTimestamp)
	{
		public readonly TMessage Message = message;
		public readonly ulong CorrelationId = correlationId;
		public readonly long SubmitTimestamp = submitTimestamp;
	}
}
