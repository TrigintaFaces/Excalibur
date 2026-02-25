// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Saga.Abstractions;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Saga;

/// <summary>
/// Fluent builder for configuring advanced saga orchestration.
/// </summary>
public sealed class AdvancedSagaBuilder
{
	private readonly IServiceCollection _services;
	private readonly AdvancedSagaOptions _options;

	/// <summary>
	/// Initializes a new instance of the <see cref="AdvancedSagaBuilder"/> class.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="options">The saga options.</param>
	public AdvancedSagaBuilder(IServiceCollection services, AdvancedSagaOptions options)
	{
		_services = services ?? throw new ArgumentNullException(nameof(services));
		_options = options ?? throw new ArgumentNullException(nameof(options));
	}

	/// <summary>
	/// Gets the service collection for dependency registration.
	/// </summary>
	/// <value>The service collection.</value>
	public IServiceCollection Services => _services;

	/// <summary>
	/// Configures the saga state store implementation.
	/// </summary>
	/// <typeparam name="TStore">The state store implementation type.</typeparam>
	/// <returns>The builder for fluent configuration.</returns>
	public AdvancedSagaBuilder UseStateStore<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
	TStore>()
		where TStore : class, ISagaStateStore
	{
		_ = _services.AddSingleton<ISagaStateStore, TStore>();
		return this;
	}

	/// <summary>
	/// Configures the saga retry policy implementation.
	/// </summary>
	/// <typeparam name="TPolicy">The retry policy implementation type.</typeparam>
	/// <returns>The builder for fluent configuration.</returns>
	public AdvancedSagaBuilder UseRetryPolicy<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
	TPolicy>()
		where TPolicy : class, ISagaRetryPolicy
	{
		_ = _services.AddSingleton<ISagaRetryPolicy, TPolicy>();
		return this;
	}

	/// <summary>
	/// Configures the default timeout for saga execution.
	/// </summary>
	/// <param name="timeout">The timeout duration.</param>
	/// <returns>The builder for fluent configuration.</returns>
	public AdvancedSagaBuilder WithDefaultTimeout(TimeSpan timeout)
	{
		_options.DefaultTimeout = timeout;
		return this;
	}

	/// <summary>
	/// Configures the default timeout for individual saga steps.
	/// </summary>
	/// <param name="timeout">The timeout duration.</param>
	/// <returns>The builder for fluent configuration.</returns>
	public AdvancedSagaBuilder WithStepTimeout(TimeSpan timeout)
	{
		_options.DefaultStepTimeout = timeout;
		return this;
	}

	/// <summary>
	/// Configures the maximum retry attempts for failed steps.
	/// </summary>
	/// <param name="maxAttempts">The maximum number of retry attempts.</param>
	/// <returns>The builder for fluent configuration.</returns>
	public AdvancedSagaBuilder WithMaxRetries(int maxAttempts)
	{
		_options.MaxRetryAttempts = maxAttempts;
		return this;
	}

	/// <summary>
	/// Configures the maximum degree of parallelism for parallel saga steps.
	/// </summary>
	/// <param name="maxDegree">The maximum degree of parallelism.</param>
	/// <returns>The builder for fluent configuration.</returns>
	public AdvancedSagaBuilder WithMaxParallelism(int maxDegree)
	{
		_options.MaxDegreeOfParallelism = maxDegree;
		return this;
	}

	/// <summary>
	/// Enables or disables automatic compensation on failure.
	/// </summary>
	/// <param name="enable">Whether to enable automatic compensation.</param>
	/// <returns>The builder for fluent configuration.</returns>
	public AdvancedSagaBuilder WithAutoCompensation(bool enable = true)
	{
		_options.EnableAutoCompensation = enable;
		return this;
	}

	/// <summary>
	/// Enables or disables saga state persistence.
	/// </summary>
	/// <param name="enable">Whether to enable state persistence.</param>
	/// <returns>The builder for fluent configuration.</returns>
	public AdvancedSagaBuilder WithStatePersistence(bool enable = true)
	{
		_options.EnableStatePersistence = enable;
		return this;
	}

	/// <summary>
	/// Enables or disables saga metrics collection.
	/// </summary>
	/// <param name="enable">Whether to enable metrics collection.</param>
	/// <returns>The builder for fluent configuration.</returns>
	public AdvancedSagaBuilder WithMetrics(bool enable = true)
	{
		_options.EnableMetrics = enable;
		return this;
	}

	/// <summary>
	/// Configures the retention period for completed saga states.
	/// </summary>
	/// <param name="retention">The retention period.</param>
	/// <returns>The builder for fluent configuration.</returns>
	public AdvancedSagaBuilder WithCompletedSagaRetention(TimeSpan retention)
	{
		_options.CompletedSagaRetention = retention;
		return this;
	}
}
