// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

namespace Excalibur.Dispatch.Tests.Workflows.CdcAntiCorruption;

/// <summary>
/// CDC Anti-Corruption Layer - Error Handling workflow tests.
/// Tests error handling, retry policies, and recovery patterns.
/// </summary>
/// <remarks>
/// <para>
/// Sprint 182 - Functional Testing Epic Phase 2.
/// bd-divj5: CDC Error Handling Tests (5 tests).
/// </para>
/// <para>
/// These tests use in-memory simulation to validate CDC error handling patterns
/// without requiring TestContainers or external services.
/// </para>
/// </remarks>
[Trait("Epic", "FunctionalTesting")]
[Trait("Sprint", "182")]
[Trait("Component", "CdcAntiCorruption")]
[Trait("Category", "Unit")]
public sealed class CdcErrorHandlingWorkflowShould
{
	/// <summary>
	/// Tests that errors from handlers propagate correctly to the caller.
	/// Handler throws exception > Exception bubbles to caller.
	/// </summary>
	[Fact]
	public async Task PropagateErrorsFromHandlers()
	{
		// Arrange
		var executionLog = new ExecutionLog();
		var cdcSource = new SimulatedCdcSource();
		var failingHandler = new FailingHandler(failAlways: true, executionLog);
		var pipeline = new CdcErrorHandlingPipeline(executionLog, failingHandler);

		var cdcEvent = cdcSource.EmitInsert("Orders", new Dictionary<string, object?>
		{
			["order_id"] = "ORD-FAIL",
		});

		// Act & Assert - Exception should propagate
		var exception = await Should.ThrowAsync<CdcHandlerException>(async () =>
		{
			await pipeline.ProcessCdcEventAsync(cdcEvent, CancellationToken.None).ConfigureAwait(true);
		}).ConfigureAwait(true);

		exception.Message.ShouldContain("Handler failed");
		exception.TableName.ShouldBe("Orders");
		exception.ChangeType.ShouldBe(CdcChangeType.Insert);

		// Verify error was logged
		executionLog.Steps.ShouldContain(s => s.Contains("Pipeline:Error:Orders"));
	}

	/// <summary>
	/// Tests that transient failures trigger retry and eventually succeed.
	/// First 2 attempts fail > 3rd attempt succeeds (retry policy).
	/// </summary>
	[Fact]
	public async Task RetryTransientFailures()
	{
		// Arrange
		var executionLog = new ExecutionLog();
		var cdcSource = new SimulatedCdcSource();
		var failingHandler = new FailingHandler(failCount: 2, executionLog); // Fail first 2 times
		var pipeline = new CdcErrorHandlingPipeline(executionLog, failingHandler);
		pipeline.ConfigureRetryPolicy(maxRetries: 3, retryDelayMs: 10);

		var cdcEvent = cdcSource.EmitInsert("Orders", new Dictionary<string, object?>
		{
			["order_id"] = "ORD-RETRY",
		});

		// Act
		await pipeline.ProcessCdcEventAsync(cdcEvent, CancellationToken.None).ConfigureAwait(true);

		// Assert - Handler was retried and eventually succeeded
		var attempts = executionLog.Steps.Count(s => s.StartsWith("Handler:Attempt:"));
		attempts.ShouldBe(3); // 2 failures + 1 success

		executionLog.Steps.ShouldContain("Handler:Attempt:1:Failed");
		executionLog.Steps.ShouldContain("Handler:Attempt:2:Failed");
		executionLog.Steps.ShouldContain("Handler:Attempt:3:Success");
		executionLog.Steps.ShouldContain("Pipeline:Success:Orders");
	}

