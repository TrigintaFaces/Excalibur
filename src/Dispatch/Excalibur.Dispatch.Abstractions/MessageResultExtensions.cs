// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Extension methods for functional composition on <see cref="IMessageResult{T}"/>.
/// </summary>
/// <remarks>
/// <para>
/// These extensions enable railway-oriented programming patterns for message results,
/// allowing cleaner code with Map, Bind, and Match patterns instead of verbose null checking.
/// </para>
/// <para>
/// Example usage:
/// <code>
/// var result = await dispatcher
///     .DispatchAsync&lt;GetOrderAction, Order&gt;(action, ct)
///     .Map(order => new OrderDto(order))
///     .Match(
///         onSuccess: dto => Ok(dto),
///         onFailure: problem => Problem(problem));
/// </code>
/// </para>
/// </remarks>
public static class MessageResultExtensions
{
	/// <summary>
	/// Transforms the success value using the specified mapping function.
	/// </summary>
	/// <typeparam name="TIn"> The type of the input value. </typeparam>
	/// <typeparam name="TOut"> The type of the output value. </typeparam>
	/// <param name="result"> The message result to transform. </param>
	/// <param name="mapper"> The function to transform the success value. </param>
	/// <returns>
	/// A new <see cref="IMessageResult{T}"/> with the transformed value if successful;
	/// otherwise, a failed result with the original <see cref="IMessageResult.ProblemDetails"/>.
	/// </returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="result"/> or <paramref name="mapper"/> is null.
	/// </exception>
	public static IMessageResult<TOut> Map<TIn, TOut>(
		this IMessageResult<TIn> result,
		Func<TIn, TOut> mapper)
	{
		ArgumentNullException.ThrowIfNull(result);
		ArgumentNullException.ThrowIfNull(mapper);

		if (!result.Succeeded || result.ReturnValue is null)
		{
			return MessageResult.Failed<TOut>(result.ErrorMessage, result.ProblemDetails);
		}

		return MessageResult.Success(mapper(result.ReturnValue));
	}

	/// <summary>
	/// Transforms the success value using an async mapping function.
	/// </summary>
	/// <typeparam name="TIn"> The type of the input value. </typeparam>
	/// <typeparam name="TOut"> The type of the output value. </typeparam>
	/// <param name="result"> The message result to transform. </param>
	/// <param name="mapper"> The async function to transform the success value. </param>
	/// <returns>
	/// A task representing a new <see cref="IMessageResult{T}"/> with the transformed value if successful;
	/// otherwise, a failed result with the original <see cref="IMessageResult.ProblemDetails"/>.
	/// </returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="result"/> or <paramref name="mapper"/> is null.
	/// </exception>
	public static async Task<IMessageResult<TOut>> MapAsync<TIn, TOut>(
		this IMessageResult<TIn> result,
		Func<TIn, Task<TOut>> mapper)
	{
		ArgumentNullException.ThrowIfNull(result);
		ArgumentNullException.ThrowIfNull(mapper);

		if (!result.Succeeded || result.ReturnValue is null)
		{
			return MessageResult.Failed<TOut>(result.ErrorMessage, result.ProblemDetails);
		}

		var mapped = await mapper(result.ReturnValue).ConfigureAwait(false);
		return MessageResult.Success(mapped);
	}

	/// <summary>
	/// Transforms the success value of an async result using the specified mapping function.
	/// </summary>
	/// <typeparam name="TIn"> The type of the input value. </typeparam>
	/// <typeparam name="TOut"> The type of the output value. </typeparam>
	/// <param name="resultTask"> The task representing the message result to transform. </param>
	/// <param name="mapper"> The function to transform the success value. </param>
	/// <returns>
	/// A task representing a new <see cref="IMessageResult{T}"/> with the transformed value if successful;
	/// otherwise, a failed result with the original <see cref="IMessageResult.ProblemDetails"/>.
	/// </returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="resultTask"/> or <paramref name="mapper"/> is null.
	/// </exception>
	public static async Task<IMessageResult<TOut>> Map<TIn, TOut>(
		this Task<IMessageResult<TIn>> resultTask,
		Func<TIn, TOut> mapper)
	{
		ArgumentNullException.ThrowIfNull(resultTask);
		ArgumentNullException.ThrowIfNull(mapper);

		var result = await resultTask.ConfigureAwait(false);
		return result.Map(mapper);
	}

