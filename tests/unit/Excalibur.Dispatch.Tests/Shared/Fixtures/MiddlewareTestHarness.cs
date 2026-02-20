// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Tests.TestFakes;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Tests.Shared.Fixtures;

/// <summary>
/// Base test harness for middleware testing providing common setup and utilities.
/// </summary>
/// <remarks>
/// Sprint 412 - Infrastructure task T412.5.
/// Provides reusable infrastructure for testing middleware components including:
/// - Pre-configured pipeline stages
/// - Mock context factory
/// - Assertion helpers for middleware behavior
/// </remarks>
public abstract class MiddlewareTestHarness
{
	/// <summary>
	/// Gets the logger factory for creating typed loggers.
	/// </summary>
	protected ILoggerFactory LoggerFactory { get; } = NullLoggerFactory.Instance;

	/// <summary>
	/// Creates a new test message context with default values.
	/// </summary>
	/// <param name="messageId">Optional message ID. If not provided, a new GUID is generated.</param>
	/// <returns>A configured <see cref="FakeMessageContext"/>.</returns>
	protected static FakeMessageContext CreateTestContext(string? messageId = null)
	{
		return new FakeMessageContext
		{
			MessageId = messageId ?? Guid.NewGuid().ToString(),
			CorrelationId = Guid.NewGuid().ToString(),
			CausationId = Guid.NewGuid().ToString(),
			Source = "test-source",
		};
	}

	/// <summary>
	/// Creates a new test dispatch message.
	/// </summary>
	/// <param name="messageType">Optional message type. Defaults to "TestMessage".</param>
	/// <param name="kind">Optional message kind. Defaults to <see cref="MessageKinds.Event"/>.</param>
	/// <returns>A configured <see cref="FakeDispatchMessage"/>.</returns>
	protected static FakeDispatchMessage CreateTestMessage(string? messageType = null, MessageKinds kind = MessageKinds.Event)
	{
		return new FakeDispatchMessage
		{
			Type = messageType ?? "TestMessage",
			MessageType = messageType ?? "TestMessage",
			Kind = kind,
		};
	}

	/// <summary>
	/// Creates a typed logger for middleware testing.
	/// </summary>
	/// <typeparam name="T">The type for the logger.</typeparam>
	/// <returns>A logger instance.</returns>
	protected ILogger<T> CreateLogger<T>()
	{
		return LoggerFactory.CreateLogger<T>();
	}

	/// <summary>
	/// Creates a pipeline delegate that always succeeds.
	/// </summary>
	/// <returns>A delegate that returns a successful result.</returns>
	protected static DispatchRequestDelegate CreateSuccessDelegate()
	{
		return (_, _, _) => new ValueTask<IMessageResult>(Excalibur.Dispatch.Abstractions.MessageResult.Success());
	}

	/// <summary>
	/// Creates a pipeline delegate that always fails with the specified problem details.
	/// </summary>
	/// <param name="type">The problem type.</param>
	/// <param name="title">The problem title.</param>
	/// <param name="errorCode">The error code.</param>
	/// <returns>A delegate that returns a failed result.</returns>
	protected static DispatchRequestDelegate CreateFailureDelegate(
		string type = "TestError",
		string title = "Test Failure",
		int errorCode = 500)
	{
		return (_, _, _) => new ValueTask<IMessageResult>(Excalibur.Dispatch.Abstractions.MessageResult.Failed(
			new MessageProblemDetails
			{
				Type = type,
				Title = title,
				ErrorCode = errorCode,
				Status = errorCode,
			}));
	}

	/// <summary>
	/// Creates a pipeline delegate that throws the specified exception.
	/// </summary>
	/// <typeparam name="TException">The type of exception to throw.</typeparam>
	/// <param name="exceptionFactory">Factory to create the exception.</param>
	/// <returns>A delegate that throws the exception.</returns>
	protected static DispatchRequestDelegate CreateThrowingDelegate<TException>(Func<TException> exceptionFactory)
		where TException : Exception
	{
		return (_, _, _) => throw exceptionFactory();
	}

