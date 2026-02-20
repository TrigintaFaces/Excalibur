// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Configuration;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Options.Delivery;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Excalibur.Dispatch.Messaging;

/// <summary>
/// Extension methods for configuring dispatch messaging components and features. Provides fluent API for adding outbox, scheduling, inbox,
/// and related messaging patterns to the dispatch system builder.
/// </summary>
public static class MessagingDispatchBuilderExtensions
{
	/// <summary>
	/// Configures the outbox pattern with a custom outbox store implementation. Enables reliable message publishing by persisting messages
	/// before delivery and ensuring at-least-once semantics.
	/// </summary>
	/// <typeparam name="TStore"> The type of outbox store implementation to use. </typeparam>
	/// <param name="builder"> The dispatch builder to configure. </param>
	/// <returns> The dispatch builder for method chaining. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when <paramref name="builder" /> is null. </exception>
	public static IDispatchBuilder WithOutbox<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TStore>(
		this IDispatchBuilder builder)
		where TStore : class, IOutboxStore
	{
		ArgumentNullException.ThrowIfNull(builder);

		builder.Services.TryAddSingleton<IOutboxStore, TStore>();

		return builder;
	}

	/// <summary>
	/// Adds dispatch scheduling capabilities to the builder. Enables deferred message processing, scheduled execution, and time-based
	/// delivery for implementing temporal messaging patterns.
	/// </summary>
	/// <param name="builder"> The dispatch builder to configure. </param>
	/// <returns> The dispatch builder for method chaining. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when <paramref name="builder" /> is null. </exception>
	public static IDispatchBuilder WithDispatchScheduling(this IDispatchBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		// Scheduling orchestration is provided by Excalibur. Keep core wiring in Dispatch only.
		return builder;
	}

	/// <summary>
	/// Registers a custom schedule store implementation for the dispatch system.
	/// </summary>
	/// <typeparam name="TStore"> The type of schedule store implementation to register. </typeparam>
	/// <param name="builder"> The dispatch builder to configure. </param>
	/// <returns> The same dispatch builder instance for method chaining. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when <paramref name="builder" /> is null. </exception>
	public static IDispatchBuilder
		WithScheduleStore<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TStore>(
			this IDispatchBuilder builder)
		where TStore : class, IScheduleStore
	{
		ArgumentNullException.ThrowIfNull(builder);

		builder.Services.TryAddSingleton<IScheduleStore, TStore>();
		return builder;
	}

	/// <summary>
	/// Registers a custom dispatch scheduler implementation for the dispatch system.
	/// </summary>
	/// <typeparam name="TScheduler"> The type of dispatch scheduler implementation to register. </typeparam>
	/// <param name="builder"> The dispatch builder to configure. </param>
	/// <returns> The same dispatch builder instance for method chaining. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when <paramref name="builder" /> is null. </exception>
	public static IDispatchBuilder WithDispatchScheduler<
	[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
	TScheduler>(this IDispatchBuilder builder)
		where TScheduler : class, IDispatchScheduler
	{
		ArgumentNullException.ThrowIfNull(builder);

		builder.Services.TryAddSingleton<IDispatchScheduler, TScheduler>();
		return builder;
	}

	/// <summary>
	/// Configures the dispatch system with an inbox pattern implementation for message deduplication and reliability.
	/// </summary>
	/// <typeparam name="TStore"> The type of inbox store implementation to register. </typeparam>
	/// <param name="builder"> The dispatch builder to configure. </param>
	/// <param name="configure"> Optional configuration action for inbox options. </param>
	/// <returns> The same dispatch builder instance for method chaining. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when <paramref name="builder" /> is null. </exception>
	public static IDispatchBuilder WithInbox<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TStore>(
		this IDispatchBuilder builder, Action<InboxOptions>? configure = null)
		where TStore : class, IInboxStore
	{
		ArgumentNullException.ThrowIfNull(builder);

		builder.Services.TryAddSingleton<IInboxStore, TStore>();
		if (configure is not null)
		{
			_ = builder.Services.Configure(configure);
		}

		return builder;
	}

	/// <summary>
	/// Configures inbox options using values from configuration.
	/// </summary>
	/// <param name="builder"> The dispatch builder to configure. </param>
	/// <param name="configuration"> The configuration instance containing inbox settings. </param>
	/// <returns> The same dispatch builder instance for method chaining. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when <paramref name="builder" /> or <paramref name="configuration" /> is null. </exception>
	[RequiresUnreferencedCode(
		"Configuration binding may reference types not preserved during trimming. Ensure options types are annotated with DynamicallyAccessedMembers.")]
	[RequiresDynamicCode("Configuration binding requires dynamic code generation for property reflection and value conversion.")]
	public static IDispatchBuilder WithInboxOptions(this IDispatchBuilder builder, IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configuration);

		// Bind configuration into InboxOptions (root or pre-filtered by caller's section).
		_ = builder.Services.Configure<InboxOptions>(configuration);
		return builder;
	}
}