	/// <summary>
	/// Chains result operations, allowing composition of result-returning functions.
	/// </summary>
	/// <typeparam name="TIn"> The type of the input value. </typeparam>
	/// <typeparam name="TOut"> The type of the output value. </typeparam>
	/// <param name="result"> The message result to chain. </param>
	/// <param name="binder"> The function that returns a new result based on the success value. </param>
	/// <returns>
	/// The result of the <paramref name="binder"/> function if the input was successful;
	/// otherwise, a failed result with the original <see cref="IMessageResult.ProblemDetails"/>.
	/// </returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="result"/> or <paramref name="binder"/> is null.
	/// </exception>
	public static IMessageResult<TOut> Bind<TIn, TOut>(
		this IMessageResult<TIn> result,
		Func<TIn, IMessageResult<TOut>> binder)
	{
		ArgumentNullException.ThrowIfNull(result);
		ArgumentNullException.ThrowIfNull(binder);

		if (!result.Succeeded || result.ReturnValue is null)
		{
			return MessageResult.Failed<TOut>(result.ErrorMessage, result.ProblemDetails);
		}

		return binder(result.ReturnValue);
	}

	/// <summary>
	/// Chains result operations using an async binder function.
	/// </summary>
	/// <typeparam name="TIn"> The type of the input value. </typeparam>
	/// <typeparam name="TOut"> The type of the output value. </typeparam>
	/// <param name="result"> The message result to chain. </param>
	/// <param name="binder"> The async function that returns a new result based on the success value. </param>
	/// <returns>
	/// A task representing the result of the <paramref name="binder"/> function if the input was successful;
	/// otherwise, a failed result with the original <see cref="IMessageResult.ProblemDetails"/>.
	/// </returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="result"/> or <paramref name="binder"/> is null.
	/// </exception>
	public static async Task<IMessageResult<TOut>> BindAsync<TIn, TOut>(
		this IMessageResult<TIn> result,
		Func<TIn, Task<IMessageResult<TOut>>> binder)
	{
		ArgumentNullException.ThrowIfNull(result);
		ArgumentNullException.ThrowIfNull(binder);

		if (!result.Succeeded || result.ReturnValue is null)
		{
			return MessageResult.Failed<TOut>(result.ErrorMessage, result.ProblemDetails);
		}

		return await binder(result.ReturnValue).ConfigureAwait(false);
	}

	/// <summary>
	/// Chains result operations on an async result using a sync binder function.
	/// </summary>
	/// <typeparam name="TIn"> The type of the input value. </typeparam>
	/// <typeparam name="TOut"> The type of the output value. </typeparam>
	/// <param name="resultTask"> The task representing the message result to chain. </param>
	/// <param name="binder"> The function that returns a new result based on the success value. </param>
	/// <returns>
	/// A task representing the result of the <paramref name="binder"/> function if the input was successful;
	/// otherwise, a failed result with the original <see cref="IMessageResult.ProblemDetails"/>.
	/// </returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="resultTask"/> or <paramref name="binder"/> is null.
	/// </exception>
	public static async Task<IMessageResult<TOut>> Bind<TIn, TOut>(
		this Task<IMessageResult<TIn>> resultTask,
		Func<TIn, IMessageResult<TOut>> binder)
	{
		ArgumentNullException.ThrowIfNull(resultTask);
		ArgumentNullException.ThrowIfNull(binder);

		var result = await resultTask.ConfigureAwait(false);
		return result.Bind(binder);
	}

	/// <summary>
	/// Pattern matches on the result, executing the appropriate handler.
	/// </summary>
	/// <typeparam name="TIn"> The type of the input value. </typeparam>
	/// <typeparam name="TOut"> The type of the output value. </typeparam>
	/// <param name="result"> The message result to match on. </param>
	/// <param name="onSuccess"> The function to execute when the result is successful. </param>
	/// <param name="onFailure"> The function to execute when the result is a failure. </param>
	/// <returns> The result of either <paramref name="onSuccess"/> or <paramref name="onFailure"/>. </returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="result"/>, <paramref name="onSuccess"/>, or <paramref name="onFailure"/> is null.
	/// </exception>
	public static TOut Match<TIn, TOut>(
		this IMessageResult<TIn> result,
		Func<TIn, TOut> onSuccess,
		Func<IMessageProblemDetails?, TOut> onFailure)
	{
		ArgumentNullException.ThrowIfNull(result);
		ArgumentNullException.ThrowIfNull(onSuccess);
		ArgumentNullException.ThrowIfNull(onFailure);

		if (result.Succeeded && result.ReturnValue is not null)
		{
			return onSuccess(result.ReturnValue);
		}

		return onFailure(result.ProblemDetails);
	}