	/// <summary>
	/// Creates a pipeline delegate that tracks invocation count and can be configured to fail/succeed.
	/// </summary>
	/// <param name="failUntilAttempt">Number of attempts that should fail before succeeding. 0 means always succeed.</param>
	/// <param name="exceptionToThrow">Optional exception to throw instead of returning a failed result.</param>
	/// <returns>A delegate with invocation tracking and a func to get the attempt count.</returns>
	protected static (DispatchRequestDelegate Delegate, Func<int> GetAttemptCount) CreateTrackingDelegate(
		int failUntilAttempt = 0,
		Exception? exceptionToThrow = null)
	{
		var attemptCount = 0;

		ValueTask<IMessageResult> Delegate(IDispatchMessage _, IMessageContext __, CancellationToken ___)
		{
			attemptCount++;
			if (attemptCount <= failUntilAttempt)
			{
				if (exceptionToThrow != null)
				{
					throw exceptionToThrow;
				}

				return new ValueTask<IMessageResult>(Excalibur.Dispatch.Abstractions.MessageResult.Failed(
					new MessageProblemDetails
					{
						Type = "TestError",
						Title = $"Test failure on attempt {attemptCount}",
						ErrorCode = 500,
						Status = 500,
					}));
			}

			return new ValueTask<IMessageResult>(Excalibur.Dispatch.Abstractions.MessageResult.Success());
		}

		return (Delegate, () => attemptCount);
	}

	/// <summary>
	/// Creates a pipeline delegate that captures timing information between invocations.
	/// </summary>
	/// <param name="failUntilAttempt">Number of attempts that should fail before succeeding.</param>
	/// <returns>A delegate with timing capture and a func to get the timestamps.</returns>
	protected static (DispatchRequestDelegate Delegate, Func<IReadOnlyList<DateTime>> GetTimestamps) CreateTimingDelegate(
		int failUntilAttempt = 0)
	{
		var timestamps = new List<DateTime>();
		var attemptCount = 0;

		ValueTask<IMessageResult> Delegate(IDispatchMessage _, IMessageContext __, CancellationToken ___)
		{
			timestamps.Add(DateTime.UtcNow);
			attemptCount++;

			if (attemptCount <= failUntilAttempt)
			{
				return new ValueTask<IMessageResult>(Excalibur.Dispatch.Abstractions.MessageResult.Failed(
					new MessageProblemDetails
					{
						Type = "TestError",
						Title = $"Test failure on attempt {attemptCount}",
						ErrorCode = 500,
						Status = 500,
					}));
			}

			return new ValueTask<IMessageResult>(Excalibur.Dispatch.Abstractions.MessageResult.Success());
		}

		return (Delegate, () => timestamps.AsReadOnly());
	}

	/// <summary>
	/// Creates a pipeline delegate that tracks exceptions thrown by type.
	/// </summary>
	/// <param name="exceptionsToThrow">Sequence of exceptions to throw on each invocation.</param>
	/// <returns>A delegate that throws the specified exceptions in order.</returns>
	protected static (DispatchRequestDelegate Delegate, Func<int> GetAttemptCount) CreateSequentialExceptionDelegate(
		params Exception[] exceptionsToThrow)
	{
		var attemptCount = 0;
		var exceptions = exceptionsToThrow.ToList();

		ValueTask<IMessageResult> Delegate(IDispatchMessage _, IMessageContext __, CancellationToken ___)
		{
			attemptCount++;
			if (attemptCount <= exceptions.Count)
			{
				throw exceptions[attemptCount - 1];
			}

			return new ValueTask<IMessageResult>(Excalibur.Dispatch.Abstractions.MessageResult.Success());
		}

		return (Delegate, () => attemptCount);
	}
}
