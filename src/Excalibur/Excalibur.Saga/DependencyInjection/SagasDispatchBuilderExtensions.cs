// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions.Configuration;
using Excalibur.Saga;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring advanced sagas through the Dispatch builder.
/// </summary>
public static class SagasDispatchBuilderExtensions
{
	/// <summary>
	/// Adds advanced saga orchestration support to the Dispatch pipeline.
	/// </summary>
	/// <param name="builder">The Dispatch builder.</param>
	/// <param name="configure">Optional action to configure the saga builder.</param>
	/// <returns>The Dispatch builder for fluent configuration.</returns>
	/// <remarks>
	/// This adds the advanced saga middleware to the pipeline and registers
	/// all required saga services including the orchestrator, state store,
	/// and retry policy.
	/// </remarks>
	/// <example>
	/// <code>
	/// services.AddDispatch()
	///     .WithAdvancedSagas(sagas => sagas
	///         .WithMaxRetries(5)
	///         .WithDefaultTimeout(TimeSpan.FromMinutes(60))
	///         .WithAutoCompensation(true)
	///         .UseStateStore&lt;CustomStateStore&gt;());
	/// </code>
	/// </example>
	public static IDispatchBuilder WithAdvancedSagas(
		this IDispatchBuilder builder,
		Action<AdvancedSagaBuilder>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(builder);

		if (configure != null)
		{
			_ = builder.Services.AddDispatchAdvancedSagas(configure);
		}
		else
		{
			_ = builder.Services.AddDispatchAdvancedSagas();
		}

		// Add middleware to the pipeline
		_ = builder.UseMiddleware<AdvancedSagaMiddleware>();

		return builder;
	}

	/// <summary>
	/// Adds advanced saga orchestration with specific options.
	/// </summary>
	/// <param name="builder">The Dispatch builder.</param>
	/// <param name="configureOptions">Action to configure saga options.</param>
	/// <returns>The Dispatch builder for fluent configuration.</returns>
	/// <example>
	/// <code>
	/// services.AddDispatch()
	///     .WithAdvancedSagas(options =>
	///     {
	///         options.MaxRetryAttempts = 5;
	///         options.DefaultTimeout = TimeSpan.FromMinutes(60);
	///         options.EnableAutoCompensation = true;
	///     });
	/// </code>
	/// </example>
	public static IDispatchBuilder WithAdvancedSagas(
		this IDispatchBuilder builder,
		Action<AdvancedSagaOptions> configureOptions)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configureOptions);

		_ = builder.Services.AddDispatchAdvancedSagas(configureOptions);

		// Add middleware to the pipeline
		_ = builder.UseMiddleware<AdvancedSagaMiddleware>();

		return builder;
	}
}
