// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.Abstractions;
using Excalibur.Saga.Models;

namespace Excalibur.Saga.Implementation;

/// <summary>
/// Builder for creating conditional saga steps.
/// </summary>
/// <typeparam name="TData"> The type of data for the saga. </typeparam>
/// <remarks> Initializes a new instance of the <see cref="ConditionalStepBuilder{TData}" /> class. </remarks>
/// <param name="name"> The name of the step. </param>
public sealed class ConditionalStepBuilder<TData>(string name)
	where TData : class
{
	private readonly string _name = name ?? throw new ArgumentNullException(nameof(name));
	private Func<ISagaContext<TData>, CancellationToken, Task<bool>>? _condition;
	private ISagaStep<TData>? _thenStep;
	private ISagaStep<TData>? _elseStep;
	private TimeSpan _timeout = TimeSpan.FromMinutes(5);
	private RetryPolicy? _retryPolicy;

	/// <summary>
	/// Sets the condition for the conditional step.
	/// </summary>
	/// <param name="condition"> The condition evaluation function. </param>
	/// <returns> The builder instance. </returns>
	/// <exception cref="ArgumentNullException">Thrown when the condition is null.</exception>
	public ConditionalStepBuilder<TData> When(Func<ISagaContext<TData>, CancellationToken, Task<bool>> condition)
	{
		_condition = condition ?? throw new ArgumentNullException(nameof(condition));
		return this;
	}

	/// <summary>
	/// Sets the condition for the conditional step using a synchronous predicate.
	/// </summary>
	/// <param name="predicate"> The condition predicate. </param>
	/// <returns> The builder instance. </returns>
	public ConditionalStepBuilder<TData> When(Func<ISagaContext<TData>, bool> predicate)
	{
		ArgumentNullException.ThrowIfNull(predicate);

		_condition = (context, _) => Task.FromResult(predicate(context));
		return this;
	}

	/// <summary>
	/// Sets the step to execute when the condition is true.
	/// </summary>
	/// <param name="step"> The then step. </param>
	/// <returns> The builder instance. </returns>
	/// <exception cref="ArgumentNullException">Thrown when the step is null.</exception>
	public ConditionalStepBuilder<TData> Then(ISagaStep<TData> step)
	{
		_thenStep = step ?? throw new ArgumentNullException(nameof(step));
		return this;
	}

	/// <summary>
	/// Sets the step to execute when the condition is false.
	/// </summary>
	/// <param name="step"> The else step. </param>
	/// <returns> The builder instance. </returns>
	/// <exception cref="ArgumentNullException">Thrown when the step is null.</exception>
	public ConditionalStepBuilder<TData> Else(ISagaStep<TData> step)
	{
		_elseStep = step ?? throw new ArgumentNullException(nameof(step));
		return this;
	}

	/// <summary>
	/// Sets the timeout for the conditional step.
	/// </summary>
	/// <param name="timeout"> The timeout duration. </param>
	/// <returns> The builder instance. </returns>
	public ConditionalStepBuilder<TData> WithTimeout(TimeSpan timeout)
	{
		_timeout = timeout;
		return this;
	}

	/// <summary>
	/// Sets the retry policy for the conditional step.
	/// </summary>
	/// <param name="retryPolicy"> The retry policy. </param>
	/// <returns> The builder instance. </returns>
	public ConditionalStepBuilder<TData> WithRetryPolicy(RetryPolicy retryPolicy)
	{
		_retryPolicy = retryPolicy;
		return this;
	}

	/// <summary>
	/// Builds the conditional saga step.
	/// </summary>
	/// <returns> The configured conditional saga step. </returns>
	/// <exception cref="InvalidOperationException">Thrown when the condition has not been specified.</exception>
	public ConditionalSagaStep<TData> Build()
	{
		if (_condition == null)
		{
			throw new InvalidOperationException(
				Resources.ConditionalStepBuilder_ConditionMustBeSpecified);
		}

		return new ConditionalSagaStep<TData>(_name, _condition, _thenStep, _elseStep) { Timeout = _timeout, RetryPolicy = _retryPolicy, };
	}
}
