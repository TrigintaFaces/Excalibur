// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Jobs.Abstractions;

namespace Excalibur.Jobs.Quartz;

/// <summary>
/// Provides a fluent API for configuring individual jobs in the Excalibur job system.
/// </summary>
public interface IJobConfigurator
{
	/// <summary>
	/// Adds a specific background job implementation to the job system.
	/// </summary>
	/// <typeparam name="TJob"> The job implementation type. </typeparam>
	/// <param name="cronExpression"> The cron expression for scheduling the job. </param>
	/// <param name="jobKey"> Optional unique key for the job. If not provided, the job type name will be used. </param>
	/// <returns> The job configurator for chaining. </returns>
	IJobConfigurator AddJob<TJob>(string cronExpression, string? jobKey = null)
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
	IJobConfigurator AddJob<TJob, TContext>(string cronExpression, TContext context, string? jobKey = null)
		where TJob : class, IBackgroundJob<TContext>
		where TContext : class;

	/// <summary>
	/// Adds a recurring job that executes at a specified interval.
	/// </summary>
	/// <typeparam name="TJob"> The job implementation type. </typeparam>
	/// <param name="interval"> The interval between job executions. </param>
	/// <param name="jobKey"> Optional unique key for the job. If not provided, the job type name will be used. </param>
	/// <returns> The job configurator for chaining. </returns>
	IJobConfigurator AddRecurringJob<TJob>(TimeSpan interval, string? jobKey = null)
		where TJob : class, IBackgroundJob;

	/// <summary>
	/// Adds a one-time job that executes immediately.
	/// </summary>
	/// <typeparam name="TJob"> The job implementation type. </typeparam>
	/// <param name="jobKey"> Optional unique key for the job. If not provided, the job type name will be used. </param>
	/// <returns> The job configurator for chaining. </returns>
	IJobConfigurator AddOneTimeJob<TJob>(string? jobKey = null)
		where TJob : class, IBackgroundJob;

	/// <summary>
	/// Adds a delayed job that executes after a specified delay.
	/// </summary>
	/// <typeparam name="TJob"> The job implementation type. </typeparam>
	/// <param name="delay"> The delay before executing the job. </param>
	/// <param name="jobKey"> Optional unique key for the job. If not provided, the job type name will be used. </param>
	/// <returns> The job configurator for chaining. </returns>
	IJobConfigurator AddDelayedJob<TJob>(TimeSpan delay, string? jobKey = null)
		where TJob : class, IBackgroundJob;

	/// <summary>
	/// Conditionally adds a job based on a predicate.
	/// </summary>
	/// <param name="condition"> The condition to evaluate. </param>
	/// <param name="configureJob"> Action to configure the job if the condition is true. </param>
	/// <returns> The job configurator for chaining. </returns>
	IJobConfigurator AddJobIf(bool condition, Action<IJobConfigurator> configureJob);

	/// <summary>
	/// Adds multiple instances of the same job type with different configurations.
	/// </summary>
	/// <typeparam name="TJob"> The job implementation type. </typeparam>
	/// <param name="configurations"> Array of job configurations. </param>
	/// <returns> The job configurator for chaining. </returns>
	IJobConfigurator AddJobInstances<TJob>(params JobConfiguration[] configurations)
		where TJob : class, IBackgroundJob;
}