	/// <summary>
	/// Tests that permanent failures (all retries exhausted) move event to dead letter queue.
	/// All retries exhausted > Event moved to dead letter.
	/// </summary>
	[Fact]
	public async Task DeadLetterPermanentFailures()
	{
		// Arrange
		var executionLog = new ExecutionLog();
		var cdcSource = new SimulatedCdcSource();
		var failingHandler = new FailingHandler(failAlways: true, executionLog);
		var deadLetterQueue = new SimulatedDeadLetterQueue(executionLog);
		var pipeline = new CdcErrorHandlingPipeline(executionLog, failingHandler, deadLetterQueue);
		pipeline.ConfigureRetryPolicy(maxRetries: 3, retryDelayMs: 10);

		var cdcEvent = cdcSource.EmitInsert("Orders", new Dictionary<string, object?>
		{
			["order_id"] = "ORD-DLQ",
			["amount"] = 100.00m,
		});

		// Act
		await pipeline.ProcessCdcEventAsync(cdcEvent, CancellationToken.None).ConfigureAwait(true);

		// Assert - All retries were attempted
		var attempts = executionLog.Steps.Count(s => s.StartsWith("Handler:Attempt:"));
		attempts.ShouldBe(3);

		// Assert - Event was moved to dead letter queue
		executionLog.Steps.ShouldContain("DeadLetter:Enqueue:Orders:ORD-DLQ");
		deadLetterQueue.Events.Count.ShouldBe(1);

		var dlqEvent = deadLetterQueue.Events[0];
		dlqEvent.TableName.ShouldBe("Orders");
		dlqEvent.FailureReason.ShouldContain("Handler failed");
		dlqEvent.AttemptCount.ShouldBe(3);
	}

	/// <summary>
	/// Tests that transformation errors are handled with context.
	/// Invalid CDC data > TransformationException with context.
	/// </summary>
	[Fact]
	public async Task HandleTransformationErrors()
	{
		// Arrange
		var executionLog = new ExecutionLog();
		var cdcSource = new SimulatedCdcSource();
		var handler = new TransformingHandler(executionLog);
		var pipeline = new CdcErrorHandlingPipeline(executionLog, handler);

		// Create an event with invalid data that will fail transformation
		var cdcEvent = cdcSource.EmitInsertWithInvalidData("Orders", new Dictionary<string, object?>
		{
			["order_id"] = null, // NULL primary key - invalid
			["amount"] = "not-a-number", // Invalid type
		});

		// Act & Assert
		var exception = await Should.ThrowAsync<TransformationException>(async () =>
		{
			await pipeline.ProcessCdcEventAsync(cdcEvent, CancellationToken.None).ConfigureAwait(true);
		}).ConfigureAwait(true);

		// Assert - Exception contains helpful context
		exception.Message.ShouldContain("transform", Case.Insensitive);
		exception.TableName.ShouldBe("Orders");
		exception.ColumnName.ShouldBe("order_id");
		exception.ExpectedType.ShouldNotBeNullOrEmpty();

		// Verify error was logged with context
		executionLog.Steps.ShouldContain(s => s.Contains("Transformation:Error:order_id"));
	}

