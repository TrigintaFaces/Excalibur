// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization.Metadata;

namespace Excalibur.Jobs.Quartz;

/// <summary>
/// Provides a fluent API for configuring individual jobs in the Excalibur job system.
/// </summary>
/// <remarks>
/// Core interface with 4 methods. Convenience methods are available
/// as extension methods in <see cref="JobConfiguratorExtensions"/>.
/// </remarks>
public interface IJobConfigurator
{
	/// <summary>
	/// Adds a specific background job implementation to the job system.
	/// </summary>
	/// <typeparam name="TJob"> The job implementation type. </typeparam>
	/// <param name="cronExpression"> The cron expression for scheduling the job. </param>
	/// <param name="jobKey"> Optional unique key for the job. If not provided, the job type name will be used. </param>
	/// <returns> The job configurator for chaining. </returns>
	IJobConfigurator AddJob<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TJob>(
		string cronExpression, string? jobKey = null)
		where TJob : class, IBackgroundJob;

	/// <summary>
	/// Adds a specific background job implementation with context to the job system.
	/// </summary>
	/// <typeparam name="TJob"> The job implementation type. </typeparam>
	/// <typeparam name="TContext"> The context type for the job. </typeparam>
	/// <param name="cronExpression"> The cron expression for scheduling the job. </param>
	/// <param name="context"> The context data to pass to the job. </param>
	/// <param name="jobKey"> Optional unique key for the job. If not provided, the job type name will be used. </param>
	/// <returns> The job configurator for chaining. </returns>
	IJobConfigurator AddJob<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TJob, TContext>(
		string cronExpression, TContext context, string? jobKey = null)
		where TJob : class, IBackgroundJob<TContext>
		where TContext : class;

	/// <summary>Adds a job with explicitly supplied JSON metadata for trimming and Native AOT.</summary>
	/// <typeparam name="TJob">The job implementation.</typeparam>
	/// <typeparam name="TContext">The context type.</typeparam>
	/// <param name="cronExpression">The schedule.</param>
	/// <param name="context">The initial persisted context.</param>
	/// <param name="typeInfo">The serialization metadata, registered again on every host startup.</param>
	/// <param name="contextVersion">The persisted codec version. Change it when the JSON contract changes and migrate existing payloads.</param>
	/// <param name="jobKey">The stable job name.</param>
	/// <returns>The configurator.</returns>
	IJobConfigurator AddJob<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TJob, TContext>(
		string cronExpression, TContext context, JsonTypeInfo<TContext> typeInfo, string? jobKey = null, string contextVersion = "1")
		where TJob : class, IBackgroundJob<TContext>
		where TContext : class => throw new NotSupportedException("This configurator does not support explicit JSON metadata.");

	/// <summary>
	/// Adds a one-time job that executes immediately.
	/// </summary>
	/// <typeparam name="TJob"> The job implementation type. </typeparam>
	/// <param name="jobKey"> Optional unique key for the job. If not provided, the job type name will be used. </param>
	/// <returns> The job configurator for chaining. </returns>
	IJobConfigurator AddOneTimeJob<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TJob>(
		string? jobKey = null)
		where TJob : class, IBackgroundJob;

	/// <summary>
	/// Adds a delayed job that executes after a specified delay.
	/// </summary>
	/// <typeparam name="TJob"> The job implementation type. </typeparam>
	/// <param name="delay"> The delay before executing the job. </param>
	/// <param name="jobKey"> Optional unique key for the job. If not provided, the job type name will be used. </param>
	/// <returns> The job configurator for chaining. </returns>
	IJobConfigurator AddDelayedJob<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TJob>(
		TimeSpan delay, string? jobKey = null)
		where TJob : class, IBackgroundJob;
}
