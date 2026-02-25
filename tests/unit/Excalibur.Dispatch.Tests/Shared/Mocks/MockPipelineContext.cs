// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Tests.TestFakes;

using MessageResult = Excalibur.Dispatch.Abstractions.MessageResult;

namespace Excalibur.Dispatch.Tests.Shared.Mocks;

/// <summary>
/// A mock pipeline context that captures middleware invocation details for testing.
/// </summary>
/// <remarks>
/// Sprint 412 - Infrastructure task T412.5.
/// Provides isolated middleware testing with pipeline invocation tracking.
/// </remarks>
public sealed class MockPipelineContext
{
	private readonly List<PipelineInvocation> _invocations = [];
	private int _currentInvocation;
	private Func<int, IMessageResult>? _resultFactory;
	private Func<int, Exception?>? _exceptionFactory;

	/// <summary>
	/// Gets the message being processed.
	/// </summary>
	public IDispatchMessage Message { get; }

	/// <summary>
	/// Gets the message context.
	/// </summary>
	public IMessageContext Context { get; }

	/// <summary>
	/// Gets all recorded pipeline invocations.
	/// </summary>
	public IReadOnlyList<PipelineInvocation> Invocations => _invocations.AsReadOnly();

	/// <summary>
	/// Gets the number of times the pipeline has been invoked.
	/// </summary>
	public int InvocationCount => _invocations.Count;

	/// <summary>
	/// Initializes a new instance of the <see cref="MockPipelineContext"/> class.
	/// </summary>
	/// <param name="message">Optional message to use. Creates a default if not provided.</param>
	/// <param name="context">Optional context to use. Creates a default if not provided.</param>
	public MockPipelineContext(IDispatchMessage? message = null, IMessageContext? context = null)
	{
		Message = message ?? new FakeDispatchMessage();
		Context = context ?? new FakeMessageContext
		{
			MessageId = Guid.NewGuid().ToString(),
			CorrelationId = Guid.NewGuid().ToString(),
		};
	}

	/// <summary>
	/// Configures the pipeline to return specific results based on invocation number.
	/// </summary>
	/// <param name="resultFactory">Factory that takes invocation number (1-based) and returns a result.</param>
	/// <returns>This context for chaining.</returns>
	public MockPipelineContext ReturnsResult(Func<int, IMessageResult> resultFactory)
	{
		_resultFactory = resultFactory;
		return this;
	}

	/// <summary>
	/// Configures the pipeline to always return a successful result.
	/// </summary>
	/// <returns>This context for chaining.</returns>
	public MockPipelineContext ReturnsSuccess()
	{
		_resultFactory = _ => MessageResult.Success();
		return this;
	}

	/// <summary>
	/// Configures the pipeline to always return a failed result.
	/// </summary>
	/// <param name="type">The problem type.</param>
	/// <param name="title">The problem title.</param>
	/// <param name="errorCode">The error code.</param>
	/// <returns>This context for chaining.</returns>
	public MockPipelineContext ReturnsFailure(string type = "TestError", string title = "Test Failure", int errorCode = 500)
	{
		_resultFactory = _ => MessageResult.Failed(new MessageProblemDetails
		{
			Type = type,
			Title = title,
			ErrorCode = errorCode,
			Status = errorCode,
		});
		return this;
	}

	/// <summary>
	/// Configures the pipeline to fail for a number of invocations, then succeed.
	/// </summary>
	/// <param name="failCount">Number of invocations that should fail.</param>
	/// <returns>This context for chaining.</returns>
	public MockPipelineContext FailsThenSucceeds(int failCount)
	{
		_resultFactory = invocation => invocation <= failCount
			? MessageResult.Failed(new MessageProblemDetails
			{
				Type = "TestError",
				Title = $"Test failure on attempt {invocation}",
				ErrorCode = 500,
				Status = 500,
			})
			: MessageResult.Success();
		return this;
	}

	/// <summary>
	/// Configures the pipeline to throw exceptions based on invocation number.
	/// </summary>
	/// <param name="exceptionFactory">Factory that takes invocation number (1-based) and returns an exception or null.</param>
	/// <returns>This context for chaining.</returns>
	public MockPipelineContext ThrowsException(Func<int, Exception?> exceptionFactory)
	{
		_exceptionFactory = exceptionFactory;
		return this;
	}