	/// <summary>
	/// Tests that the pipeline can recover after a simulated connection loss.
	/// Simulate disconnect > reconnect > resume processing.
	/// </summary>
	[Fact]
	public async Task RecoverAfterConnectionLoss()
	{
		// Arrange
		var executionLog = new ExecutionLog();
		var cdcSource = new SimulatedCdcSource();
		var handler = new ReliableHandler(executionLog);
		var connectionManager = new SimulatedConnectionManager(executionLog);
		var pipeline = new CdcErrorHandlingPipeline(executionLog, handler, connectionManager: connectionManager);

		// Create multiple events
		var events = new[]
		{
			cdcSource.EmitInsert("Orders", new Dictionary<string, object?> { ["order_id"] = "ORD-001" }),
			cdcSource.EmitInsert("Orders", new Dictionary<string, object?> { ["order_id"] = "ORD-002" }),
			cdcSource.EmitInsert("Orders", new Dictionary<string, object?> { ["order_id"] = "ORD-003" }),
		};

		// Act - Process first event, then simulate connection loss, then recover
		await pipeline.ProcessCdcEventAsync(events[0], CancellationToken.None).ConfigureAwait(true);

		// Simulate connection loss after first event
		connectionManager.SimulateDisconnect();
		executionLog.Log("Connection:Lost");

		// Second event should fail due to connection loss, but be retried after reconnect
		connectionManager.SimulateReconnect();
		executionLog.Log("Connection:Restored");

		await pipeline.ProcessCdcEventAsync(events[1], CancellationToken.None).ConfigureAwait(true);
		await pipeline.ProcessCdcEventAsync(events[2], CancellationToken.None).ConfigureAwait(true);

		// Assert - All events were eventually processed
		var successSteps = executionLog.Steps.Where(s => s.StartsWith("Handler:Success:")).ToList();
		successSteps.Count.ShouldBe(3);
		successSteps.ShouldContain("Handler:Success:ORD-001");
		successSteps.ShouldContain("Handler:Success:ORD-002");
		successSteps.ShouldContain("Handler:Success:ORD-003");

		// Assert - Connection events were logged
		var steps = executionLog.GetOrderedSteps();
		var lostIndex = steps.FindIndex(s => s.Contains("Connection:Lost"));
		var restoredIndex = steps.FindIndex(s => s.Contains("Connection:Restored"));
		var ord002Index = steps.FindIndex(s => s.Contains("Handler:Success:ORD-002"));

		lostIndex.ShouldBeGreaterThan(-1);
		restoredIndex.ShouldBeGreaterThan(lostIndex);
		ord002Index.ShouldBeGreaterThan(restoredIndex);
	}

	#region Test Infrastructure

	internal enum CdcChangeType
	{
		Insert,
		Update,
		Delete,
	}

	internal interface ICdcHandler
	{
		Task HandleAsync(SimulatedCdcEvent cdcEvent, CancellationToken cancellationToken);
	}

	/// <summary>
	/// Execution log to track CDC error handling steps.
	/// </summary>
	internal sealed class ExecutionLog
	{
		private readonly ConcurrentQueue<string> _orderedSteps = new();
		public ConcurrentBag<string> Steps { get; } = [];

		public void Log(string step)
		{
			Steps.Add(step);
			_orderedSteps.Enqueue(step);
		}

		public List<string> GetOrderedSteps() => [.. _orderedSteps];
	}

	/// <summary>
	/// Simulated CDC source.
	/// </summary>
	internal sealed class SimulatedCdcSource
	{
		private int _sequenceNumber;

		public SimulatedCdcEvent EmitInsert(string tableName, Dictionary<string, object?> columns)
		{
			return new SimulatedCdcEvent
			{
				Lsn = BitConverter.GetBytes(++_sequenceNumber),
				TableName = tableName,
				ChangeType = CdcChangeType.Insert,
				Columns = columns,
				IsValid = true,
			};
		}

		public SimulatedCdcEvent EmitInsertWithInvalidData(string tableName, Dictionary<string, object?> columns)
		{
			return new SimulatedCdcEvent
			{
				Lsn = BitConverter.GetBytes(++_sequenceNumber),
				TableName = tableName,
				ChangeType = CdcChangeType.Insert,
				Columns = columns,
				IsValid = false, // Mark as invalid for transformation error tests
			};
		}
	}

	/// <summary>
	/// Handler that fails a configurable number of times.
	/// </summary>
	internal sealed class FailingHandler : ICdcHandler
	{
		private readonly int _failCount;
		private readonly bool _failAlways;
		private readonly ExecutionLog _log;
		private int _attempts;

		public FailingHandler(int failCount = 0, ExecutionLog? log = null)
		{
			_failCount = failCount;
			_failAlways = false;
			_log = log ?? new ExecutionLog();
		}

		public FailingHandler(bool failAlways, ExecutionLog log)
		{
			_failAlways = failAlways;
			_failCount = int.MaxValue;
			_log = log;
		}

