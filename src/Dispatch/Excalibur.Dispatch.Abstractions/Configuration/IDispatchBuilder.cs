// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;


using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Abstractions.Configuration;

/// <summary>
/// Fluent builder interface for configuring the Excalibur framework.
/// </summary>
public interface IDispatchBuilder
{
	/// <summary>
	/// Gets the service collection for dependency registration.
	/// </summary>
	/// <value> The DI service collection used to register Dispatch services. </value>
	IServiceCollection Services { get; }

	/// <summary>
	/// Configures a pipeline with the specified name.
	/// </summary>
	/// <param name="name"> The pipeline name. </param>
	/// <param name="configure"> Configuration action for the pipeline. </param>
	/// <returns> The builder for fluent configuration. </returns>
	IDispatchBuilder ConfigurePipeline(string name, Action<IPipelineBuilder> configure);

	/// <summary>
	/// Registers a pipeline profile.
	/// </summary>
	/// <param name="profile"> The profile to register. </param>
	/// <returns> The builder for fluent configuration. </returns>
	IDispatchBuilder RegisterProfile(IPipelineProfile profile);

	/// <summary>
	/// Adds a transport binding.
	/// </summary>
	/// <param name="configure"> Configuration action for the binding. </param>
	/// <returns> The builder for fluent configuration. </returns>
	IDispatchBuilder AddBinding(Action<IBindingConfigurationBuilder> configure);

	/// <summary>
	/// Adds global middleware to all pipelines.
	/// </summary>
	/// <typeparam name="TMiddleware"> The middleware type. </typeparam>
	/// <returns> The builder for fluent configuration. </returns>
	/// <remarks>
	/// <para>
	/// Global middleware registered via this method is added to <b>all</b> pipelines,
	/// including any auto-created default pipeline.
	/// </para>
	/// <para>
	/// If no explicit pipelines are configured via <see cref="ConfigurePipeline"/>,
	/// Dispatch automatically creates a "Default" pipeline containing all middleware
	/// registered through this method. This allows simple configurations without
	/// explicit pipeline setup.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// // Simple configuration - creates a default pipeline automatically
	/// builder.Services.AddDispatch(dispatch =>
	/// {
	///     dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
	///     dispatch.UseMiddleware&lt;LoggingMiddleware&gt;();
	///     dispatch.UseMiddleware&lt;ValidationMiddleware&gt;();
	/// });
	/// </code>
	/// </example>
	IDispatchBuilder UseMiddleware<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TMiddleware>()
		where TMiddleware : IDispatchMiddleware;

	/// <summary>
	/// Configures options for the Excalibur framework.
	/// </summary>
	/// <typeparam name="TOptions"> The options type. </typeparam>
	/// <param name="configure"> Configuration action for options. </param>
	/// <returns> The builder for fluent configuration. </returns>
	IDispatchBuilder ConfigureOptions<TOptions>(Action<TOptions> configure)
		where TOptions : class;
}
