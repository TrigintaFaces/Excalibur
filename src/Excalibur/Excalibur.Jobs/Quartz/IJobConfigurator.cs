// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Jobs.Abstractions;

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
}