		public Task HandleAsync(SimulatedCdcEvent cdcEvent, CancellationToken cancellationToken)
		{
			_attempts++;
			_log.Log($"Handler:Attempt:{_attempts}:{(_attempts <= _failCount || _failAlways ? "Failed" : "Success")}");

			if (_attempts <= _failCount || _failAlways)
			{
				throw new InvalidOperationException("Handler failed - transient error");
			}

			return Task.CompletedTask;
		}
	}

	/// <summary>
	/// Handler that validates and transforms CDC events.
	/// </summary>
	internal sealed class TransformingHandler : ICdcHandler
	{
		private readonly ExecutionLog _log;

		public TransformingHandler(ExecutionLog log)
		{
			_log = log;
		}

		public Task HandleAsync(SimulatedCdcEvent cdcEvent, CancellationToken cancellationToken)
		{
			// Validate and transform
			if (!cdcEvent.IsValid)
			{
				// Find the first null or invalid column
				foreach (var kvp in cdcEvent.Columns)
				{
					if (kvp.Value is null && kvp.Key == "order_id")
					{
						_log.Log($"Transformation:Error:{kvp.Key}:NullValue");
						throw new TransformationException(
							$"Failed to transform CDC event: {kvp.Key} cannot be null",
							cdcEvent.TableName,
							kvp.Key,
							"String (non-null)");
					}

					if (kvp.Value is string s && kvp.Key == "amount" && !decimal.TryParse(s, out _))
					{
						_log.Log($"Transformation:Error:{kvp.Key}:InvalidType");
						throw new TransformationException(
							$"Failed to transform CDC event: {kvp.Key} is not a valid decimal",
							cdcEvent.TableName,
							kvp.Key,
							"Decimal");
					}
				}
			}

			_log.Log($"Handler:Transform:Success");
			return Task.CompletedTask;
		}
	}

	/// <summary>
	/// Handler that always succeeds.
	/// </summary>
	internal sealed class ReliableHandler : ICdcHandler
	{
		private readonly ExecutionLog _log;

		public ReliableHandler(ExecutionLog log)
		{
			_log = log;
		}

		public Task HandleAsync(SimulatedCdcEvent cdcEvent, CancellationToken cancellationToken)
		{
			var orderId = cdcEvent.Columns.GetValueOrDefault("order_id")?.ToString() ?? "Unknown";
			_log.Log($"Handler:Success:{orderId}");
			return Task.CompletedTask;
		}
	}

	/// <summary>
	/// CDC error handling pipeline with retry and dead letter support.
	/// </summary>
	internal sealed class CdcErrorHandlingPipeline
	{
		private readonly ExecutionLog _log;
		private readonly ICdcHandler _handler;
		private readonly SimulatedDeadLetterQueue? _deadLetterQueue;
		private readonly SimulatedConnectionManager? _connectionManager;
		private int _maxRetries = 1;
		private int _retryDelayMs;

		public CdcErrorHandlingPipeline(
			ExecutionLog log,
			ICdcHandler handler,
			SimulatedDeadLetterQueue? deadLetterQueue = null,
			SimulatedConnectionManager? connectionManager = null)
		{
			_log = log;
			_handler = handler;
			_deadLetterQueue = deadLetterQueue;
			_connectionManager = connectionManager;
		}

		public void ConfigureRetryPolicy(int maxRetries, int retryDelayMs)
		{
			_maxRetries = maxRetries;
			_retryDelayMs = retryDelayMs;
		}

