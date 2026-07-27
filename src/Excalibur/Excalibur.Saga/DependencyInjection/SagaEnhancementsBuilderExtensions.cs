// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Saga.DependencyInjection;
using Excalibur.Saga.Handlers;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods on <see cref="ISagaBuilder"/> for configuring saga enhancements.
/// </summary>
public static class SagaEnhancementsBuilderExtensions
{
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
	public static ISagaBuilder WithNotFoundHandler<TSaga,
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] THandler>(this ISagaBuilder builder)
		where TSaga : class
		where THandler : class, ISagaNotFoundHandler<TSaga>
	{
		ArgumentNullException.ThrowIfNull(builder);

		_ = builder.Services.AddSagaNotFoundHandler<TSaga, THandler>();

		return builder;
	}
}
