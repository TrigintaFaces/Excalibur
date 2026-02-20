// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Configuration;

/// <summary>
/// Base class for exception-to-problem-details mappings.
/// </summary>
internal abstract class ExceptionMapping
{
	/// <summary>
	/// Gets the exception type this mapping handles.
	/// </summary>
	public abstract Type ExceptionType { get; }

	/// <summary>
	/// Gets a value indicating whether this mapping requires async execution.
	/// </summary>
	public abstract bool IsAsync { get; }

	/// <summary>
	/// Determines if this mapping can handle the specified exception.
	/// </summary>
	/// <param name="exception"> The exception to check. </param>
	/// <returns> <see langword="true"/> if this mapping can handle the exception; otherwise, <see langword="false"/>. </returns>
	public abstract bool CanHandle(Exception exception);

	/// <summary>
	/// Synchronously maps the exception to problem details.
	/// </summary>
	/// <param name="exception"> The exception to map. </param>
	/// <returns> The problem details. </returns>
	public abstract IMessageProblemDetails Map(Exception exception);

	/// <summary>
	/// Asynchronously maps the exception to problem details.
	/// </summary>
	/// <param name="exception"> The exception to map. </param>
	/// <param name="cancellationToken"> A token to cancel the operation. </param>
	/// <returns> A task that resolves to the problem details. </returns>
	public abstract Task<IMessageProblemDetails> MapAsync(Exception exception, CancellationToken cancellationToken);
}

/// <summary>
/// Type-specific exception mapping with synchronous mapper.
/// </summary>
/// <typeparam name="TException"> The type of exception to map. </typeparam>
internal sealed class TypedExceptionMapping<TException> : ExceptionMapping
	where TException : Exception
{
	private readonly Func<TException, IMessageProblemDetails> _mapper;

	/// <summary>
	/// Initializes a new instance of the <see cref="TypedExceptionMapping{TException}"/> class.
	/// </summary>
	/// <param name="mapper"> The function to map exceptions to problem details. </param>
	public TypedExceptionMapping(Func<TException, IMessageProblemDetails> mapper)
	{
		_mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
	}

	/// <inheritdoc />
	public override Type ExceptionType => typeof(TException);

	/// <inheritdoc />
	public override bool IsAsync => false;

	/// <inheritdoc />
	public override bool CanHandle(Exception exception) => exception is TException;

	/// <inheritdoc />
	public override IMessageProblemDetails Map(Exception exception)
	{
		if (exception is not TException typed)
		{
			throw new ArgumentException($"Expected exception of type {typeof(TException).Name}, but got {exception.GetType().Name}.", nameof(exception));
		}

		return _mapper(typed);
	}

	/// <inheritdoc />
	public override Task<IMessageProblemDetails> MapAsync(Exception exception, CancellationToken cancellationToken)
	{
		return Task.FromResult(Map(exception));
	}
}

/// <summary>
/// Type-specific exception mapping with asynchronous mapper.
/// </summary>
/// <typeparam name="TException"> The type of exception to map. </typeparam>
internal sealed class AsyncTypedExceptionMapping<TException> : ExceptionMapping
	where TException : Exception
{
	private readonly Func<TException, CancellationToken, Task<IMessageProblemDetails>> _mapper;

	/// <summary>
	/// Initializes a new instance of the <see cref="AsyncTypedExceptionMapping{TException}"/> class.
	/// </summary>
	/// <param name="mapper"> The async function to map exceptions to problem details. </param>
	public AsyncTypedExceptionMapping(Func<TException, CancellationToken, Task<IMessageProblemDetails>> mapper)
	{
		_mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
	}

	/// <inheritdoc />
	public override Type ExceptionType => typeof(TException);

	/// <inheritdoc />
	public override bool IsAsync => true;

	/// <inheritdoc />
	public override bool CanHandle(Exception exception) => exception is TException;

	/// <inheritdoc />
	[SuppressMessage("AsyncUsage", "VSTHRD002:Avoid problematic synchronous waits",
		Justification = "ExceptionMapping.Map() is synchronous by base class contract. Callers should prefer MapAsync when possible.")]
	public override IMessageProblemDetails Map(Exception exception)
	{
		// For async mappings, we need to block - but this should ideally be avoided
		// by using MapAsync in the middleware
		return MapAsync(exception, CancellationToken.None).GetAwaiter().GetResult();
	}

	/// <inheritdoc />
	public override async Task<IMessageProblemDetails> MapAsync(Exception exception, CancellationToken cancellationToken)
	{
		if (exception is not TException typed)
		{
			throw new ArgumentException($"Expected exception of type {typeof(TException).Name}, but got {exception.GetType().Name}.", nameof(exception));
		}

		return await _mapper(typed, cancellationToken).ConfigureAwait(false);
	}
}

/// <summary>
/// Conditional exception mapping that applies only when a predicate matches.
/// </summary>
/// <typeparam name="TException"> The type of exception to map. </typeparam>
internal sealed class ConditionalExceptionMapping<TException> : ExceptionMapping
	where TException : Exception
{
	private readonly Func<TException, bool> _predicate;
	private readonly Func<TException, IMessageProblemDetails> _mapper;

	/// <summary>
	/// Initializes a new instance of the <see cref="ConditionalExceptionMapping{TException}"/> class.
	/// </summary>
	/// <param name="predicate"> The predicate that determines if this mapping applies. </param>
	/// <param name="mapper"> The function to map exceptions to problem details. </param>
	public ConditionalExceptionMapping(Func<TException, bool> predicate, Func<TException, IMessageProblemDetails> mapper)
	{
		_predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
		_mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
	}

	/// <inheritdoc />
	public override Type ExceptionType => typeof(TException);

	/// <inheritdoc />
	public override bool IsAsync => false;

	/// <inheritdoc />
	public override bool CanHandle(Exception exception)
	{
		return exception is TException typed && _predicate(typed);
	}

	/// <inheritdoc />
	public override IMessageProblemDetails Map(Exception exception)
	{
		if (exception is not TException typed)
		{
			throw new ArgumentException($"Expected exception of type {typeof(TException).Name}, but got {exception.GetType().Name}.", nameof(exception));
		}

		return _mapper(typed);
	}

	/// <inheritdoc />
	public override Task<IMessageProblemDetails> MapAsync(Exception exception, CancellationToken cancellationToken)
	{
		return Task.FromResult(Map(exception));
	}
}