	/// <summary>
	/// Pattern matches on an async result, executing the appropriate handler.
	/// </summary>
	/// <typeparam name="TIn"> The type of the input value. </typeparam>
	/// <typeparam name="TOut"> The type of the output value. </typeparam>
	/// <param name="resultTask"> The task representing the message result to match on. </param>
	/// <param name="onSuccess"> The function to execute when the result is successful. </param>
	/// <param name="onFailure"> The function to execute when the result is a failure. </param>
	/// <returns>
	/// A task representing the result of either <paramref name="onSuccess"/> or <paramref name="onFailure"/>.
	/// </returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="resultTask"/>, <paramref name="onSuccess"/>, or <paramref name="onFailure"/> is null.
	/// </exception>
	public static async Task<TOut> Match<TIn, TOut>(
		this Task<IMessageResult<TIn>> resultTask,
		Func<TIn, TOut> onSuccess,
		Func<IMessageProblemDetails?, TOut> onFailure)
	{
		ArgumentNullException.ThrowIfNull(resultTask);
		ArgumentNullException.ThrowIfNull(onSuccess);
		ArgumentNullException.ThrowIfNull(onFailure);

		var result = await resultTask.ConfigureAwait(false);
		return result.Match(onSuccess, onFailure);
	}

	/// <summary>
	/// Pattern matches on the result, executing the appropriate async handler.
	/// </summary>
	/// <typeparam name="TIn"> The type of the input value. </typeparam>
	/// <typeparam name="TOut"> The type of the output value. </typeparam>
	/// <param name="result"> The message result to match on. </param>
	/// <param name="onSuccess"> The async function to execute when the result is successful. </param>
	/// <param name="onFailure"> The async function to execute when the result is a failure. </param>
	/// <returns>
	/// A task representing the result of either <paramref name="onSuccess"/> or <paramref name="onFailure"/>.
	/// </returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="result"/>, <paramref name="onSuccess"/>, or <paramref name="onFailure"/> is null.
	/// </exception>
	public static async Task<TOut> MatchAsync<TIn, TOut>(
		this IMessageResult<TIn> result,
		Func<TIn, Task<TOut>> onSuccess,
		Func<IMessageProblemDetails?, Task<TOut>> onFailure)
	{
		ArgumentNullException.ThrowIfNull(result);
		ArgumentNullException.ThrowIfNull(onSuccess);
		ArgumentNullException.ThrowIfNull(onFailure);

		if (result.Succeeded && result.ReturnValue is not null)
		{
			return await onSuccess(result.ReturnValue).ConfigureAwait(false);
		}

		return await onFailure(result.ProblemDetails).ConfigureAwait(false);
	}

	// EHE-009: Tap, GetValueOrDefault, GetValueOrThrow

	/// <summary>
	/// Executes a side effect on success without modifying the result.
	/// </summary>
	/// <typeparam name="T"> The type of the value. </typeparam>
	/// <param name="result"> The message result. </param>
	/// <param name="action"> The action to execute on the success value. </param>
	/// <returns> The original result unchanged. </returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="result"/> or <paramref name="action"/> is null.
	/// </exception>
	/// <remarks>
	/// This method is useful for logging, metrics, or other side effects that should
	/// not alter the result chain.
	/// </remarks>
	public static IMessageResult<T> Tap<T>(
		this IMessageResult<T> result,
		Action<T> action)
	{
		ArgumentNullException.ThrowIfNull(result);
		ArgumentNullException.ThrowIfNull(action);

		if (result.Succeeded && result.ReturnValue is not null)
		{
			action(result.ReturnValue);
		}

		return result;
	}

