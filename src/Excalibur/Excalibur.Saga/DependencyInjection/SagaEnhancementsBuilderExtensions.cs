// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.DependencyInjection;
using Excalibur.Saga.Handlers;
using Excalibur.Saga.Hosting;
using Excalibur.Saga.Idempotency;
using Excalibur.Saga.Reminders;
using Excalibur.Saga.Snapshots;

using Microsoft.Extensions.Configuration;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods on <see cref="ISagaBuilder"/> for configuring Sprint 572 saga enhancements.
/// </summary>
public static class SagaEnhancementsBuilderExtensions
{
	/// <summary>
	/// Adds saga correlation services including the convention-based correlator.
	/// </summary>
	/// <param name="builder">The saga builder.</param>
	/// <returns>The saga builder for chaining.</returns>
	public static ISagaBuilder WithCorrelation(this ISagaBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		_ = builder.Services.AddSagaCorrelation();

		return builder;
	}

	/// <summary>
	/// Adds the default logging-based not-found handler for a saga type.
	/// </summary>
	/// <typeparam name="TSaga">The saga type.</typeparam>
	/// <param name="builder">The saga builder.</param>
	/// <returns>The saga builder for chaining.</returns>
	public static ISagaBuilder WithNotFoundHandler<TSaga>(this ISagaBuilder builder)
		where TSaga : class
	{
		ArgumentNullException.ThrowIfNull(builder);

		_ = builder.Services.AddSagaNotFoundHandler<TSaga>();

		return builder;
	}

	/// <summary>
	/// Adds a custom not-found handler for a saga type.
	/// </summary>
	/// <typeparam name="TSaga">The saga type.</typeparam>
	/// <typeparam name="THandler">The handler implementation type.</typeparam>
	/// <param name="builder">The saga builder.</param>
	/// <returns>The saga builder for chaining.</returns>
	public static ISagaBuilder WithNotFoundHandler<TSaga, THandler>(this ISagaBuilder builder)
		where TSaga : class
		where THandler : class, ISagaNotFoundHandler<TSaga>
	{
		ArgumentNullException.ThrowIfNull(builder);

		_ = builder.Services.AddSagaNotFoundHandler<TSaga, THandler>();

		return builder;
	}

	/// <summary>
	/// Adds saga state inspection services.
	/// </summary>
	/// <param name="builder">The saga builder.</param>
	/// <returns>The saga builder for chaining.</returns>
	public static ISagaBuilder WithStateInspection(this ISagaBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		_ = builder.Services.AddSagaStateInspection();

		return builder;
	}

	/// <summary>
	/// Adds saga reminder services.
	/// </summary>
	/// <param name="builder">The saga builder.</param>
	/// <param name="configure">Optional action to configure reminder options.</param>
	/// <returns>The saga builder for chaining.</returns>
	public static ISagaBuilder WithReminders(
		this ISagaBuilder builder,
		Action<SagaReminderOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(builder);

		if (configure is not null)
		{
			_ = builder.Services.AddSagaReminders(configure);
		}
		else
		{
			_ = builder.Services.AddSagaReminders();
		}

		return builder;
	}

	/// <summary>
	/// Adds saga reminder services using an <see cref="IConfiguration"/> section.
	/// </summary>
	/// <param name="builder">The saga builder.</param>
	/// <param name="configuration">The configuration section to bind to <see cref="SagaReminderOptions"/>.</param>
	/// <returns>The saga builder for chaining.</returns>
	public static ISagaBuilder WithReminders(
		this ISagaBuilder builder,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = builder.Services.AddOptions<SagaReminderOptions>()
			.Bind(configuration)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		_ = builder.Services.AddSagaReminders();

		return builder;
	}

	/// <summary>
	/// Adds saga state snapshot services.
	/// </summary>
	/// <param name="builder">The saga builder.</param>
	/// <param name="configure">Optional action to configure snapshot options.</param>
	/// <returns>The saga builder for chaining.</returns>
	public static ISagaBuilder WithSnapshots(
		this ISagaBuilder builder,
		Action<SagaSnapshotOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(builder);

		if (configure is not null)
		{
			_ = builder.Services.AddSagaSnapshots(configure);
		}
		else
		{
			_ = builder.Services.AddSagaSnapshots();
		}

		return builder;
	}

	/// <summary>
	/// Adds saga state snapshot services using an <see cref="IConfiguration"/> section.
	/// </summary>
	/// <param name="builder">The saga builder.</param>
	/// <param name="configuration">The configuration section to bind to <see cref="SagaSnapshotOptions"/>.</param>
	/// <returns>The saga builder for chaining.</returns>
	public static ISagaBuilder WithSnapshots(
		this ISagaBuilder builder,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = builder.Services.AddOptions<SagaSnapshotOptions>()
			.Bind(configuration)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		_ = builder.Services.AddSagaSnapshots();

		return builder;
	}

	/// <summary>
	/// Adds saga idempotency tracking services.
	/// </summary>
	/// <typeparam name="TProvider">The idempotency provider implementation type.</typeparam>
	/// <param name="builder">The saga builder.</param>
	/// <returns>The saga builder for chaining.</returns>
	public static ISagaBuilder WithIdempotency<TProvider>(this ISagaBuilder builder)
		where TProvider : class, ISagaIdempotencyProvider
	{
		ArgumentNullException.ThrowIfNull(builder);

		_ = builder.Services.AddSagaIdempotency<TProvider>();

		return builder;
	}

	/// <summary>
	/// Adds the saga timeout cleanup background service.
	/// </summary>
	/// <param name="builder">The saga builder.</param>
	/// <param name="configure">Optional action to configure cleanup options.</param>
	/// <returns>The saga builder for chaining.</returns>
	public static ISagaBuilder WithTimeoutCleanup(
		this ISagaBuilder builder,
		Action<SagaTimeoutCleanupOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(builder);

		if (configure is not null)
		{
			_ = builder.Services.AddSagaTimeoutCleanup(configure);
		}
		else
		{
			_ = builder.Services.AddSagaTimeoutCleanup();
		}

		return builder;
	}

	/// <summary>
	/// Adds the saga timeout cleanup background service using an <see cref="IConfiguration"/> section.
	/// </summary>
	/// <param name="builder">The saga builder.</param>
	/// <param name="configuration">The configuration section to bind to <see cref="SagaTimeoutCleanupOptions"/>.</param>
	/// <returns>The saga builder for chaining.</returns>
	public static ISagaBuilder WithTimeoutCleanup(
		this ISagaBuilder builder,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = builder.Services.AddOptions<SagaTimeoutCleanupOptions>()
			.Bind(configuration)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		_ = builder.Services.AddSagaTimeoutCleanup();

		return builder;
	}
}