	/// <summary>
	/// Configures the pipeline to always throw the specified exception.
	/// </summary>
	/// <typeparam name="TException">The type of exception to throw.</typeparam>
	/// <param name="exception">The exception to throw.</param>
	/// <returns>This context for chaining.</returns>
	public MockPipelineContext ThrowsException<TException>(TException exception)
		where TException : Exception
	{
		_exceptionFactory = _ => exception;
		return this;
	}

	/// <summary>
	/// Configures the pipeline to throw exceptions for a number of invocations, then succeed.
	/// </summary>
	/// <param name="exceptionCount">Number of invocations that should throw.</param>
	/// <param name="exceptionFactory">Factory to create each exception.</param>
	/// <returns>This context for chaining.</returns>
	public MockPipelineContext ThrowsThenSucceeds(int exceptionCount, Func<int, Exception> exceptionFactory)
	{
		_exceptionFactory = invocation => invocation <= exceptionCount ? exceptionFactory(invocation) : null;
		_resultFactory = _ => MessageResult.Success();
		return this;
	}

	/// <summary>
	/// Creates the pipeline delegate that can be passed to middleware.
	/// </summary>
	/// <returns>A <see cref="DispatchRequestDelegate"/> for testing.</returns>
	public DispatchRequestDelegate CreateDelegate()
	{
		return (message, context, cancellationToken) =>
		{
			_currentInvocation++;
			var invocation = new PipelineInvocation(
				_currentInvocation,
				message,
				context,
				DateTime.UtcNow,
				cancellationToken);
			_invocations.Add(invocation);

			// Check for exception
			var exception = _exceptionFactory?.Invoke(_currentInvocation);
			if (exception != null)
			{
				invocation.SetException(exception);
				throw exception;
			}

			// Get result
			var result = _resultFactory?.Invoke(_currentInvocation) ?? MessageResult.Success();
			invocation.SetResult(result);
			return new ValueTask<IMessageResult>(result);
		};
	}

	/// <summary>
	/// Resets the invocation tracking state.
	/// </summary>
	public void Reset()
	{
		_invocations.Clear();
		_currentInvocation = 0;
	}

	/// <summary>
	/// Records details of a single pipeline invocation.
	/// </summary>
#pragma warning disable CA1034 // Nested types should not be visible - needed for test API
	public sealed class PipelineInvocation
#pragma warning restore CA1034
	{
		/// <summary>
		/// Gets the invocation number (1-based).
		/// </summary>
		public int InvocationNumber { get; }

		/// <summary>
		/// Gets the message passed to the pipeline.
		/// </summary>
		public IDispatchMessage Message { get; }

		/// <summary>
		/// Gets the context passed to the pipeline.
		/// </summary>
		public IMessageContext Context { get; }

		/// <summary>
		/// Gets the timestamp when the invocation occurred.
		/// </summary>
		public DateTime Timestamp { get; }

		/// <summary>
		/// Gets the cancellation token that was passed.
		/// </summary>
		public CancellationToken CancellationToken { get; }

		/// <summary>
		/// Gets the result returned by this invocation, if any.
		/// </summary>
		public IMessageResult? Result { get; private set; }

		/// <summary>
		/// Gets the exception thrown by this invocation, if any.
		/// </summary>
		public Exception? Exception { get; private set; }

		/// <summary>
		/// Gets whether this invocation threw an exception.
		/// </summary>
		public bool ThrewException => Exception != null;

		/// <summary>
		/// Gets whether this invocation returned a successful result.
		/// </summary>
		public bool WasSuccessful => Result?.IsSuccess == true;

		internal PipelineInvocation(
			int invocationNumber,
			IDispatchMessage message,
			IMessageContext context,
			DateTime timestamp,
			CancellationToken cancellationToken)
		{
			InvocationNumber = invocationNumber;
			Message = message;
			Context = context;
			Timestamp = timestamp;
			CancellationToken = cancellationToken;
		}

		internal void SetResult(IMessageResult result) => Result = result;

		internal void SetException(Exception exception) => Exception = exception;
	}
}
