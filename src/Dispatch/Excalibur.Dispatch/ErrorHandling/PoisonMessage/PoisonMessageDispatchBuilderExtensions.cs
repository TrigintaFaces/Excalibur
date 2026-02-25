// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions.Configuration;
using Excalibur.Dispatch.Options.ErrorHandling;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.ErrorHandling;

/// <summary>
/// Extension methods for configuring poison message handling in the dispatch pipeline.
/// </summary>
public static class PoisonMessageDispatchBuilderExtensions
{
	/// <summary>
	/// Adds poison message handling to the dispatch pipeline.
	/// </summary>
	/// <param name="builder"> The dispatch builder. </param>
	/// <param name="configureOptions"> Action to configure poison message options. </param>
	/// <returns> The dispatch builder for chaining. </returns>
	public static IDispatchBuilder AddPoisonMessageHandling(
		this IDispatchBuilder builder,
		Action<PoisonMessageOptions>? configureOptions = null)
	{
		ArgumentNullException.ThrowIfNull(builder);

		_ = builder.Services.AddPoisonMessageHandling(configureOptions);

		return builder;
	}

	/// <summary>
	/// Adds poison message handling to the dispatch pipeline using configuration.
	/// </summary>
	/// <param name="builder"> The dispatch builder. </param>
	/// <param name="configuration"> The configuration section for poison message options. </param>
	/// <returns> The dispatch builder for chaining. </returns>
	[RequiresUnreferencedCode("Uses reflection which may break with AOT compilation")]
	[RequiresDynamicCode("Uses dynamic code generation which requires JIT compilation")]
	public static IDispatchBuilder AddPoisonMessageHandling(
		this IDispatchBuilder builder,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = builder.Services.AddPoisonMessageHandling(configuration);

		return builder;
	}

	/// <summary>
	/// Configures the dispatch pipeline to use an in-memory dead letter store.
	/// </summary>
	/// <param name="builder"> The dispatch builder. </param>
	/// <returns> The dispatch builder for chaining. </returns>
	public static IDispatchBuilder UseInMemoryDeadLetterStore(this IDispatchBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		_ = builder.Services.AddInMemoryDeadLetterStore();

		return builder;
	}

	// NOTE: SQL dead letter store moved to Excalibur.Data.SqlServer.AddSqlServerDeadLetterStore() (Sprint 306)

	/// <summary>
	/// Adds a custom poison message detector to the pipeline.
	/// </summary>
	/// <typeparam name="TDetector"> The type of the detector to add. </typeparam>
	/// <param name="builder"> The dispatch builder. </param>
	/// <returns> The dispatch builder for chaining. </returns>
	public static IDispatchBuilder AddPoisonDetector<
	[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
	TDetector>(this IDispatchBuilder builder)
		where TDetector : class, IPoisonMessageDetector
	{
		ArgumentNullException.ThrowIfNull(builder);

		_ = builder.Services.AddPoisonMessageDetector<TDetector>();

		return builder;
	}

	// NOTE: AddProductionPoisonMessageHandling moved to Excalibur.Data.SqlServer (Sprint 306)
}
