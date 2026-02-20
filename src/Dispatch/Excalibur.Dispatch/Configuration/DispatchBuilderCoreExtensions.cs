// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Configuration;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Excalibur.Dispatch.Configuration;

/// <summary>
/// Core extension methods for the IDispatchBuilder fluent configuration API.
/// </summary>
public static class DispatchBuilderCoreExtensions
{
	/// <summary>
	/// Adds an event handler to the dispatch pipeline.
	/// </summary>
	/// <typeparam name="THandler">The type of the event handler.</typeparam>
	/// <param name="builder">The dispatch builder.</param>
	/// <returns>The builder for fluent configuration.</returns>
	public static IDispatchBuilder
		AddEventHandler<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] THandler>(
			this IDispatchBuilder builder)
		where THandler : class
	{
		ArgumentNullException.ThrowIfNull(builder);

		builder.Services.TryAddScoped<THandler>();

		return builder;
	}

	/// <summary>
	/// Adds an action handler (command/query handler) to the dispatch pipeline.
	/// </summary>
	/// <typeparam name="THandler">The type of the action handler.</typeparam>
	/// <param name="builder">The dispatch builder.</param>
	/// <returns>The builder for fluent configuration.</returns>
	public static IDispatchBuilder AddActionHandler<
	[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
	THandler>(this IDispatchBuilder builder)
		where THandler : class
	{
		ArgumentNullException.ThrowIfNull(builder);

		builder.Services.TryAddScoped<THandler>();

		return builder;
	}

	/// <summary>
	/// Adds a message handler to the dispatch pipeline.
	/// </summary>
	/// <typeparam name="TMessage">The type of message handled.</typeparam>
	/// <typeparam name="THandler">The type of the handler.</typeparam>
	/// <param name="builder">The dispatch builder.</param>
	/// <returns>The builder for fluent configuration.</returns>
	public static IDispatchBuilder AddHandler<TMessage,
	[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
	THandler>(this IDispatchBuilder builder)
		where TMessage : class, IDispatchMessage
		where THandler : class, IDispatchHandler<TMessage>
	{
		ArgumentNullException.ThrowIfNull(builder);

		builder.Services.TryAddScoped<THandler>();
		builder.Services.TryAddScoped<IDispatchHandler<TMessage>, THandler>();

		return builder;
	}
}