	/// <summary>
	/// Executes an async side effect on success without modifying the result.
	/// </summary>
	/// <typeparam name="T"> The type of the value. </typeparam>
	/// <param name="result"> The message result. </param>
	/// <param name="action"> The async action to execute on the success value. </param>
	/// <returns> A task representing the original result unchanged. </returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="result"/> or <paramref name="action"/> is null.
	/// </exception>
	public static async Task<IMessageResult<T>> TapAsync<T>(
		this IMessageResult<T> result,
		Func<T, Task> action)
	{
		ArgumentNullException.ThrowIfNull(result);
		ArgumentNullException.ThrowIfNull(action);

		if (result.Succeeded && result.ReturnValue is not null)
		{
			await action(result.ReturnValue).ConfigureAwait(false);
		}

		return result;
	}

	/// <summary>
	/// Executes a side effect on an async result without modifying it.
	/// </summary>
	/// <typeparam name="T"> The type of the value. </typeparam>
	/// <param name="resultTask"> The task representing the message result. </param>
	/// <param name="action"> The action to execute on the success value. </param>
	/// <returns> A task representing the original result unchanged. </returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="resultTask"/> or <paramref name="action"/> is null.
	/// </exception>
	public static async Task<IMessageResult<T>> Tap<T>(
		this Task<IMessageResult<T>> resultTask,
		Action<T> action)
	{
		ArgumentNullException.ThrowIfNull(resultTask);
		ArgumentNullException.ThrowIfNull(action);

		var result = await resultTask.ConfigureAwait(false);
		return result.Tap(action);
	}

	/// <summary>
	/// Returns the success value or the specified default value.
	/// </summary>
	/// <typeparam name="T"> The type of the value. </typeparam>
	/// <param name="result"> The message result. </param>
	/// <param name="defaultValue"> The default value to return if the result is not successful. </param>
	/// <returns>
	/// The <see cref="IMessageResult{T}.ReturnValue"/> if successful and non-null;
	/// otherwise, <paramref name="defaultValue"/>.
	/// </returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="result"/> is null.
	/// </exception>
	public static T? GetValueOrDefault<T>(
		this IMessageResult<T> result,
		T? defaultValue = default)
	{
		ArgumentNullException.ThrowIfNull(result);

		return result.Succeeded && result.ReturnValue is not null
			? result.ReturnValue
			: defaultValue;
	}

	/// <summary>
	/// Returns the success value or throws an exception with ProblemDetails information.
	/// </summary>
	/// <typeparam name="T"> The type of the value. </typeparam>
	/// <param name="result"> The message result. </param>
	/// <returns> The <see cref="IMessageResult{T}.ReturnValue"/>. </returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="result"/> is null.
	/// </exception>
	/// <exception cref="InvalidOperationException">
	/// Thrown when the result is not successful or the return value is null.
	/// The exception message contains details from the <see cref="IMessageResult.ProblemDetails"/>.
	/// </exception>
	/// <remarks>
	/// <para>
	/// Use this method when you need the value and want to fail fast with a clear error.
	/// The thrown exception includes the <see cref="IMessageProblemDetails"/> in its Data dictionary.
	/// </para>
	/// <para>
	/// Example usage:
	/// <code>
	/// var order = result.GetValueOrThrow(); // Throws if not successful
	/// </code>
	/// </para>
	/// </remarks>
	public static T GetValueOrThrow<T>(this IMessageResult<T> result)
	{
		ArgumentNullException.ThrowIfNull(result);

		if (result.Succeeded && result.ReturnValue is not null)
		{
			return result.ReturnValue;
		}

		var message = result.ProblemDetails?.Detail
			?? result.ErrorMessage
			?? "Result did not contain a value.";

		var exception = new InvalidOperationException(message);
		if (result.ProblemDetails is not null)
		{
			exception.Data["ProblemDetails"] = result.ProblemDetails;
		}

		throw exception;
	}

	/// <summary>
	/// Returns the success value from an async result or throws an exception.
	/// </summary>
	/// <typeparam name="T"> The type of the value. </typeparam>
	/// <param name="resultTask"> The task representing the message result. </param>
	/// <returns> A task representing the <see cref="IMessageResult{T}.ReturnValue"/>. </returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="resultTask"/> is null.
	/// </exception>
	/// <exception cref="InvalidOperationException">
	/// Thrown when the result is not successful or the return value is null.
	/// </exception>
	public static async Task<T> GetValueOrThrow<T>(this Task<IMessageResult<T>> resultTask)
	{
		ArgumentNullException.ThrowIfNull(resultTask);

		var result = await resultTask.ConfigureAwait(false);
		return result.GetValueOrThrow();
	}
}