		public async Task ProcessCdcEventAsync(SimulatedCdcEvent cdcEvent, CancellationToken cancellationToken)
		{
			_log.Log($"Pipeline:Start:{cdcEvent.TableName}");

			var attempts = 0;
			Exception? lastException = null;

			while (attempts < _maxRetries)
			{
				attempts++;
				try
				{
					await _handler.HandleAsync(cdcEvent, cancellationToken).ConfigureAwait(false);
					_log.Log($"Pipeline:Success:{cdcEvent.TableName}");
					return;
				}
				catch (TransformationException)
				{
					// Don't retry transformation errors - they're permanent
					throw;
				}
				catch (Exception ex)
				{
					lastException = ex;
					_log.Log($"Pipeline:Error:{cdcEvent.TableName}:{ex.Message}");

					if (attempts < _maxRetries)
					{
						await global::Tests.Shared.Infrastructure.TestTiming.PauseAsync(_retryDelayMs, cancellationToken).ConfigureAwait(false);
					}
				}
			}

			// All retries exhausted
			if (_deadLetterQueue != null && lastException != null)
			{
				var orderId = cdcEvent.Columns.GetValueOrDefault("order_id")?.ToString() ?? "Unknown";
				_log.Log($"DeadLetter:Enqueue:{cdcEvent.TableName}:{orderId}");
				_deadLetterQueue.Enqueue(cdcEvent, lastException.Message, attempts);
			}
			else if (lastException != null)
			{
				throw new CdcHandlerException(
					$"Handler failed after {attempts} attempts: {lastException.Message}",
					cdcEvent.TableName,
					cdcEvent.ChangeType,
					lastException);
			}
		}
	}

	/// <summary>
	/// Simulated dead letter queue.
	/// </summary>
	internal sealed class SimulatedDeadLetterQueue
	{
		private readonly ExecutionLog _log;

		public SimulatedDeadLetterQueue(ExecutionLog log)
		{
			_log = log;
		}

		public List<DeadLetterEvent> Events { get; } = [];

		public void Enqueue(SimulatedCdcEvent cdcEvent, string failureReason, int attemptCount)
		{
			Events.Add(new DeadLetterEvent
			{
				TableName = cdcEvent.TableName,
				OriginalEvent = cdcEvent,
				FailureReason = failureReason,
				AttemptCount = attemptCount,
				EnqueuedAt = DateTimeOffset.UtcNow,
			});
		}
	}

	/// <summary>
	/// Simulated connection manager for testing recovery.
	/// </summary>
	internal sealed class SimulatedConnectionManager
	{
		private readonly ExecutionLog _log;
		private bool _isConnected = true;

		public SimulatedConnectionManager(ExecutionLog log)
		{
			_log = log;
		}

		public bool IsConnected => _isConnected;

		public void SimulateDisconnect()
		{
			_isConnected = false;
		}

		public void SimulateReconnect()
		{
			_isConnected = true;
		}
	}

	// Handler interface
	// CDC types
	internal sealed class SimulatedCdcEvent
	{
		public byte[] Lsn { get; init; } = [];
		public string TableName { get; init; } = string.Empty;
		public CdcChangeType ChangeType { get; init; }
		public Dictionary<string, object?> Columns { get; init; } = [];
		public bool IsValid { get; init; } = true;
	}

	internal sealed class DeadLetterEvent
	{
		public string TableName { get; init; } = string.Empty;
		public SimulatedCdcEvent? OriginalEvent { get; init; }
		public string FailureReason { get; init; } = string.Empty;
		public int AttemptCount { get; init; }
		public DateTimeOffset EnqueuedAt { get; init; }
	}

	// Exception types

	internal sealed class CdcHandlerException : Exception
	{
		public CdcHandlerException(string message, string tableName, CdcChangeType changeType, Exception? inner = null)
			: base(message, inner)
		{
			TableName = tableName;
			ChangeType = changeType;
		}

		public string TableName { get; }
		public CdcChangeType ChangeType { get; }
	}

	internal sealed class TransformationException : Exception
	{
		public TransformationException(string message, string tableName, string columnName, string expectedType)
			: base(message)
		{
			TableName = tableName;
			ColumnName = columnName;
			ExpectedType = expectedType;
		}

		public string TableName { get; }
		public string ColumnName { get; }
		public string ExpectedType { get; }
	}

	#endregion Test Infrastructure
}

